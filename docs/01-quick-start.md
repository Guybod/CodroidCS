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

## Standard Connection Pattern / 标准连接写法

**Recommended: Always call `StartCriDataPush` + `WaitForCriData` after `ConnectRemoteAndSwitchOn`.**

**推荐：连接后立即调用 `StartCriDataPush` + `WaitForCriData`。**

```csharp
using Codroid;

var robot = new CodroidClient("192.168.8.136");

try
{
    // Standard connection pattern / 标准连接写法
    await robot.ConnectRemoteAndSwitchOn();
    await robot.StartCriDataPush("192.168.8.150", 18888);
    await robot.WaitForCriData(5.0);

    // Now you can use all APIs / 现在可以使用所有 API
    robot.MovJSync(JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 }), speed: 40, acc: 100);
}
finally
{
    robot.Disconnect();
}
```

**Why this pattern? / 为什么这个写法？**

- `StartCriDataPush` enables real-time state monitoring / 启用实时状态监控
- `WaitForCriData` ensures CRI data is flowing before any motion / 确保 CRI 数据在运动前已就绪
- Required for `*Sync` blocking motion APIs / `*Sync` 阻塞运动 API 必需
- Recommended for all use cases / 推荐所有场景使用

---

## Complete Workflow Example / 完整工作流示例

```csharp
using Codroid;

ConsoleUtf8.InitConsoleUtf8(); // Windows console UTF-8 / Windows 控制台 UTF-8

var robot = new CodroidClient("192.168.8.136");

try
{
    // 1. Standard connection pattern / 标准连接写法
    await robot.ConnectRemoteAndSwitchOn();
    await robot.StartCriDataPush("192.168.8.150", 18888);
    await robot.WaitForCriData(5.0);

    // 2. IO / IO 操作
    int di0 = await robot.GetDi(0);
    await robot.SetDo(10, di0);

    // 3. Register / 寄存器
    RegisterReadValue reg = await robot.GetRegisterValue(49100);
    int value = reg.GetInt32();
    await robot.SetRegisterValue(49100, value + 1);

    // 4. Motion / 运动
    await robot.MovJ(JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 }), speed: 40, acc: 100);

    // 5. Blocking motion / 阻塞运动
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
