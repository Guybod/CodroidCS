# Core Concepts / 核心概念

## CodroidClient Lifecycle / CodroidClient 生命周期

```
new CodroidClient(ip)
        │
        ▼
   Connect()  ──or──  ConnectRemoteAndSwitchOn()
        │
        ▼
   [ IO / Register / Motion / CRI ... ]
        │
        ▼
    Disconnect()
```

```csharp
var robot = new CodroidClient("192.168.8.136");

try
{
    await robot.ConnectRemoteAndSwitchOn();
    // ... use robot ...
}
finally
{
    robot.Disconnect(); // Always call in finally / 始终在 finally 中调用
}
```

### Constructor / 构造函数

```csharp
var robot = new CodroidClient(string ip);
```

- `ip` — Controller IP address / 控制器 IP 地址
- TCP port is fixed at **9001** / TCP 端口固定为 **9001**

### Properties / 属性

| Property | Type | Description / 说明 |
|----------|------|-------------------|
| `CriData` | `CriRealTimeData` | Thread-safe clone of CRI data snapshot / CRI 数据快照的线程安全副本 |
| `Data` | `CriRealTimeData` | Direct reference to internal CRI buffer (faster, not thread-safe) / 内部 CRI 缓冲区的直接引用（更快，非线程安全） |

### Event / 事件

```csharp
robot.CriDataReceived += data =>
{
    Console.WriteLine($"Joints: {string.Join(", ", data.JointPosition)}");
};
```

Fires after each valid CRI UDP frame is parsed. The `data` parameter is a thread-safe clone.

每当解析完一个有效的 CRI UDP 帧后触发。`data` 参数是线程安全的副本。

---

## TCP Command Model / TCP 指令模型

Every SDK method that talks to the controller follows this pattern:

每个与控制器通信的 SDK 方法都遵循以下模式：

1. SDK assigns a unique `id`
2. SDK serializes `{ id, ty, db }` as JSON and sends over TCP
3. Controller responds with `{ id, ty, db, err }`
4. SDK matches the response by `id`
5. If `err` is non-empty → `CodroidCommandException`
6. If no response in 10s → `TimeoutException`

### CommonResponse / 通用响应

```csharp
public class CommonResponse
{
    public object? id { get; set; }    // Request ID / 请求 ID
    public string? ty { get; set; }    // Response type / 响应类型
    public JsonElement db { get; set; } // Business data / 业务数据
    public string? err { get; set; }   // Error message / 错误信息
}
```

Most methods return `Task<CommonResponse>`. The `db` field contains the actual result data.

大多数方法返回 `Task<CommonResponse>`。`db` 字段包含实际结果数据。

---

## Unit Convention / 单位约定

SDK public APIs use **mm** and **degrees**. This matches the TCP JSON protocol.

SDK 公共 API 使用**毫米**和**度**。这与 TCP JSON 协议一致。

| Context / 上下文 | Linear / 线性 | Angular / 角度 |
|-----------------|--------------|---------------|
| SDK API, TCP JSON | **mm** | **deg** |
| CRI UDP wire format | **m** | **rad** |
| `CriRealTimeData` (parsed) | **mm** | **deg** |

**Important / 重要:** CRI UDP binary payloads use meters and radians. The SDK automatically converts to mm/deg in `CriRealtimePacketParser.Parse()` and `CriRealtimeDispatcher` (with `convertToSi=true`). Do not assume raw UDP floats are in mm/deg.

CRI UDP 二进制载荷使用米和弧度。SDK 在 `CriRealtimePacketParser.Parse()` 和 `CriRealtimeDispatcher`（`convertToSi=true`）中自动转换为 mm/deg。不要假设原始 UDP 浮点数是 mm/deg。

---

## Async Naming Convention / 异步命名约定

All public methods return `Task` or `Task<T>` but do **not** use the `Async` suffix.

所有公共方法返回 `Task` 或 `Task<T>`，但**不**使用 `Async` 后缀。

```csharp
// These are async methods — await them / 这些是异步方法 — 需要 await
await robot.ConnectRemoteAndSwitchOn();
int di = await robot.GetDi(0);
await robot.MovJ(JointPoint.Degrees(joints), 40, 100);
```

This design keeps the same API names across C#, Python, and C++ SDKs.

这样设计是为了让 C# / Python / C++ 三套 SDK 使用同一套公开函数名。

---

## Exception Types / 异常类型

| Exception / 异常 | When / 触发条件 | Source / 来源 |
|-----------------|----------------|-------------|
| `CodroidCommandException` | Controller returns `err` field / 控制器返回 `err` 字段 | TCP response |
| `TimeoutException` | No response within 10 seconds / 10 秒内未收到响应 | TCP wait |
| `ArgumentException` | Invalid parameter value / 参数值无效 | SDK validation |
| `ArgumentOutOfRangeException` | Parameter out of range (e.g. DO port) / 参数超出范围 | SDK validation |
| `InvalidOperationException` | Not connected / 未连接 | SDK state |
| `ObjectDisposedException` | Object already disposed / 对象已释放 | SDK state |

### CodroidCommandException Properties

```csharp
public class CodroidCommandException : Exception
{
    public int RequestId { get; }          // Protocol request ID / 协议请求 ID
    public string CommandType { get; }     // e.g. "Robot/move" / 如 "Robot/move"
    public string? ControllerError { get; } // err field from controller / 控制器的 err 字段
    public CommonResponse? Response { get; } // Full response / 完整响应
}
```

---

## Thread Safety / 线程安全

- `CriData` — Thread-safe (returns a clone) / 线程安全（返回副本）
- `Data` — Not thread-safe (direct reference) / 非线程安全（直接引用）
- All TCP methods — Safe to call from any thread, but do not call concurrently on the same `CodroidClient` / 可从任意线程调用，但不要在同一 `CodroidClient` 上并发调用
- `CriRealtimeDispatcher` — `SendCommand` / `SendTrajectory` are not thread-safe / 非线程安全
