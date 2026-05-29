<div align="center">
  <h1>朝夕·光色 <sub>SolarSync</sub></h1>
  <p><b>根据日出日落时间自动切换 Windows 浅色/深色模式</b></p>
  <p>Automatically switch Windows light/dark theme based on sunrise & sunset times</p>

  <p>
    <a href="#功能特性">功能特性</a> •
    <a href="#快速开始">快速开始</a> •
    <a href="#使用说明">使用说明</a> •
    <a href="#技术架构">技术架构</a> •
    <a href="#开发构建">开发构建</a> •
    <a href="#开源协议">开源协议</a>
  </p>

  <p>
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet" alt=".NET 8">
    <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=flat&logo=windows11" alt="Windows">
    <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License">
    <img src="https://img.shields.io/badge/version-1.0.3-blue" alt="Version 1.0.3">
  </p>
</div>

---

## 功能特性

- **🌐 自动定位** — 通过 `myip.ipip.net`（国内 API）获取公网 IP 及城市信息，无需手动配置
- **☀ 日出日落计算** — 基于 **NOAA 太阳位置算法**，根据经纬度精确计算当天日出日落时间（纯本地计算，零网络依赖）
- **🔄 自动主题切换** — 日出时自动切换为浅色模式，日落时自动切换为深色模式，无需手动干预
- **🎯 手动控制** — 支持一键切换浅色/深色模式，随时暂停/恢复自动切换
- **🖥 原生系统集成** — 通过 Windows 注册表 + DWM API 切换主题，效果与系统设置完全一致
- **📌 系统托盘常驻** — 关闭窗口自动隐藏到托盘，后台运行占用 < 20MB 内存
- **🌙 时间轴可视化** — 直观显示日出/日落时刻及当前时间的位置
- **📦 单文件发布** — 打包为单 EXE，无需安装，开箱即用
- **🚀 开机自启** — 支持一键设置开机自启（最小化到托盘启动）

## 快速开始

### 下载

从 [Releases 页面](https://github.com/JinkaiNiu/SolarSync/releases) 下载最新版本的 `SolarSync.exe`。

### 运行

```bash
SolarSync.exe            # 正常启动，显示主窗口
SolarSync.exe --hidden   # 启动后直接隐藏到系统托盘
```

> **提示**：首次运行需要网络连接以获取 IP 地理位置信息，之后会自动缓存结果，离线也可使用。

### 系统要求

- Windows 10 20H1 (2004) 或更高版本 / Windows 11
- [.NET 8 运行环境](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)（框架依赖版需要）
- 网络连接（首次 IP 定位需要）

## 使用说明

### 主界面

| 区域 | 说明 |
|------|------|
| 公网 IP | 显示当前公网 IP 地址，若使用缓存会标注"（缓存）" |
| 所在城市 | IP 对应的省份和城市 |
| 日出/日落 | 当天的日出和日落时间 |
| 当前主题 | 显示当前主题状态和自动/手动模式 |
| 时间轴 | 直观展示日出/日落时刻及当前时间位置 |
| 数据来源 | 各模块的数据来源说明 |

### 操作按钮

| 按钮 | 功能 |
|------|------|
| 刷新 | 重新获取 IP 定位并计算日出日落 |
| 浅色/深色模式 | 手动切换主题（自动模式暂停） |
| 自动切换中 | 恢复自动切换模式 |

### 系统托盘

- **双击图标** — 显示/隐藏主窗口
- **右键菜单**：
  - 显示/隐藏窗口
  - 强制浅色/深色模式
  - 自动切换
  - 刷新数据
  - 开机自启
  - 关于
  - 退出

## 技术架构

```
SolarSync/
├── Program.cs                  # 程序入口（单实例 Mutex）
├── MainForm.cs                 # 主窗口 + 系统托盘 + 时间轴绘制
├── SolarSync.csproj            # 项目配置
│
├── Models/
│   ├── IpInfo.cs               # IP 信息模型
│   ├── SolarInfo.cs            # 日出日落信息模型
│   ├── CityCoordEntry.cs       # 城市坐标条目
│   └── CityCoordDb.cs          # 城市坐标数据库容器
│
├── Services/
│   ├── IpLocationService.cs    # IP 定位服务（myip.ipip.net + 本地缓存）
│   ├── SolarCalculator.cs      # NOAA 日出日落算法
│   ├── ThemeService.cs         # Windows 主题切换（注册表 + DWM API）
│   └── AppStateManager.cs      # 全局状态管理 & 定时调度
│
└── Resources/
    ├── app.ico                 # 应用程序图标
    └── city_coords.json        # 350+ 中国城市经纬度数据库
```

### 技术栈

| 组件 | 选型 | 说明 |
|------|------|------|
| 语言 | C# 12 | .NET 平台 |
| 运行时 | .NET 8 LTS | 长期支持版本 |
| UI 框架 | Windows Forms | 原生 Win32 封装，最轻量 |
| IP 查询 | `myip.ipip.net` | 国内 API，免费无需 Key |
| 日出日落 | NOAA 算法（自实现） | 纯数学计算，零外部依赖 |
| 主题切换 | Registry + `DwmSetWindowAttribute` | 纯原生接口，无需第三方库 |
| 后台运行 | NotifyIcon + Timer | 系统托盘常驻 |

### 依赖关系

- **零第三方 NuGet 包**：全部功能仅依赖 .NET BCL（基类库）和 Windows Forms
- **零外部 API Key**：所有服务均为免费、无需注册

## 开发构建

### 环境准备

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
2. 克隆仓库：

```bash
git clone https://github.com/JinkaiNiu/SolarSync.git
cd SolarSync
```

### 构建调试版

```bash
dotnet build -c Release
```

### 发布单文件 EXE

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

输出路径：`bin/Release/net8.0-windows/win-x64/publish/SolarSync.exe`

### 发布框架依赖版（体积更小）

```bash
dotnet publish -c Release -r win-x64
```

输出路径：`bin/Release/net8.0-windows/win-x64/publish/SolarSync.exe`（约 65KB，需安装 .NET 8 运行时）

## 数据来源

| 数据 | 来源 |
|------|------|
| IP 地理位置 | [myip.ipip.net](https://myip.ipip.net) |
| 城市坐标 | 内置数据库 (350+ 中国城市) |
| 日出日落算法 | [NOAA 太阳位置算法](https://gml.noaa.gov/grad/solcalc/) |

## 开源协议

本项目基于 [MIT 许可证](LICENSE) 开源。

---

<div align="center">
  <p>Made with ❤️ by <a href="https://kaneniu.com">JinkaiNiu</a></p>
</div>
