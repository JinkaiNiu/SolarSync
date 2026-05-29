# 朝夕·光色（SolarSync）— 项目记忆文档

## 概述

SolarSync 是一款 Windows 桌面工具，根据用户所在地的日出日落时间自动切换系统浅色/深色模式。基于 C# 12 / .NET 8 / Windows Forms 构建，零外部依赖，发布为单文件自包含 EXE。

---

## 技术栈

| 层级       | 技术                                              |
| ---------- | ------------------------------------------------- |
| 语言       | C# 12                                             |
| 运行时     | .NET 8，目标 `net8.0-windows`                    |
| UI 框架    | Windows Forms                                     |
| 输出格式   | 单文件自包含 EXE（`win-x64`），已启用 IL 裁剪     |
| 外部依赖   | 无（无 NuGet 包，仅 .NET BCL + Windows Forms）     |

---

## 项目结构

```
SolarSync/
├── Program.cs                  # 入口：单实例 Mutex，--hidden 参数
├── MainForm.cs                 # 主窗口（460×420）+ 系统托盘 + 时间线绘制
├── SolarSync.csproj            # 项目配置
├── README.md                   # 中文说明文档
├── CHANGELOG.md                # 版本更新日志
├── PROJECT.md                  # 本文件
├── Models/
│   ├── IpInfo.cs               # IP 信息模型
│   ├── SolarInfo.cs            # 日出日落数据模型
│   ├── CityCoordEntry.cs       # 城市坐标记录
│   └── CityCoordDb.cs          # 城市数据库根容器
├── Services/
│   ├── IpLocationService.cs    # IP 定位（HTTP + 文本解析 + 坐标匹配 + 缓存）
│   ├── SolarCalculator.cs      # NOAA 太阳位置算法（纯数学）
│   ├── ThemeService.cs         # Windows 主题切换（注册表 + DWM + 广播）
│   └── AppStateManager.cs      # 状态协调器（定位→计算→定时切换→启动自应用）
└── Resources/
    ├── app.ico                 # 程序图标
    └── city_coords.json        # 嵌入资源：350+ 中国城市经纬度
```

---

## 核心数据流

```
启动
  │
  ├─1─ IpLocationService.GetLocationAsync()
  │     ├── HTTP GET myip.ipip.net
  │     ├── 文本解析 → 提取 IP、省份、城市
  │     └── 匹配内置 city_coords.json → 获取 lat/lng
  │
  ├─2─ SolarCalculator.Calculate(lat, lng, date)
  │     └── NOAA 算法 → SolarInfo { Sunrise, Sunset }
  │
  ├─3─ AppStateManager 应用当前主题
  │     └── 白昼 → 浅色 / 夜间 → 深色
  │
  └─4─ 调度定时器
        ├── _switchTimer：在下一次日出/日落时触发主题切换
        └── _dailyTimer：每日 00:05 刷新数据
```

---

## 关键设计决策

### 1. 零外部依赖
- 不使用 Microsoft.Windows.CsWin32，手动 P/Invoke DWM API
- 不使用任何 JSON / HTTP / 定时器 NuGet 包，全用 BCL
- 发布体积更小、无供应链风险

### 2. IP 定位策略
- 优先使用 myip.ipip.net（国内免 API Key）
- 城市级别精度足够日出日落计算
- 本地缓存到 `%LOCALAPPDATA%\SolarSync\ip_cache.json`，断网时可用

### 3. NOAA 算法自实现
- 13 步完整计算，精度 ~±1 分钟
- 大气折射修正（天顶角 90.833°）
- 默认 UTC+8（中国标准时间）

### 4. 主题切换方式
- 写入 `AppsUseLightTheme` + `SystemUsesLightTheme` 注册表键
- 通过 `DwmSetWindowAttribute` （DWMWA_USE_IMMERSIVE_DARK_MODE = 20）设置标题栏
- `SendMessageTimeout` 广播 `WM_SETTINGCHANGE` 通知全系统刷新
- 耗时广播操作放入 `Task.Run` 后台线程，避免 UI 冻结

### 5. 启动时自动应用
- v1.0.1 新增：`InitializeAsync()` 完成后立即判断当前时间并应用正确主题
- 无需等待下一次定时器触发

### 6. 定时器架构与安全
- `System.Threading.Timer` 用于主题切换和每日刷新（不依赖 UI 线程）
- `System.Windows.Forms.Timer` 每 60 秒刷新 UI 显示
- `System.Threading.Timer` 回调在 ThreadPool 线程运行，必须避免依赖 UI 同步上下文
- `PerformScheduledSwitch()` 使用 `TaskScheduler.Default` + MainForm 自有 `InvokeRequired` 跨线程调度，替代 `FromCurrentSynchronizationContext()`
- 所有定时器回调包裹 `try-catch` 防止未捕获异常导致进程退出
- `CancellationTokenSource` 防止异步操作竞态

### 7. 定时器安全兜底（v1.0.3）
- 主题切换使用"精确定时 + 定期校验"双保险：
  - `_switchTimer`：一个一次性 `System.Threading.Timer`，根据日出/日落时间精确调度
  - `_syncTimer`：一个周期性 `System.Threading.Timer`，每 60 秒检查当前主题是否与时段匹配
- `ScheduleNextSwitch()` 使用 `TimeOfDay` 相对今日日期计算，消除陈旧数据导致的负延迟
- 最小延迟设为 1 秒，防止亚毫秒截断为 0 导致定时器立即重入
- `RefreshAsync()` 末尾自动续约每日刷新，确保跨日后 `CurrentSolarInfo` 持续更新

### 8. 单实例保证
- 使用命名 `Mutex`（`"SolarSync-SingleInstance-Mutex"`），防止多开
- 第二个实例弹出提示后退出

---

## 构建与发布

```bash
# Debug 构建
dotnet build

# 单文件自包含发布（推荐）
dotnet publish -c Release -r win-x64 --self-contained true
# 输出：bin/Release/net8.0-windows/win-x64/publish/SolarSync.exe
```

---

## 运行

```bash
SolarSync.exe            # 正常启动，窗口可见
SolarSync.exe --hidden   # 启动后隐藏到系统托盘
```

---

## 系统要求

- Windows 10 20H1+ / Windows 11（支持沉浸式深色模式）
- 无需安装 .NET 运行时（自包含发布）

---

## 许可证

MIT License | Copyright © 2026 JinkaiNiu
