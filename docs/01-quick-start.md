# Quick Start / 快速上手

## Install / 安装

### NuGet

```bash
dotnet add package Codroidsdk
```

### Project Reference / 项目引用

```bash
dotnet add path/to/YourApp.csproj reference path/to/CodroidSDK/CodroidCS.csproj
```

### Supported Targets / 支持的目标框架

```xml
<!-- net8.0 (recommended) -->
<TargetFramework>net8.0</TargetFramework>

<!-- net6.0 -->
<TargetFramework>net6.0</TargetFramework>

<!-- .NET Framework 4.6.2+ (Windows only) -->
<TargetFramework>net462</TargetFramework>
```

---

## Minimal Example / 最小示例

Connect to the controller, read a digital input, write a digital output, and disconnect.

连接控制器，读取数字输入，写入数字输出，然后断开。

```csharp
using Codroid;

var robot = new CodroidClient("192.168.8.136");

try
{
    // Connect, enter remote mode, and power on / 连接、切换远程、上电
    await robot.ConnectRemoteAndSwitchOn();

    // Read DI port 0 / 读取 DI 端口 0
    int di0 = await robot.GetDi(0);
    Console.WriteLine($"DI 0 = {di0}");

    // Write DI value to DO port 10 / 将 DI 值写入 DO 端口 10
    await robot.SetDo(10, di0);
}
finally
{
    // Always disconnect in finally / 始终在 finally 中断开
    robot.Disconnect();
}
```

---

## With CRI Real-time Data / 使用 CRI 实时数据

If you need CRI real-time data (for sync motion, state monitoring, etc.), start CRI push after connecting.

如果需要 CRI 实时数据（用于阻塞运动、状态监控等），连接后启动 CRI 推送。

```csharp
using Codroid;

var robot = new CodroidClient("192.168.8.136");

try
{
    // 1. Connect / 连接
    await robot.ConnectRemoteAndSwitchOn();

    // 2. ⚠️ Start CRI data push (required for sync motion) / 启动 CRI 数据推送（阻塞运动必需）
    await robot.StartCriDataPush("192.168.8.150", 18888);

    // 3. Wait for first CRI frame / 等待首帧 CRI 数据
    await robot.WaitForCriData(5.0);

    // 4. Now you can use sync motion / 现在可以使用阻塞运动
    robot.MovJSync(JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 }), speed: 40, acc: 100);
}
finally
{
    robot.Disconnect();
}
```

---

## Complete Workflow Example / 完整工作流示例

```csharp
using Codroid;

ConsoleUtf8.InitConsoleUtf8(); // Windows console UTF-8 / Windows 控制台 UTF-8

var robot = new CodroidClient("192.168.8.136");

try
{
    // 1. Connect / 连接
    await robot.ConnectRemoteAndSwitchOn();

    // 2. Start CRI data push (recommended after connect) / 启动 CRI 数据推送（推荐连接后立即调用）
    await robot.StartCriDataPush("192.168.8.150", 18888);
    await robot.WaitForCriData(5.0);

    // 3. IO / IO 操作
    int di0 = await robot.GetDi(0);
    await robot.SetDo(10, di0);

    // 4. Register / 寄存器
    RegisterReadValue reg = await robot.GetRegisterValue(49100);
    int value = reg.GetInt32();
    await robot.SetRegisterValue(49100, value + 1);

    // 5. Motion / 运动
    await robot.MovJ(JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 }), speed: 40, acc: 100);

    // 6. Blocking motion / 阻塞运动
    robot.MovLSync(
        CartesianPoint.MmDegWithRef(new[] { 400, 0, 300, 180, 0, 0 }, robot.CriData.JointPosition),
        speed: 150, acc: 500);
}
finally
{
    robot.Disconnect();
}
```

---

## Run Example Projects / 运行示例项目

```bash
# net8.0 (full suite / 完整套件)
dotnet run --project CodroidTestNet8/CodroidTestNet8.csproj

# With controller IP / 指定控制器 IP
dotnet run --project CodroidTestNet8/CodroidTestNet8.csproj -- 192.168.8.10

# Specific demo / 仅运行某一类演示
dotnet run --project CodroidTestNet8/CodroidTestNet8.csproj -- cri 192.168.8.10
dotnet run --project CodroidTestNet8/CodroidTestNet8.csproj -- io 192.168.8.10
dotnet run --project CodroidTestNet8/CodroidTestNet8.csproj -- register 192.168.8.10

# net462 / .NET Framework 4.6.2
dotnet run --project CodroidTestNet462/CodroidTestNet462.csproj -- 192.168.8.10
```

---

## Error Handling / 错误处理

All TCP commands throw on failure:

所有 TCP 指令在失败时抛出异常：

| Exception / 异常 | Condition / 条件 |
|-----------------|-----------------|
| `CodroidCommandException` | Controller returns `err` / 控制器返回 `err` |
| `TimeoutException` | No response within 10 seconds / 10 秒内未收到响应 |
| `ArgumentException` | Invalid parameter (SDK-side validation) / 参数无效（SDK 侧校验） |

```csharp
try
{
    await robot.SetDo(999, 1); // Invalid port / 无效端口
}
catch (CodroidCommandException ex)
{
    Console.WriteLine($"Controller error: {ex.ControllerError}");
}
catch (TimeoutException)
{
    Console.WriteLine("Request timed out / 请求超时");
}
```
