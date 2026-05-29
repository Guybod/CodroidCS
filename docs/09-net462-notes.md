# .NET Framework 4.6.2 Notes / .NET Framework 4.6.2 特别说明

This document covers the platform constraints, timing behavior, polyfill layer, and build considerations when using the Codroid SDK on .NET Framework 4.6.2.

本文档涵盖在 .NET Framework 4.6.2 上使用 Codroid SDK 时的平台限制、定时行为、兼容层和构建注意事项。

---

## 1. Platform Constraint / 平台限制

.NET Framework 4.6.2 runs only on **Windows**. Linux and macOS are not supported.

.NET Framework 4.6.2 仅在 **Windows** 上运行。不支持 Linux 和 macOS。

```xml
<!-- Only valid on Windows / 仅在 Windows 上有效 -->
<TargetFramework>net462</TargetFramework>
```

---

## 2. Target Framework / 目标框架

The SDK uses multi-targeting. The `net462` target is built alongside `net6.0` and `net8.0`.

SDK 采用多目标构建。`net462` 目标与 `net6.0` 和 `net8.0` 一同构建。

```xml
<!-- From CodroidCS.csproj / 摘自 CodroidCS.csproj -->
<TargetFrameworks>net462;net6.0;net8.0</TargetFrameworks>
```

Your test project should reference the SDK and target `net462`:

你的测试项目应引用 SDK 并以 `net462` 为目标：

```xml
<PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net462</TargetFramework>
    <LangVersion>10</LangVersion>
</PropertyGroup>

<ItemGroup>
    <ProjectReference Include="..\CodroidSDK\CodroidCS.csproj" />
</ItemGroup>
```

---

## 3. CRI 250Hz SLA / CRI 250Hz 服务等级

The default and supported CRI real-time control frequency is **250Hz** (`periodMs = 4`).

默认且受支持的 CRI 实时控制频率为 **250Hz**（`periodMs = 4`）。

- `periodMs = 4` is the **default SLA** -- tested and supported out of the box.
  `periodMs = 4` 是**默认服务等级** -- 即开即用、经过测试。
- `periodMs != 4` (including 500Hz / 1000Hz) is **NOT** within the default SLA. Higher frequencies require **on-site validation** of jitter and controller behavior.
  `periodMs != 4`（包括 500Hz / 1000Hz）**不在**默认服务等级内。更高频率需要**现场验证**抖动和控制器行为。

When `periodMs != 4`, the SDK logs a warning via `Trace.TraceWarning`:

当 `periodMs != 4` 时，SDK 通过 `Trace.TraceWarning` 输出警告：

```
[Codroidsdk][net462] CRI SendTrajectory periodMs=N is outside the default 250Hz SLA.
The default supported period is 4ms. Higher-frequency or custom-period control
requires on-site validation.
```

---

## 4. Timing Implementation / 定时实现

On .NET 6+, `SendTrajectory` uses `PeriodicTimer`. On net462, `PeriodicTimer` is unavailable. The SDK falls back to:

在 .NET 6+ 上，`SendTrajectory` 使用 `PeriodicTimer`。在 net462 上，`PeriodicTimer` 不可用。SDK 回退到：

1. **`Stopwatch`** -- high-resolution elapsed time measurement.
   **`Stopwatch`** -- 高分辨率耗时测量。

2. **`Thread.Sleep(1)`** -- for remaining time > 1.5ms (yields CPU).
   **`Thread.Sleep(1)`** -- 剩余时间 > 1.5ms 时使用（让出 CPU）。

3. **`Thread.SpinWait(50)`** -- for remaining time <= 1.5ms (busy-wait for precision).
   **`Thread.SpinWait(50)`** -- 剩余时间 <= 1.5ms 时使用（忙等待以保精度）。

```csharp
// Simplified net462 timing loop / 简化的 net462 定时循环
long ticksPerPeriod = (long)(Stopwatch.Frequency * periodMs / 1000.0);
long nextTick = stopwatch.ElapsedTicks;

foreach (var point in trajectory)
{
    await SendCommand(point.Position, space, ct);
    nextTick += ticksPerPeriod;
    long remainingTicks = nextTick - stopwatch.ElapsedTicks;

    if (remainingTicks > 0)
    {
        double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
        if (remainingMs > 1.5)
            Thread.Sleep(1);       // coarse wait / 粗等待
        else
            Thread.SpinWait(50);   // fine wait / 细等待
    }
}
```

---

## 5. Jitter Statistics / 抖动统计

After `SendTrajectory` completes on net462, the SDK outputs jitter statistics via `Trace.TraceInformation`.

在 net462 上 `SendTrajectory` 完成后，SDK 通过 `Trace.TraceInformation` 输出抖动统计。

```
[Codroidsdk][net462] CRI SendTrajectory statistics:
  Duration: 4.12s
  Frames sent: 1031
  Average period: 4.005ms
  Max period: 5.823ms
  Overruns (>6ms): 0
  Max consecutive overruns: 0
  UDP exceptions: 0
```

| Metric / 指标 | Description / 说明 |
|---|---|
| `Duration` | Total elapsed time / 总耗时 |
| `Frames sent` | Number of UDP frames sent / 发送的 UDP 帧数 |
| `Average period` | Mean inter-frame interval / 平均帧间隔 |
| `Max period` | Worst-case inter-frame interval / 最大帧间隔 |
| `Overruns (>6ms)` | Frames exceeding 6ms interval / 超过 6ms 间隔的帧数 |
| `Max consecutive overruns` | Longest streak of consecutive overruns / 最长连续超限次数 |
| `UDP exceptions` | Socket errors during send / 发送期间的 Socket 异常 |

---

## 6. TraceWarning for periodMs != 4 / periodMs != 4 的警告

If you call `SendTrajectory` with any `periodMs` value other than 4, the SDK emits a `Trace.TraceWarning` before starting. This is informational only -- execution proceeds normally.

如果以非 4 的 `periodMs` 值调用 `SendTrajectory`，SDK 会在开始前发出 `Trace.TraceWarning`。这仅作提示 -- 程序正常执行。

---

## 7. Polyfill Layer / 兼容层

Five polyfill files provide missing .NET 6+ APIs when targeting net462. All are in `CodroidSDK/Compat/`.

五个兼容文件在目标为 net462 时提供 .NET 6+ 缺失的 API。全部位于 `CodroidSDK/Compat/`。

### 7.1 ArgumentNullException.ThrowIfNull

**File / 文件:** `Polyfills.cs`

On net462, `ArgumentNullException.ThrowIfNull` does not exist. The SDK provides `Polyfills.ThrowIfNull` with `CallerArgumentExpression` for automatic parameter name capture.

在 net462 上，`ArgumentNullException.ThrowIfNull` 不存在。SDK 提供 `Polyfills.ThrowIfNull`，使用 `CallerArgumentExpression` 自动捕获参数名。

```csharp
// Internal usage / 内部使用
Polyfills.ThrowIfNull(argument); // auto-captures param name / 自动捕获参数名

// On net6+ this delegates to ArgumentNullException.ThrowIfNull
// 在 net6+ 上委托给 ArgumentNullException.ThrowIfNull
```

### 7.2 Math.Clamp

**File / 文件:** `MathPolyfills.cs`

`Math.Clamp` is not available in .NET Framework. The SDK provides `MathPolyfills.Clamp` with identical semantics.

`Math.Clamp` 在 .NET Framework 中不可用。SDK 提供语义相同的 `MathPolyfills.Clamp`。

```csharp
// int overload / int 重载
int clamped = MathPolyfills.Clamp(value, min, max);

// double overload / double 重载
double clampedD = MathPolyfills.Clamp(value, 0.0, 1.0);
```

### 7.3 double.IsFinite

**File / 文件:** `DoublePolyfills.cs`

`double.IsFinite` is not available in .NET Framework. The SDK provides `DoublePolyfills.IsFinite`.

`double.IsFinite` 在 .NET Framework 中不可用。SDK 提供 `DoublePolyfills.IsFinite`。

```csharp
bool ok = DoublePolyfills.IsFinite(d); // true if not NaN and not Infinity / 非 NaN 且非 Infinity 时为 true
```

### 7.4 IsExternalInit

**File / 文件:** `IsExternalInit.cs`

The `init` accessor keyword requires `System.Runtime.CompilerServices.IsExternalInit`, which does not exist in net462. This polyfill adds the marker type.

`init` 访问器关键字需要 `System.Runtime.CompilerServices.IsExternalInit`，在 net462 中不存在。此兼容文件添加该标记类型。

```csharp
// Enables C# init properties on net462 / 在 net462 上启用 C# init 属性
public string Name { get; init; } = "";
```

### 7.5 CallerArgumentExpressionAttribute

**File / 文件:** `CallerArgumentExpressionAttribute.cs`

The `[CallerArgumentExpression]` attribute is a C# 10 / .NET 6+ feature. This polyfill declares the attribute for net462, enabling `Polyfills.ThrowIfNull` to capture parameter names automatically.

`[CallerArgumentExpression]` 属性是 C# 10 / .NET 6+ 特性。此兼容文件在 net462 上声明该属性，使 `Polyfills.ThrowIfNull` 能自动捕获参数名。

---

## 8. NuGet Dependencies / NuGet 依赖

When targeting net462, the SDK pulls in two additional NuGet packages:

当目标为 net462 时，SDK 引入两个额外的 NuGet 包：

| Package / 包 | Version / 版本 | Purpose / 用途 |
|---|---|---|
| `System.Text.Json` | 8.0.5 | JSON serialization / JSON 序列化 |
| `System.Memory` | 4.5.5 | `Span<T>`, `Memory<T>` support / `Span<T>`、`Memory<T>` 支持 |

```xml
<!-- From CodroidCS.csproj / 摘自 CodroidCS.csproj -->
<ItemGroup Condition="'$(TargetFramework)' == 'net462'">
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
    <PackageReference Include="System.Memory" Version="4.5.5" />
</ItemGroup>
```

---

## 9. UdpClient Differences / UdpClient 差异

The `UdpClient` API differs between .NET Framework and .NET 6+. The SDK uses conditional compilation to handle this.

`UdpClient` API 在 .NET Framework 和 .NET 6+ 之间存在差异。SDK 使用条件编译处理。

### SendAsync

On net462, `UdpClient.SendAsync` is not available with `ReadOnlyMemory<byte>`. The SDK uses the synchronous `Send` method instead.

在 net462 上，`UdpClient.SendAsync` 不支持 `ReadOnlyMemory<byte>`。SDK 改用同步 `Send` 方法。

```csharp
#if NET462
    _udp.Send(buffer, length, target);       // synchronous / 同步
    await Task.CompletedTask;
#else
    await _udp.SendAsync(buffer.AsMemory(), target, ct);  // async / 异步
#endif
```

### ReceiveAsync with CancellationToken

On net462, `ReceiveAsync(CancellationToken)` is not available. The SDK registers a cancellation callback that closes the socket, causing the blocking `ReceiveAsync()` to throw.

在 net462 上，`ReceiveAsync(CancellationToken)` 不可用。SDK 注册一个取消回调来关闭套接字，使阻塞的 `ReceiveAsync()` 抛出异常。

```csharp
#if NET462
    using var reg = token.Register(() =>
    {
        try { _udpClient?.Close(); } catch { }
    });
    // Then: await _udpClient.ReceiveAsync() (no token)
    // 然后：await _udpClient.ReceiveAsync()（无 token）
#else
    await _udpClient.ReceiveAsync(token);
#endif
```

### WriteDoubleLittleEndian

On net462, `BinaryPrimitives.WriteDoubleLittleEndian(Span<byte>, double)` is not available. The SDK falls back to `BitConverter.GetBytes` with manual endianness handling.

在 net462 上，`BinaryPrimitives.WriteDoubleLittleEndian(Span<byte>, double)` 不可用。SDK 回退到 `BitConverter.GetBytes` 并手动处理字节序。

```csharp
#if NET462
    byte[] bytes = BitConverter.GetBytes(value);
    if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
    Array.Copy(bytes, 0, buffer, offset, 8);
#else
    BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(offset, 8), value);
#endif
```

---

## 10. LangVersion = 10 / 语言版本 = 10

Both the SDK and net462 test projects set `<LangVersion>10</LangVersion>`. This enables C# 10 features (file-scoped namespaces, `init`, global usings, etc.) even on net462.

SDK 和 net462 测试项目均设置 `<LangVersion>10</LangVersion>`。这使得即使在 net462 上也能使用 C# 10 特性（文件范围命名空间、`init`、全局 using 等）。

```xml
<PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <LangVersion>10</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

---

## 11. Running the net462 Test Projects / 运行 net462 测试项目

### Basic API Test / 基本 API 测试

```bash
# Full suite (IO, register, kinematics, publish, global vars) / 完整套件
dotnet run --project CodroidTestNet462/CodroidTestNet462.csproj -- 192.168.8.10

# Single IO test / 单项 IO 测试
dotnet run --project CodroidTestNet462/CodroidTestNet462.csproj -- io 192.168.8.10

# Single register test / 单项寄存器测试
dotnet run --project CodroidTestNet462/CodroidTestNet462.csproj -- register 192.168.8.10
```

### CRI Real-Time Control Test / CRI 实时控制测试

```bash
# All segments (joint + cartesian + path) / 全部段（关节 + 笛卡尔 + 路径）
dotnet run --project CodroidCRITestNet462/CodroidCRITestNet462.csproj

# Joint only / 仅关节
dotnet run --project CodroidCRITestNet462/CodroidCRITestNet462.csproj -- joint

# Cartesian with custom speed / 自定义速度的笛卡尔
dotnet run --project CodroidCRITestNet462/CodroidCRITestNet462.csproj -- cart --speed 120 --accel 600

# Path with specific IPs / 指定 IP 的路径段
dotnet run --project CodroidCRITestNet462/CodroidCRITestNet462.csproj -- path 192.168.8.10 192.168.8.150

# Duration mode (6 seconds) / 时长模式（6 秒）
dotnet run --project CodroidCRITestNet462/CodroidCRITestNet462.csproj -- cart --duration 6
```

---

## Summary of net462 vs net6.0+ Differences / net462 与 net6.0+ 差异总结

| Aspect / 方面 | net462 | net6.0+ |
|---|---|---|
| Platform / 平台 | Windows only / 仅 Windows | Windows, Linux, macOS |
| CRI timer / CRI 定时器 | `Stopwatch` + `Thread.Sleep(1)` + `SpinWait(50)` | `PeriodicTimer` |
| UDP send / UDP 发送 | `UdpClient.Send` (sync) | `UdpClient.SendAsync` (async) |
| UDP receive cancellation / UDP 接收取消 | `token.Register(Close)` | `ReceiveAsync(token)` |
| Double write / double 写入 | `BitConverter.GetBytes` + manual endian | `BinaryPrimitives.WriteDoubleLittleEndian` |
| `Math.Clamp` | `MathPolyfills.Clamp` | `Math.Clamp` |
| `double.IsFinite` | `DoublePolyfills.IsFinite` | `double.IsFinite` |
| `init` accessor | Polyfill (`IsExternalInit.cs`) | Built-in / 内置 |
| `[CallerArgumentExpression]` | Polyfill | Built-in / 内置 |
| NuGet extras / 额外 NuGet | `System.Text.Json 8.0.5`, `System.Memory 4.5.5` | None / 无 |
