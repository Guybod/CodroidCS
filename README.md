# CodroidCS

Codroid 机器人控制器的 **C# SDK**：通过 TCP/UDP 与 JSON 协议与控制器通信，支持寄存器、IO、运动、CRI 实时数据等能力。

## 环境要求

- [.NET SDK](https://dotnet.microsoft.com/download)：**推荐安装 8.x**（示例程序 `CodroidTest` 面向 `net8.0`）。
- 类库 `CodroidSDK` 同时编译 **net6.0** 与 **net8.0**，可按需在项目中引用对应目标框架。

本仓库为托管代码，**可在 Linux、Windows、macOS 上开发与运行**，无需单独做平台分支；仅需安装对应系统的 .NET SDK。

## 仓库结构

| 目录 / 项目 | 说明 |
|-------------|------|
| `CodroidSDK/` | SDK 类库（NuGet 包 id：`Codroidsdk`，构建时可生成 `.nupkg`） |
| `CodroidTest/` | 控制台示例程序，演示各类 API 用法 |

## 构建 SDK

```bash
dotnet build CodroidSDK/CodroidCS.csproj -c Release
```

生成的程序集在 `CodroidSDK/bin/Release/net6.0/` 与 `net8.0/`（若单独指定 `-f net8.0` 则仅该框架）。若启用 `GeneratePackageOnBuild`，会在输出目录生成 NuGet 包。

## 运行示例程序

示例默认连接的控制器 IP 在 `CodroidTest/Program.cs` 中的 `DefaultRobotIp`；也可通过命令行传入。

```bash
# 完整套件（默认）
dotnet run --project CodroidTest

# 指定控制器 IP
dotnet run --project CodroidTest -- 192.168.8.10

# 仅运行某一类演示（如 CRI、IO、寄存器等）
dotnet run --project CodroidTest -- cri 192.168.8.10
dotnet run --project CodroidTest -- io 192.168.8.10
dotnet run --project CodroidTest -- register 192.168.8.10
```

更多子命令与说明见 `CodroidTest/Program.cs` 文件顶部注释。

## 在自己的项目中引用

**方式一：项目引用（开发调试）**

```bash
dotnet add path/to/YourApp.csproj reference path/to/CodroidSDK/CodroidCS.csproj
```

**方式二：NuGet**

对打包生成的 `Codroidsdk.*.nupkg` 配置本地源或使用内部 NuGet 源后：

```bash
dotnet add package Codroidsdk
```

在代码中加入 `using Codroid;` 即可使用 SDK 类型。

## 仓库地址

<https://github.com/Guybod/CodroidCS>

## 许可证

本项目采用 [MIT License](LICENSE)。

---

运行示例前请确认本机网络可达机器人控制器，并根据现场修改 IP 与安全策略。
