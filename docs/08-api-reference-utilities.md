# API Reference: Utilities / API 参考：工具类 API

## Publish / Subscribe (TCP Topic Push) / 发布/订阅（TCP 主题推送）

The controller pushes state-change notifications over TCP. Use `SubscribePublishTopic` to register a callback for a specific topic.

控制器通过 TCP 推送状态变更通知。使用 `SubscribePublishTopic` 注册特定主题的回调。

---

### SubscribePublishTopic / 订阅主题

```csharp
Task<PublishTopicSubscription> SubscribePublishTopic(
    string topicTy,
    Action<PublishNotification> handler,
    int tcMilliseconds = 100)
```

Subscribes to a TCP publish topic. The first call on a connection sends a subscription frame (no `id`). Subsequent pushes matching `topicTy` are dispatched to `handler` on the thread pool.

订阅 TCP 主题推送。首次在连接上调用时发送订阅帧（无 `id`）。之后匹配 `topicTy` 的推送将在线程池上分发给 `handler`。

| Parameter / 参数 | Description / 说明 |
|---|---|
| `topicTy` | Topic name, e.g. `PublishTopics.RobotStatus` / 主题名，如 `PublishTopics.RobotStatus` |
| `handler` | Callback to process notifications; should not block for long / 处理通知的回调；不应长时间阻塞 |
| `tcMilliseconds` | Protocol `tc` field in ms; default 100 / 协议 `tc` 字段，毫秒；默认 100 |

Returns a `PublishTopicSubscription` (disposable). Call `Dispose()` to unregister the local callback. It does NOT send an "unsubscribe" frame to the controller.

返回 `PublishTopicSubscription`（可释放）。调用 `Dispose()` 取消本地回调注册。不会向控制器发送"退订"报文。

**Example: Subscribe to RobotStatus / 示例：订阅 RobotStatus**

```csharp
using Codroid;

var robot = new CodroidClient("192.168.8.136");
await robot.Connect();

// Subscribe and collect pushes for 10 seconds / 订阅并收集 10 秒推送
int count = 0;
using var sub = await robot.SubscribePublishTopic(
    PublishTopics.RobotStatus,
    msg =>
    {
        int n = Interlocked.Increment(ref count);
        Console.WriteLine($"[{n}] ty={msg.Ty}");
        if (msg.Db.ValueKind != JsonValueKind.Undefined)
            Console.WriteLine("  db: " + msg.Db.GetRawText());
    });

await Task.Delay(TimeSpan.FromSeconds(10));
Console.WriteLine($"Received {Volatile.Read(ref count)} pushes.");
sub.Dispose(); // Stop receiving locally / 本地停止接收
robot.Disconnect();
```

---

### PublishTopicSubscription

Disposable handle returned by `SubscribePublishTopic`. Disposing removes the local handler; the TCP connection being dropped also invalidates all subscriptions.

`SubscribePublishTopic` 返回的可释放句柄。Dispose 移除本地处理器；TCP 断开后所有订阅也会失效。

| Member / 成员 | Description / 说明 |
|---|---|
| `TopicTy` | The topic name (`string`) / 主题名（`string`） |
| `Dispose()` | Unregister the local callback / 取消本地回调注册 |

---

### PublishNotification

The notification object passed to your handler.

传入回调处理器的通知对象。

| Property / 属性 | Type / 类型 | Description / 说明 |
|---|---|---|
| `Ty` | `string` | Topic type, e.g. `"publish/RobotStatus"` / 主题类型，如 `"publish/RobotStatus"` |
| `Db` | `JsonElement` | Business payload; `JsonValueKind.Undefined` if absent / 业务载荷；缺省时为 `Undefined` |
| `RawJson` | `string` | Full JSON text of this message / 本条消息的完整 JSON 文本 |

---

### PublishTopics Constants / PublishTopics 常量

Common topic names for subscription.

常用主题名称，用于订阅。

| Constant / 常量 | Value / 值 | Description / 说明 |
|---|---|---|
| `PublishTopics.ProjectState` | `"publish/ProjectState"` | Project run state / 工程运行状态 |
| `PublishTopics.VarUpdate` | `"publish/VarUpdate"` | Global variable changed / 全局变量变更 |
| `PublishTopics.RobotStatus` | `"publish/RobotStatus"` | Robot status / 机器人状态 |
| `PublishTopics.RobotPosture` | `"publish/RobotPosture"` | Robot posture / 机器人姿态 |
| `PublishTopics.RobotCoordinate` | `"publish/RobotCoordinate"` | Coordinate data / 坐标数据 |
| `PublishTopics.Log` | `"publish/Log"` | Log messages / 日志消息 |
| `PublishTopics.Error` | `"publish/Error"` | Error notifications / 错误通知 |

### PublishSubscribeDefaults

| Constant / 常量 | Value / 值 | Description / 说明 |
|---|---|---|
| `PublishSubscribeDefaults.TcMilliseconds` | `100` | Default `tc` for subscription frames / 订阅帧的默认 `tc` |

---

## Global Variables / 全局变量

---

### GetGlobalVars / 读取全局变量

Read all global variables from the controller (raw response).

从控制器读取所有全局变量（原始响应）。

```csharp
CommonResponse resp = await robot.GetGlobalVars();
Console.WriteLine(resp.db.GetRawText());
```

**Signature / 签名：**

```csharp
Task<CommonResponse> GetGlobalVars()
```

---

### GetGlobalVarsCatalog / 读取全局变量目录

Read all global variables and parse them into a dictionary keyed by name.

读取所有全局变量并解析为以变量名为键的字典。

```csharp
var catalog = await robot.GetGlobalVarsCatalog();

foreach (var kv in catalog)
{
    Console.WriteLine($"  Name: {kv.Key}");
    Console.WriteLine($"  Value: {kv.Value.Value.GetRawText()}");
    Console.WriteLine($"  Remark: {kv.Value.Remark}");
}
```

**Signature / 签名：**

```csharp
Task<IReadOnlyDictionary<string, GlobalVarCatalogEntry>> GetGlobalVarsCatalog()
```

---

### SaveGlobalVar / 保存单个全局变量

Save (create or update) a single global variable.

保存（创建或更新）单个全局变量。

```csharp
// Save an integer variable / 保存整数变量
await robot.SaveGlobalVar("my_counter", 100, "test remark");

// Save a string / 保存字符串
await robot.SaveGlobalVar("my_name", "hello_codroid");

// Save an array / 保存数组
await robot.SaveGlobalVar("my_arr", new[] { 1, 2, 3 });

// Save a dictionary / 保存字典
await robot.SaveGlobalVar("my_map", new Dictionary<string, int> { ["x"] = 10 });

// Save raw JSON literal / 保存原始 JSON 字面量
await robot.SaveGlobalVar("my_pose",
    new GlobalVarRawJson("{\"jp\":[1,2,3,4,5,6]}"));
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SaveGlobalVar(string name, object value, string? remark = null)
```

Variable names are validated by `GlobalVarNaming.Validate()`: must start with a letter or underscore, contain only `[A-Za-z0-9_]`, must not start with `__`, and must not collide with reserved Lua/controller identifiers.

变量名由 `GlobalVarNaming.Validate()` 校验：必须以字母或下划线开头，仅含 `[A-Za-z0-9_]`，不得以 `__` 开头，且不得与 Lua/控制器保留标识符冲突。

---

### SaveGlobalVars / 批量保存全局变量

Save multiple global variables in one request.

一次请求中保存多个全局变量。

```csharp
await robot.SaveGlobalVars(new[]
{
    new GlobalVarSaveItem("sdk_test_int", 100, "integer test"),
    new GlobalVarSaveItem("sdk_test_float", 90.4, "float test"),
    new GlobalVarSaveItem("sdk_test_str", "hello", "string test"),
    new GlobalVarSaveItem("sdk_test_arr", new[] { 1, 2, 3 }),
});
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SaveGlobalVars(IReadOnlyCollection<GlobalVarSaveItem> items)
```

---

### RemoveGlobalVars / 删除全局变量

Delete global variables by name. Deleting a non-existent name does not error.

按名称删除全局变量。删除不存在的名称不会报错。

```csharp
await robot.RemoveGlobalVars(new[] { "sdk_test_int", "sdk_test_float" });
```

**Signature / 签名：**

```csharp
Task<CommonResponse> RemoveGlobalVars(IEnumerable<string> names)
```

---

### Global Variable Helper Types / 全局变量辅助类型

#### GlobalVarSaveItem

Record struct for saving a variable.

用于保存变量的记录结构体。

```csharp
public readonly record struct GlobalVarSaveItem(
    string Name,
    object Value,
    string? Remark = null);
```

| Field / 字段 | Description / 说明 |
|---|---|
| `Name` | Variable name (validated) / 变量名（会校验） |
| `Value` | Any JSON-serializable object, or `GlobalVarRawJson` / 任意可 JSON 序列化对象，或 `GlobalVarRawJson` |
| `Remark` | Optional remark (Chinese OK); null/blank omits the `nm` field / 可选备注（可中文）；null/空白则不发送 `nm` 字段 |

#### GlobalVarRawJson

Wraps a pre-formatted JSON literal to avoid double-serialization.

包装预格式化的 JSON 字面量，避免二次序列化。

```csharp
var raw = new GlobalVarRawJson("{\"jp\":[1,2,3,4,5,6]}");
await robot.SaveGlobalVar("my_pose", raw);
```

#### GlobalVarCatalogEntry

Parsed entry from `GetGlobalVarsCatalog`.

`GetGlobalVarsCatalog` 返回的已解析条目。

| Property / 属性 | Type / 类型 | Description / 说明 |
|---|---|---|
| `Value` | `JsonElement` | The variable's value / 变量的值 |
| `Remark` | `string` | Remark string; empty if none / 备注字符串；无则为空 |

#### GlobalVarNaming

Validation utilities for global variable names.

全局变量名校验工具。

```csharp
// Throws ArgumentException on invalid name / 无效名称时抛出 ArgumentException
GlobalVarNaming.Validate("my_var");     // OK
GlobalVarNaming.Validate("__bad");      // throws: double underscore
GlobalVarNaming.Validate("if");         // throws: reserved word

// List of reserved names / 保留名称列表
IReadOnlyCollection<string> reserved = GlobalVarNaming.ReservedNames;
```

| Member / 成员 | Description / 说明 |
|---|---|
| `Validate(string name)` | Throws `ArgumentException` on invalid name / 无效名称时抛出 `ArgumentException` |
| `ReservedNames` | Read-only collection of reserved identifiers / 保留标识符的只读集合 |

---

## Kinematics / 运动学

Forward and inverse kinematics, and relative pose calculation.

正运动学、逆运动学和相对位姿计算。

---

### AposToCpos / 正运动学

Convert joint angles to Cartesian pose (forward kinematics).

将关节角转换为笛卡尔位姿（正运动学）。

```csharp
var jp = new[] { 0.0, 0.0, 90.0, 0.0, 90.0, 0.0 };
var coor = new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
var tool = new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };

// Returns parsed result directly / 直接返回解析后的结果
double[] pose = await robot.AposToCposPose(jp, coor, tool);
// pose = [x, y, z, rx, ry, rz] in mm + deg
Console.WriteLine($"TCP: [{string.Join(", ", pose)}]");

// Or get raw response / 或获取原始响应
CommonResponse resp = await robot.AposToCpos(jp, coor, tool);
```

**Signature / 签名：**

```csharp
Task<double[]> AposToCposPose(
    double[] jointDegrees,
    double[] userFrame,
    double[] toolFrame,
    double[]? externalAxisPositions = null)
```

All vectors must be exactly 6 elements. Units: degrees for joints, mm + deg for frames.

所有向量必须恰好 6 个元素。单位：关节为度，坐标系为 mm + deg。

---

### CposToApos / 逆运动学

Convert Cartesian pose to joint angles (inverse kinematics).

将笛卡尔位姿转换为关节角（逆运动学）。

```csharp
var cp = new[] { 927.503, 214.5, 898.998, 179.999, 0.0, -90.0 };
var rj = new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0 }; // reference joints

try
{
    double[] joints = await robot.CposToAposJoints(cp, rj);
    Console.WriteLine($"Joints (deg): [{string.Join(", ", joints)}]");
}
catch (InvalidOperationException)
{
    Console.WriteLine("No solution found. Try different reference joints.");
}
```

**Signature / 签名：**

```csharp
Task<double[]> CposToAposJoints(
    double[] cartesianMmDeg,
    double[] referenceJointDegrees,
    double[]? externalAxisPositions = null)
```

`referenceJointDegrees` is used as the starting guess. If the controller returns an empty array, an `InvalidOperationException` is thrown. Adjust the reference joints and retry.

`referenceJointDegrees` 用作起始猜测。如果控制器返回空数组，将抛出 `InvalidOperationException`。请调整参考关节角后重试。

---

### CalculateRelativePose / CalculateRelativePoseResult / 相对位姿计算

Calculate an offset pose relative to a tool or user coordinate frame.

计算相对于工具或用户坐标系的偏移位姿。

```csharp
var currentPose = new[] { 927.503, 214.5, 898.998, 179.999, 0.0, -90.0 };
var offset = new[] { 0.0, 0.0, -300.0, 0.0, 0.0, 0.0 };

// Calculate in user coordinate frame / 在用户坐标系下计算
double[] result = await robot.CalculateRelativePoseResult(
    currentPose,
    offset,
    RelativePoseCoorType.User);

Console.WriteLine($"Result: [{string.Join(", ", result)}]");

// Or use tool coordinate frame / 或使用工具坐标系
double[] toolResult = await robot.CalculateRelativePoseResult(
    currentPose,
    offset,
    RelativePoseCoorType.Tool);
```

**Signature / 签名：**

```csharp
Task<double[]> CalculateRelativePoseResult(
    double[] tcpPoseWorld,
    double[] offset,
    RelativePoseCoorType coorType,
    double[]? tcpPoseInPosCoorFrame = null,
    double[]? userCoorFrame = null)
```

#### RelativePoseCoorType

| Value / 值 | Integer / 整数 | Description / 说明 |
|---|---|---|
| `RelativePoseCoorType.User` | 0 | User coordinate frame / 用户坐标系 |
| `RelativePoseCoorType.Tool` | 1 | Tool coordinate frame / 工具坐标系 |

---

## ConsoleUtf8

Sets `Console.InputEncoding` and `Console.OutputEncoding` to UTF-8. On Windows this prevents Chinese character garbling. On Linux/macOS it is a no-op.

将 `Console.InputEncoding` 和 `Console.OutputEncoding` 设为 UTF-8。在 Windows 上防止中文乱码。在 Linux/macOS 上为空操作。

**Signature / 签名：**

```csharp
public static void InitConsoleUtf8()
```

**Example / 示例：**

```csharp
using Codroid;

// Call at program entry / 在程序入口调用
ConsoleUtf8.InitConsoleUtf8();

var robot = new CodroidClient("192.168.8.136");
// ... rest of your program
```

---

## Full Example: Utilities / 完整示例：工具类 API

```csharp
using System;
using System.Text.Json;
using System.Threading;
using Codroid;

ConsoleUtf8.InitConsoleUtf8();

var robot = new CodroidClient("192.168.8.136");

try
{
    await robot.ConnectRemoteAndSwitchOn();

    // --- Publish/Subscribe ---
    using var sub = await robot.SubscribePublishTopic(
        PublishTopics.RobotStatus,
        msg => Console.WriteLine($"Push: ty={msg.Ty}"));

    // --- Global Variables ---
    await robot.SaveGlobalVar("sdk_demo", 42, "demo variable");
    var catalog = await robot.GetGlobalVarsCatalog();
    if (catalog.TryGetValue("sdk_demo", out var entry))
        Console.WriteLine($"sdk_demo = {entry.Value.GetRawText()}");

    await robot.RemoveGlobalVars(new[] { "sdk_demo" });

    // --- Kinematics ---
    var jp = new[] { 0.0, 0.0, 90.0, 0.0, 90.0, 0.0 };
    var zero = new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };

    double[] tcp = await robot.AposToCposPose(jp, zero, zero);
    Console.WriteLine($"FK result: [{string.Join(", ", tcp)}]");

    try
    {
        double[] joints = await robot.CposToAposJoints(tcp, jp);
        Console.WriteLine($"IK result: [{string.Join(", ", joints)}]");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("IK: no solution with given reference.");
    }

    double[] offsetResult = await robot.CalculateRelativePoseResult(
        tcp, new[] { 0, 0, -100, 0, 0, 0 }, RelativePoseCoorType.Tool);
    Console.WriteLine($"Relative pose: [{string.Join(", ", offsetResult)}]");

    sub.Dispose();
}
finally
{
    robot.Disconnect();
}
```
