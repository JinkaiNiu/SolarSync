// ============================================================================
// 朝夕·光色 - Windows 主题切换服务
// 开发者: JinkaiNiu (niujinkai1997@qq.com)
// 主页: https://kaneniu.com
// 版本: 1.0.3.0
// 说明: 通过操作 Windows 注册表和 DWM API 实现浅色/深色模式切换，
//       完全使用原生 Windows 接口，无需第三方依赖。
// ============================================================================

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SolarSync.Services;

/// <summary>Windows 主题模式枚举</summary>
public enum ThemeMode
{
    /// <summary>浅色模式</summary>
    Light,
    /// <summary>深色模式</summary>
    Dark
}

/// <summary>
/// Windows 主题切换服务。
/// 通过修改注册表 Personalized 键值 + DWM API 实现系统级深浅色切换。
/// 切换完成后通过 SendMessageTimeout 广播通知所有窗口。
/// </summary>
public sealed class ThemeService
{
    /// <summary>Windows 主题注册表路径</summary>
    private const string ThemeRegPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>DWM 深色模式属性 ID（Win10 20H1+）</summary>
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>广播消息常量：发送到所有顶层窗口</summary>
    private const int HWND_BROADCAST = 0xffff;

    /// <summary>WM_SETTINGCHANGE 消息 ID</summary>
    private const int WM_SETTINGCHANGE = 0x001A;

    /// <summary>SendMessageTimeout 标志：遇到挂起窗口时立即返回</summary>
    private const int SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>获取当前 Windows 主题模式（通过注册表读取）</summary>
    public ThemeMode GetCurrentTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeRegPath);
            if (key?.GetValue("AppsUseLightTheme") is int val)
                return val == 1 ? ThemeMode.Light : ThemeMode.Dark;
        }
        catch { }
        return ThemeMode.Light;
    }

    /// <summary>
    /// 设置 Windows 主题模式。
    /// 写入 AppsUseLightTheme + SystemUsesLightTheme 注册表键，
    /// 并通过 DWM API 设置当前窗口标题栏主题。
    /// 最后广播 WM_SETTINGCHANGE 通知系统刷新。
    /// </summary>
    /// <param name="mode">目标主题模式</param>
    /// <param name="windowHandle">当前窗口句柄（用于设置标题栏主题，可选）</param>
    public void SetTheme(ThemeMode mode, IntPtr? windowHandle = null)
    {
        try
        {
            var isLight = mode == ThemeMode.Light;

            // 写入 AppsUseLightTheme（控制应用程序主题）
            Registry.SetValue(
                $@"HKEY_CURRENT_USER\{ThemeRegPath}",
                "AppsUseLightTheme", isLight ? 1 : 0,
                RegistryValueKind.DWord);

            // 写入 SystemUsesLightTheme（控制任务栏等系统 UI 主题）
            Registry.SetValue(
                $@"HKEY_CURRENT_USER\{ThemeRegPath}",
                "SystemUsesLightTheme", isLight ? 1 : 0,
                RegistryValueKind.DWord);

            // 通过 DWM API 设置当前窗口标题栏主题
            if (windowHandle.HasValue && windowHandle.Value != IntPtr.Zero)
            {
                var useDark = mode == ThemeMode.Dark ? 1 : 0;
                DwmSetWindowAttribute(windowHandle.Value,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref useDark, sizeof(int));
            }

            // 广播主题变更通知（异步等待各窗口响应，不阻塞 UI）
            BroadcastThemeChange();
        }
        catch
        {
            // 静默失败：可能在旧版 Windows 或无权限环境下运行
        }
    }

    /// <summary>
    /// 通过 SendMessageTimeout 广播 WM_SETTINGCHANGE 消息，
    /// 通知 explorer.exe 及其他窗口重新加载主题配色。
    /// 使用 SMTO_ABORTIFHUNG 避免因挂起窗口导致长时间阻塞。
    /// </summary>
    private static void BroadcastThemeChange()
    {
        SendMessageTimeout(
            (IntPtr)HWND_BROADCAST,
            WM_SETTINGCHANGE,
            IntPtr.Zero,
            "ImmersiveColorSet",
            SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, int Msg, IntPtr wParam, string lParam,
        int fuFlags, int uTimeout, out IntPtr lpdwResult);
}
