// ============================================================================
// 朝夕·光色 - 主窗口及系统托盘
// 开发者: JinkaiNiu (niujinkai1997@qq.com)
// 主页: https://kaneniu.com
// 版本: 1.0.0.0
// 说明: 主界面展示公网 IP、城市、日出日落时间、主题模式、数据来源；
//       系统托盘支持隐藏/显示、手动切换、开机自启等功能。
// ============================================================================

using System.Drawing.Drawing2D;
using System.Drawing.Text;
using SolarSync.Models;
using SolarSync.Services;

namespace SolarSync;

/// <summary>
/// 应用程序主窗口。显示当前 IP、城市、日出日落时间、主题状态，
/// 提供手动切换、刷新数据和自动模式开关等功能。
/// 关闭按钮实际隐藏到系统托盘，右键菜单可真正退出。
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppStateManager _state;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly System.Windows.Forms.Timer _uiTimer;

    // UI 控件
    private readonly Label _lblIp;
    private readonly Label _lblCity;
    private readonly Label _lblSunrise;
    private readonly Label _lblSunset;
    private readonly Label _lblTheme;
    private readonly Label _lblSource1;
    private readonly Label _lblSource2;
    private readonly Label _lblSource3;
    private readonly Label _lblSource4;
    private readonly Panel _timelinePanel;
    private readonly Button _btnRefresh;
    private readonly Button _btnLight;
    private readonly Button _btnDark;
    private readonly Button _btnAuto;
    private readonly CheckBox _chkStartHidden;
    private readonly LinkLabel _lblDevInfo;

    // 数据值标签
    private readonly Label _lblIpValue;
    private readonly Label _lblCityValue;
    private readonly Label _lblSunriseValue;
    private readonly Label _lblSunsetValue;
    private readonly Label _lblThemeValue;

    public MainForm(string[] args)
    {
        _state = new AppStateManager();
        _state.OnDataRefreshed += OnDataRefreshed;
        _state.OnThemeChanged += OnThemeChanged;
        _state.OnThemeSwitching += OnThemeSwitching;

        // ---- 窗口基础设置 ----
        Text = "朝夕·光色 v1.0 — 日出日落主题切换";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(460, 420);

        var fontSize = 10f;
        var valueWidth = 280;
        var xLabel = 20;
        var xValue = 150;
        var yStart = 20;
        var rowHeight = 28;

        Font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular);

        // ---- 左侧标签列 ----
        _lblIp = CreateLabel(xLabel, yStart, "公网 IP：");
        _lblCity = CreateLabel(xLabel, yStart + rowHeight, "所在城市：");
        _lblSunrise = CreateLabel(xLabel, yStart + 2 * rowHeight, "日出时间：");
        _lblSunset = CreateLabel(xLabel, yStart + 3 * rowHeight, "日落时间：");
        _lblTheme = CreateLabel(xLabel, yStart + 4 * rowHeight, "当前主题：");

        // ---- 右侧数值列 ----
        var vOffset = 6;
        _lblIpValue = CreateValueLabel(xValue, yStart + vOffset, valueWidth, "获取中...");
        _lblCityValue = CreateValueLabel(xValue, yStart + rowHeight + vOffset, valueWidth, "");
        _lblSunriseValue = CreateValueLabel(xValue, yStart + 2 * rowHeight + vOffset, valueWidth, "");
        _lblSunsetValue = CreateValueLabel(xValue, yStart + 3 * rowHeight + vOffset, valueWidth, "");
        _lblThemeValue = CreateValueLabel(xValue, yStart + 4 * rowHeight + vOffset, valueWidth, "");

        Controls.AddRange([_lblIp, _lblCity, _lblSunrise, _lblSunset, _lblTheme,
                           _lblIpValue, _lblCityValue, _lblSunriseValue,
                           _lblSunsetValue, _lblThemeValue]);

        // ---- 时间轴面板（自定义绘制日出日落时间轴） ----
        _timelinePanel = new TimelinePanel
        {
            Location = new Point(20, yStart + 5 * rowHeight + 10),
            Size = new Size(420, 50)
        };
        Controls.Add(_timelinePanel);

        // ---- 数据来源说明 ----
        var sourceY = _timelinePanel.Bottom + 10;
        _lblSource1 = new Label
        {
            Location = new Point(20, sourceY), Size = new Size(420, 18),
            Text = "数据来源：",
            Font = new Font("Microsoft YaHei UI", 9f)
        };
        _lblSource2 = new Label
        {
            Location = new Point(20, sourceY + 20), Size = new Size(420, 18),
            Text = "  IP 定位：myip.ipip.net",
            Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Color.Gray
        };
        _lblSource3 = new Label
        {
            Location = new Point(20, sourceY + 40), Size = new Size(420, 18),
            Text = "  城市坐标：内置数据库 (350+城市)",
            Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Color.Gray
        };
        _lblSource4 = new Label
        {
            Location = new Point(20, sourceY + 60), Size = new Size(420, 18),
            Text = "  日出日落：NOAA 太阳位置算法",
            Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Color.Gray
        };
        Controls.AddRange([_lblSource1, _lblSource2, _lblSource3, _lblSource4]);

        // ---- 操作按钮 ----
        var btnY = _lblSource4.Bottom + 10;
        _btnRefresh = new Button
        {
            Location = new Point(20, btnY), Size = new Size(90, 30),
            Text = "刷新"
        };
        _btnRefresh.Click += async (_, _) => await _state.RefreshAsync();

        _btnLight = new Button
        {
            Location = new Point(120, btnY), Size = new Size(90, 30),
            Text = "浅色模式"
        };
        _btnLight.Click += async (_, _) =>
            await _state.SetThemeManuallyAsync(ThemeMode.Light, Handle);

        _btnDark = new Button
        {
            Location = new Point(220, btnY), Size = new Size(90, 30),
            Text = "深色模式"
        };
        _btnDark.Click += async (_, _) =>
            await _state.SetThemeManuallyAsync(ThemeMode.Dark, Handle);

        _btnAuto = new Button
        {
            Location = new Point(320, btnY), Size = new Size(120, 30),
            Text = "自动切换中", BackColor = Color.LightGreen
        };
        _btnAuto.Click += (_, _) =>
        {
            _state.SetAutoMode(true);
            UpdateAutoButton();
        };

        Controls.AddRange([_btnRefresh, _btnLight, _btnDark, _btnAuto]);

        // ---- 底部版权与版本信息 ----
        var footerY = btnY + 38;
        var footerFont = new Font("Microsoft YaHei UI", 8f);

        var lblCopyright = new Label
        {
            Location = new Point(20, footerY),
            Size = new Size(200, 20),
            Text = "© 2026 JinkaiNiu",
            Font = footerFont,
            ForeColor = Color.Gray
        };
        Controls.Add(lblCopyright);

        _lblDevInfo = new LinkLabel
        {
            Location = new Point(290, footerY),
            Size = new Size(150, 20),
            Text = "朝夕·光色  v1.0",
            TextAlign = ContentAlignment.MiddleRight,
            Font = footerFont,
            ForeColor = Color.Gray,
            LinkColor = Color.Gray,
            ActiveLinkColor = Color.DarkOrange,
            VisitedLinkColor = Color.Gray
        };
        _lblDevInfo.LinkClicked += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://kaneniu.com",
                    UseShellExecute = true
                }); }
            catch { }
        };
        Controls.Add(_lblDevInfo);

        // ---- 启动隐藏复选框 ----
        _chkStartHidden = new CheckBox
        {
            Location = new Point(20, btnY + 40),
            Size = new Size(200, 24),
            Text = "启动时隐藏窗口到托盘",
            Checked = args.Length > 0 && args.Contains("--hidden")
        };
        Controls.Add(_chkStartHidden);

        // ---- 系统托盘右键菜单 ----
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("显示/隐藏窗口", null, (_, _) => ToggleWindow());
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("强制浅色模式", null,
            async (_, _) => await _state.SetThemeManuallyAsync(
                ThemeMode.Light, Handle));
        _trayMenu.Items.Add("强制深色模式", null,
            async (_, _) => await _state.SetThemeManuallyAsync(
                ThemeMode.Dark, Handle));
        _trayMenu.Items.Add("-");

        var autoItem = _trayMenu.Items.Add("自动切换");
        autoItem.Click += (_, _) =>
        {
            _state.SetAutoMode(true);
            UpdateAutoButton();
        };

        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("刷新数据", null,
            async (_, _) => await _state.RefreshAsync());
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("开机自启...", null, (_, _) => ToggleStartup());
        _trayMenu.Items.Add("关于 朝夕·光色", null, (_, _) => ShowAbout());
        _trayMenu.Items.Add("退出", null, (_, _) => ExitApp());

        // ---- 系统托盘图标 ----
        var trayText = "朝夕·光色 v1.0 — 根据日出日落切换 Windows 主题";
        _trayIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = trayText.Length > 63 ? trayText[..63] : trayText,
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ToggleWindow();

        // ---- 定时器：每分钟刷新 UI ----
        _uiTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        _uiTimer.Tick += (_, _) => UpdateUI();
        _uiTimer.Start();

        // ---- 窗口事件 ----
        FormClosing += OnFormClosing;
        Shown += async (_, _) =>
        {
            if (_chkStartHidden.Checked)
            {
                Hide();
                ShowInTaskbar = false;
            }
            await _state.InitializeAsync();
        };

        BackColor = Color.White;
        ForeColor = Color.Black;
    }

    /// <summary>创建左侧标签</summary>
    private static Label CreateLabel(int x, int y, string text) => new()
    {
        Location = new Point(x, y + 6),
        Size = new Size(120, 22),
        Text = text,
        TextAlign = ContentAlignment.MiddleRight,
        Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold)
    };

    /// <summary>创建右侧数据值标签</summary>
    private static Label CreateValueLabel(int x, int y, int width, string text) => new()
    {
        Location = new Point(x, y),
        Size = new Size(width, 22),
        AutoSize = false,
        Text = text,
        Font = new Font("Consolas", 10f)
    };

    /// <summary>事件回调：主题切换开始，显示"⏳ 切换中..."</summary>
    private void OnThemeSwitching()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnThemeSwitching);
            return;
        }
        _lblThemeValue.Text = "⏳ 切换中...";
        _lblThemeValue.ForeColor = Color.DarkOrange;
    }

    /// <summary>事件回调：数据刷新完成，更新 UI</summary>
    private void OnDataRefreshed()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnDataRefreshed);
            return;
        }
        UpdateUI();
    }

    /// <summary>事件回调：主题切换完成，更新 UI</summary>
    private void OnThemeChanged(ThemeMode mode)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnThemeChanged(mode));
            return;
        }
        _lblThemeValue.ForeColor = Color.Black;
        UpdateUI();
        UpdateAutoButton();
    }

    /// <summary>刷新所有 UI 显示</summary>
    private void UpdateUI()
    {
        var ip = _state.CurrentIpInfo;
        var cacheSuffix = ip?.FromCache == true ? "（缓存）" : "";
        _lblIpValue.Text = ip?.Ip != null ? $"{ip.Ip}{cacheSuffix}" : "获取失败";
        _lblCityValue.Text = ip?.DisplayName ?? "未知";

        var solar = _state.CurrentSolarInfo;
        if (solar != null)
        {
            _lblSunriseValue.Text = solar.Sunrise.ToString("HH:mm");
            _lblSunsetValue.Text = solar.Sunset.ToString("HH:mm");
        }
        else
        {
            _lblSunriseValue.Text = "待获取";
            _lblSunsetValue.Text = "待获取";
        }

        var theme = _state.CurrentTheme;
        var themeIcon = theme == ThemeMode.Light ? "☀" : "🌙";
        var themeText = theme == ThemeMode.Light ? "浅色模式" : "深色模式";
        var autoStatus = _state.IsAutoMode ? "（自动切换）" : "（手动模式）";
        _lblThemeValue.Text = $"{themeIcon} {themeText} {autoStatus}";

        // 数据来源状态提示
        if (ip?.FromCache == true)
        {
            _lblSource2.ForeColor = Color.DarkOrange;
            _lblSource2.Text = "  IP 定位：上次缓存结果";
        }
        else if (ip != null)
        {
            _lblSource2.ForeColor = Color.Gray;
            _lblSource2.Text = "  IP 定位：myip.ipip.net";
        }
        else
        {
            _lblSource2.ForeColor = Color.Red;
            _lblSource2.Text = "  IP 定位：获取失败，请检查网络";
        }

        _timelinePanel.Invalidate();
    }

    /// <summary>更新自动切换按钮状态</summary>
    private void UpdateAutoButton()
    {
        if (InvokeRequired)
        {
            BeginInvoke(UpdateAutoButton);
            return;
        }
        if (_state.IsAutoMode)
        {
            _btnAuto.Text = "自动切换中";
            _btnAuto.BackColor = Color.LightGreen;
        }
        else
        {
            _btnAuto.Text = "已暂停自动";
            _btnAuto.BackColor = Color.LightCoral;
        }
    }

    /// <summary>切换窗口显示/隐藏</summary>
    private void ToggleWindow()
    {
        if (Visible)
        {
            Hide();
            ShowInTaskbar = false;
        }
        else
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            BringToFront();
        }
    }

    /// <summary>点击关闭按钮时隐藏到托盘而非退出</summary>
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
        }
    }

    /// <summary>显示关于对话框</summary>
    private void ShowAbout()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";
        MessageBox.Show(
            $"朝夕·光色  v{ver}\r\n\r\n"
            + "根据日出日落时间自动切换 Windows 浅色/深色模式\r\n\r\n"
            + "开发者：JinkaiNiu\r\n"
            + "主页：https://kaneniu.com\r\n"
            + "邮箱：niujinkai1997@qq.com\r\n\r\n"
            + "IP 定位：myip.ipip.net\r\n"
            + "城市坐标：内置数据库 (350+城市)\r\n"
            + "天文算法：NOAA 太阳位置算法\r\n\r\n"
            + "© 2026 JinkaiNiu",
            "关于 朝夕·光色",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>真正退出程序</summary>
    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _uiTimer.Stop();
        _state.Dispose();
        Application.Exit();
    }

    /// <summary>切换开机自启状态（写入/删除 HKCU Run 注册表）</summary>
    private void ToggleStartup()
    {
        var keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            keyPath, true);
        if (key == null) return;

        var current = key.GetValue("SolarSync") as string;
        if (current != null)
        {
            key.DeleteValue("SolarSync");
            MessageBox.Show("已关闭开机自启", "设置",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess()
                .MainModule?.FileName;
            if (exePath != null)
            {
                key.SetValue("SolarSync",
                    $"\"{exePath}\" --hidden");
                MessageBox.Show("已开启开机自启（最小化启动）", "设置",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    /// <summary>在运行时生成应用程序图标（太阳图案）</summary>
    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        // 绘制太阳本体
        using var brush = new SolidBrush(Color.DarkOrange);
        g.FillEllipse(brush, 4, 4, 24, 24);

        // 绘制太阳光芒
        using var pen = new Pen(Color.Yellow, 2);
        g.DrawLine(pen, 16, 6, 16, 10);
        g.DrawLine(pen, 16, 22, 16, 26);
        g.DrawLine(pen, 6, 16, 10, 16);
        g.DrawLine(pen, 22, 16, 26, 16);
        g.DrawLine(pen, 9, 9, 12, 12);
        g.DrawLine(pen, 20, 20, 23, 23);
        g.DrawLine(pen, 9, 23, 12, 20);
        g.DrawLine(pen, 20, 12, 23, 9);

        // 内圈填充
        using var innerBrush = new SolidBrush(Color.Orange);
        g.FillEllipse(innerBrush, 10, 10, 12, 12);

        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// 自定义时间轴面板。在 OnPaint 中绘制日出日落时间轴，
    /// 标示当前时间位置、白昼区间、日出日落时刻。
    /// </summary>
    private sealed class TimelinePanel : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            var w = Width - 40;
            var y = 15;
            var h = 6;
            var xStart = 20;

            // 背景轨道
            g.FillRectangle(Brushes.LightGray, xStart, y, w, h);

            // 获取日出日落数据
            var parent = FindForm() as MainForm;
            if (parent?._state.CurrentSolarInfo == null) return;

            var solar = parent._state.CurrentSolarInfo;
            var sunriseMin = solar.Sunrise.Hour * 60 + solar.Sunrise.Minute;
            var sunsetMin = solar.Sunset.Hour * 60 + solar.Sunset.Minute;
            var totalMin = 24 * 60;

            var sunriseX = xStart + (int)(w * sunriseMin / (double)totalMin);
            var sunsetX = xStart + (int)(w * sunsetMin / (double)totalMin);

            // 绘制白昼/黑夜区间
            var dayWidth = sunsetX - sunriseX;
            if (dayWidth > 0)
            {
                using var dayBrush = new LinearGradientBrush(
                    new Point(sunriseX, y), new Point(sunsetX, y),
                    Color.Gold, Color.DarkOrange);
                g.FillRectangle(dayBrush, sunriseX, y, dayWidth, h);

                if (sunriseX > xStart)
                    g.FillRectangle(Brushes.SlateGray,
                        xStart, y, sunriseX - xStart, h);
                if (sunsetX < xStart + w)
                    g.FillRectangle(Brushes.SlateGray,
                        sunsetX, y, xStart + w - sunsetX, h);
            }

            // 当前时间指示器（红色圆点）
            var now = DateTime.Now;
            var nowMin = now.Hour * 60 + now.Minute;
            var nowX = xStart + (int)(w * nowMin / (double)totalMin);
            g.FillEllipse(Brushes.Red, nowX - 4, y - 2, 10, 10);

            // 标注文字
            using var font = new Font("Microsoft YaHei UI", 8f);
            g.DrawString("00:00", font, Brushes.Gray, xStart, y + 10);
            g.DrawString("24:00", font, Brushes.Gray, xStart + w - 30, y + 10);
            g.DrawString($"{solar.Sunrise:HH:mm}", font, Brushes.DarkOrange,
                Math.Max(xStart, sunriseX - 25), y - 14);
            g.DrawString($"{solar.Sunset:HH:mm}", font, Brushes.DarkOrange,
                Math.Min(xStart + w - 40, sunsetX - 20), y - 14);
        }
    }
}
