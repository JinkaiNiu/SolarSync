// ============================================================================
// 朝夕·光色 - 全局状态管理器
// 开发者: JinkaiNiu (niujinkai1997@qq.com)
// 主页: https://kaneniu.com
// 版本: 1.0.0.0
// 说明: 协调 IP 定位、日出日落计算、主题切换等模块，
//       管理定时器实现日出日落时自动切换主题。
// ============================================================================

using SolarSync.Models;

namespace SolarSync.Services;

/// <summary>
/// 全局状态管理器。协调各服务模块的工作流程：
/// 1. 启动时获取 IP → 城市 → 经纬度 → 计算日出日落
/// 2. 根据日出日落时间设置定时器自动切换主题
/// 3. 每日凌晨自动重算，应对跨日
/// 4. 提供手动刷新和手动切换主题功能
/// </summary>
public sealed class AppStateManager : IDisposable
{
    private readonly IpLocationService _ipService;
    private readonly ThemeService _themeService;
    private readonly System.Threading.Timer? _dailyTimer;
    private readonly System.Threading.Timer? _switchTimer;
    private CancellationTokenSource? _cts;

    /// <summary>当前 IP 地理位置信息</summary>
    public IpInfo? CurrentIpInfo { get; private set; }

    /// <summary>当前日出日落信息</summary>
    public SolarInfo? CurrentSolarInfo { get; private set; }

    /// <summary>当前 Windows 主题模式</summary>
    public ThemeMode CurrentTheme => _themeService.GetCurrentTheme();

    /// <summary>是否处于自动切换模式</summary>
    public bool IsAutoMode { get; private set; } = true;

    /// <summary>是否已完成初始化</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>数据刷新完成事件（IP、城市、日出日落时间更新时触发）</summary>
    public event Action? OnDataRefreshed;

    /// <summary>主题切换完成事件</summary>
    public event Action<ThemeMode>? OnThemeChanged;

    /// <summary>主题切换开始事件（异步切换开始时触发，用于 UI 显示"切换中"状态）</summary>
    public event Action? OnThemeSwitching;

    public AppStateManager()
    {
        _ipService = new IpLocationService();
        _themeService = new ThemeService();

        // 每日定时器：凌晨 00:05 自动刷新数据
        _dailyTimer = new System.Threading.Timer(
            _ => _ = RefreshAsync(),
            null, Timeout.Infinite, Timeout.Infinite);

        // 主题切换定时器：在日出/日落时刻触发
        _switchTimer = new System.Threading.Timer(
            _ => PerformScheduledSwitch(),
            null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>初始化：立即刷新数据并调度下一次每日更新</summary>
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        IsInitialized = true;
        ScheduleNextDailyCheck();
    }

    /// <summary>刷新所有数据（IP、城市、经纬度、日出日落）</summary>
    public async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var ipInfo = await _ipService.GetLocationAsync()
                .ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            if (ipInfo != null)
            {
                CurrentIpInfo = ipInfo;
                // 获取到有效经纬度时才计算日出日落
                if (ipInfo.Latitude != 0 || ipInfo.Longitude != 0)
                {
                    CurrentSolarInfo = SolarCalculator.Calculate(
                        ipInfo.Latitude, ipInfo.Longitude, DateTime.Now, 8);
                }
            }

            OnDataRefreshed?.Invoke();

            // 自动模式下根据新数据重新调度
            if (IsAutoMode && CurrentSolarInfo != null)
                ScheduleNextSwitch();
        }
        catch { }
    }

    /// <summary>设置或取消自动切换模式</summary>
    public void SetAutoMode(bool enabled)
    {
        IsAutoMode = enabled;
        if (enabled && CurrentSolarInfo != null)
            ScheduleNextSwitch();
        else
            _switchTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>手动切换主题（异步，不阻塞 UI）</summary>
    public async Task SetThemeManuallyAsync(ThemeMode mode, IntPtr windowHandle)
    {
        IsAutoMode = false;
        _switchTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        await ApplyThemeAsync(mode, windowHandle).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步应用主题（在后台线程执行广播，不阻塞 UI）。
    /// 先触发 OnThemeSwitching 事件显示"切换中"提示，
    /// 后台线程完成注册表写入和广播后触发 OnThemeChanged。
    /// </summary>
    public async Task ApplyThemeAsync(ThemeMode mode, IntPtr? windowHandle = null)
    {
        OnThemeSwitching?.Invoke();

        // 将耗时的 SendMessageTimeout 广播放入后台线程
        await Task.Run(() =>
        {
            _themeService.SetTheme(mode, windowHandle);
        }).ConfigureAwait(true);

        OnThemeChanged?.Invoke(mode);
    }

    /// <summary>定时器回调：执行计划中的主题切换</summary>
    private void PerformScheduledSwitch()
    {
        if (!IsAutoMode || CurrentSolarInfo == null) return;

        var now = DateTime.Now;
        var targetTheme = CurrentSolarInfo.IsDaytime(now)
            ? ThemeMode.Light : ThemeMode.Dark;

        if (_themeService.GetCurrentTheme() != targetTheme)
        {
            OnThemeSwitching?.Invoke();
            // 后台线程执行切换，完成后回调 UI 线程
            Task.Run(() =>
            {
                _themeService.SetTheme(targetTheme);
            }).ContinueWith(_ =>
            {
                OnThemeChanged?.Invoke(targetTheme);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        ScheduleNextSwitch();
    }

    /// <summary>计算并设置下一次切换的定时器</summary>
    private void ScheduleNextSwitch()
    {
        if (CurrentSolarInfo == null) return;

        var now = DateTime.Now;
        TimeSpan delay;

        if (CurrentSolarInfo.IsDaytime(now))
        {
            // 白昼：定时到日落
            delay = CurrentSolarInfo.TimeUntilSunset(now);
        }
        else
        {
            // 夜间：定时到次日日出
            var tomorrowSunrise = CurrentSolarInfo.TimeUntilSunrise(now);
            delay = tomorrowSunrise > TimeSpan.Zero
                ? tomorrowSunrise
                : TimeSpan.FromHours(1);
        }

        if (delay <= TimeSpan.Zero)
            delay = TimeSpan.FromMinutes(1);

        _switchTimer?.Change((long)delay.TotalMilliseconds, Timeout.Infinite);
    }

    /// <summary>设定每日凌晨自动刷新定时器</summary>
    private void ScheduleNextDailyCheck()
    {
        var now = DateTime.Now;
        var nextCheck = now.Date.AddDays(1).AddMinutes(5);
        var delay = nextCheck - now;
        if (delay < TimeSpan.Zero) delay = TimeSpan.FromMinutes(1);

        _dailyTimer?.Change((long)delay.TotalMilliseconds, Timeout.Infinite);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _dailyTimer?.Dispose();
        _switchTimer?.Dispose();
        _ipService.Dispose();
    }
}
