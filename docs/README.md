# CodroidCS SDK Documentation / CodroidCS SDK 文档

**Version / 版本:** 2.1.7 | **Namespace / 命名空间:** `Codroid`

---

## Table of Contents / 目录

| # | Document / 文档 | Description / 说明 |
|---|----------------|-------------------|
| 1 | [Quick Start / 快速上手](01-quick-start.md) | Install, connect, and run your first program / 安装、连接并运行第一个程序 |
| 2 | [Core Concepts / 核心概念](02-concepts.md) | Lifecycle, TCP model, units, exceptions / 生命周期、TCP 模型、单位约定、异常处理 |
| 3 | [CodroidClient API](03-api-reference-codroidclient.md) | Complete API reference for CodroidClient / CodroidClient 完整 API 参考 |
| 4 | [Motion Types / 运动类型](04-api-reference-motion.md) | JointPoint, CartesianPoint, MoveInstruction, MotionWaitOptions, enums |
| 5 | [Data Types & Enums / 数据类型与枚举](05-api-reference-types.md) | CommonResponse, CriRealTimeData, RobotFrame, RegisterReadValue, exceptions |
| 6 | [CRI Real-Time / CRI 实时](06-api-reference-cri.md) | CriRealtimeDispatcher, TrajectoryGenerator, TrajectoryRequest, PacketParser |
| 7 | [IO & Register / IO 与寄存器](07-api-reference-io-register.md) | DI/DO/AI/AO operations, register read/write |
| 8 | [Utilities / 辅助工具](08-api-reference-utilities.md) | Publish/Subscribe, GlobalVariables, Kinematics, ConsoleUtf8 |
| 9 | [.NET Framework 4.6.2 Notes](09-net462-notes.md) | net462 platform constraints, 250Hz SLA, polyfills |

---

## Environment Requirements / 环境要求

| Target / 目标框架 | Platform / 平台 | Notes / 说明 |
|-------------------|----------------|-------------|
| `net6.0` | Linux, Windows, macOS | .NET 6 SDK |
| `net8.0` | Linux, Windows, macOS | .NET 8 SDK (recommended / 推荐) |
| `net462` | **Windows only** | .NET Framework 4.6.2+, WinForms/WPF compatible |

### Install via NuGet / NuGet 安装

```bash
dotnet add package Codroidsdk
```

### Project Reference / 项目引用

```bash
dotnet add path/to/YourApp.csproj reference path/to/CodroidSDK/CodroidCS.csproj
```

---

## API Naming Convention / API 命名约定

All public methods return `Task` / `Task<T>` but do **not** use the `Async` suffix.

所有公共方法返回 `Task` / `Task<T>`，但**不**使用 `Async` 后缀。

```csharp
// Correct / 正确
await robot.ConnectRemoteAndSwitchOn();
int di = await robot.GetDi(0);

// Wrong / 错误
await robot.ConnectRemoteAndSwitchOnAsync(); // does not exist / 不存在
```

This keeps the same public API names across C#, Python, and C++ SDKs.

这样做是为了让 C# / Python / C++ 三套 SDK 使用同一套公开函数名。

---

## Unit Convention / 单位约定

| Layer / 层级 | Linear / 线性 | Angular / 角度 |
|-------------|--------------|---------------|
| SDK public API | **mm** | **deg (degrees)** |
| TCP JSON protocol | **mm** | **deg** |
| CRI UDP binary (wire) | **m** | **rad (radians)** |
| `CriRealTimeData` (parsed) | **mm** | **deg** |

`CriRealtimePacketParser.Parse()` and `CriRealtimeDispatcher` (with `convertToSi=true`) handle the m↔mm and rad↔deg conversion automatically.

`CriRealtimePacketParser.Parse()` 和 `CriRealtimeDispatcher`（`convertToSi=true`）会自动处理 m↔mm 和 rad↔deg 的换算。
