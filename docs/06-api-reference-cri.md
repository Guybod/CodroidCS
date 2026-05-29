# CRI Real-time Data & Control API Reference / CRI 实时数据与控制 API 参考

This document covers the CRI (Codroid Real-time Interface) APIs for real-time robot control, trajectory generation, and data parsing.
本文档涵盖 CRI（Codroid 实时接口）API，用于实时机器人控制、轨迹生成与数据解析。

---

## Table of Contents / 目录

1. [CriRealtimeDispatcher](#1-crirealtimedispatcher)
2. [TrajectoryGenerator](#2-trajectorygenerator)
3. [TrajectoryRequest](#3-trajectoryrequest)
4. [TrajectoryPoint](#4-trajectorypoint)
5. [TrajectorySpace](#5-trajectoryspace)
6. [TrajectoryProfile](#6-trajectoryprofile)
7. [CriRealtimePacketParser](#7-crirealtimepacketparser)
8. [Complete CRI Control Flow Example / 完整 CRI 控制流程示例](#8-complete-cri-control-flow-example--完整-cri-控制流程示例)

---

## 1. CriRealtimeDispatcher

**Sealed class, implements IDisposable / 密封类，实现 IDisposable**

A UDP-based command dispatcher that sends real-time motion commands to the robot controller. Supports single-frame commands and full trajectory playback with configurable SI-unit conversion.
基于 UDP 的命令调度器，向机器人控制器发送实时运动指令。支持单帧命令和完整轨迹回放，可配置 SI 单位转换。

### Constants / 常量

| Constant / 常量 | Type / 类型 | Value / 值 | Description / 说明 |
|---|---|---|---|
| `DefaultControllerUdpPort` | `int` | `9030` | Default UDP port for CRI commands / CRI 命令默认 UDP 端口 |
| `CommandPacketLength` | `int` | `64` | Fixed length of each command packet / 每个命令包的固定长度 |

### Constructor / 构造函数

```csharp
CriRealtimeDispatcher(string controllerIp, int controllerUdpPort = 9030, bool convertToSi = true)
```

| Parameter / 参数 | Type / 类型 | Default / 默认值 | Description / 说明 |
|---|---|---|---|
| `controllerIp` | `string` | *(required)* | IP address of the robot controller / 机器人控制器的 IP 地址 |
| `controllerUdpPort` | `int` | `9030` | UDP port for sending commands / 发送命令的 UDP 端口 |
| `convertToSi` | `bool` | `true` | If `true`, converts deg to rad and mm to m before sending. Matches CRI data stream units. / 若为 `true`，发送前将度转换为弧度、毫米转换为米，与 CRI 数据流单位一致 |

### Methods / 方法

#### SendCommand

```csharp
Task SendCommand(
    IReadOnlyList<double> position6,
    TrajectorySpace space,
    CancellationToken ct = default
)
```

Sends a single-frame position command to the controller. The `position6` list must contain exactly 6 elements (joint angles or Cartesian pose, depending on `space`).
向控制器发送单帧位置命令。`position6` 列表必须包含恰好 6 个元素（关节角度或笛卡尔位姿，取决于 `space`）。

| Parameter / 参数 | Type / 类型 | Description / 说明 |
|---|---|---|
| `position6` | `IReadOnlyList<double>` | Target position with exactly 6 elements / 目标位置，必须为 6 个元素 |
| `space` | `TrajectorySpace` | Coordinate space: `Joint` or `Cartesian` / 坐标空间：`Joint` 或 `Cartesian` |
| `ct` | `CancellationToken` | Cancellation token / 取消令牌 |

**Example / 示例:**

```csharp
// Send a single joint position command (degrees) / 发送单个关节位置命令（度）
var dispatcher = new CriRealtimeDispatcher("192.168.8.136");
await dispatcher.SendCommand(
    new double[] { 0, 0, 90, 0, 90, 0 },
    TrajectorySpace.Joint
);
```

#### SendTrajectory

```csharp
Task SendTrajectory(
    IEnumerable<TrajectoryPoint> trajectory,
    TrajectorySpace space,
    int periodMs,
    CancellationToken ct = default
)
```

Sends a complete trajectory to the controller at a fixed time interval. Each point is dispatched according to `periodMs` to maintain real-time timing.
以固定时间间隔向控制器发送完整轨迹。每个点按照 `periodMs` 发送以保持实时时序。

| Parameter / 参数 | Type / 类型 | Description / 说明 |
|---|---|---|
| `trajectory` | `IEnumerable<TrajectoryPoint>` | Sequence of trajectory points to send / 要发送的轨迹点序列 |
| `space` | `TrajectorySpace` | Coordinate space: `Joint` or `Cartesian` / 坐标空间：`Joint` 或 `Cartesian` |
| `periodMs` | `int` | Interval between points in milliseconds / 相邻点之间的时间间隔（毫秒） |
| `ct` | `CancellationToken` | Cancellation token / 取消令牌 |

**Example / 示例:**

```csharp
// Send trajectory at 4ms intervals (250Hz) / 以 4ms 间隔发送轨迹（250Hz）
using var dispatcher = new CriRealtimeDispatcher("192.168.8.136");
await dispatcher.SendTrajectory(trajectory, TrajectorySpace.Joint, periodMs: 4);
```

#### Dispose

```csharp
void Dispose()
```

Closes the underlying UDP socket and releases resources. Call this when you are done sending commands, or use a `using` statement to ensure automatic cleanup.
关闭底层 UDP 套接字并释放资源。发送完成后调用此方法，或使用 `using` 语句确保自动清理。

---

## 2. TrajectoryGenerator

**Static class / 静态类**

Generates smooth trajectories between two positions using configurable motion profiles (cubic or trapezoidal). Returns an enumerable sequence of `TrajectoryPoint` objects.
使用可配置的运动曲线（三次或梯形）在两个位置之间生成平滑轨迹。返回 `TrajectoryPoint` 对象的可枚举序列。

### Methods / 方法

#### Generate

```csharp
static IEnumerable<TrajectoryPoint> Generate(
    IReadOnlyList<double> start,
    IReadOnlyList<double> target,
    TrajectoryRequest request
)
```

Generates a trajectory from `start` to `target` according to the parameters in `request`.
根据 `request` 中的参数，生成从 `start` 到 `target` 的轨迹。

| Parameter / 参数 | Type / 类型 | Description / 说明 |
|---|---|---|
| `start` | `IReadOnlyList<double>` | Starting position (joint angles or Cartesian) / 起始位置（关节角度或笛卡尔坐标） |
| `target` | `IReadOnlyList<double>` | Target position (joint angles or Cartesian) / 目标位置（关节角度或笛卡尔坐标） |
| `request` | `TrajectoryRequest` | Trajectory generation parameters / 轨迹生成参数 |

**Returns / 返回值:** `IEnumerable<TrajectoryPoint>` -- sequence of trajectory points / 轨迹点序列

**Example / 示例:**

```csharp
var start = new double[] { 0, 0, 90, 0, 90, 0 };
var target = new double[] { 10, 20, 45, 0, 60, 30 };

var request = new TrajectoryRequest
{
    Space = TrajectorySpace.Joint,
    Profile = TrajectoryProfile.Cubic,
    FrequencyHz = 250,
    Speed = 30
};

var trajectory = TrajectoryGenerator.Generate(start, target, request).ToList();
Console.WriteLine($"Generated {trajectory.Count} trajectory points");
```

---

## 3. TrajectoryRequest

**Sealed class / 密封类**

Parameters that control how a trajectory is generated, including coordinate space, timing, speed, and motion profile.
控制轨迹生成的参数，包括坐标空间、时序、速度和运动曲线。

### Properties / 属性

| Property / 属性 | Type / 类型 | Default / 默认值 | Description / 说明 |
|---|---|---|---|
| `Space` | `TrajectorySpace` | *(none)* | Coordinate space for the trajectory / 轨迹的坐标空间 |
| `FrequencyHz` | `double` | `250.0` | Sampling frequency in Hz / 采样频率（赫兹） |
| `Speed` | `double?` | `null` | Target speed (mutually exclusive with `DurationSeconds`) / 目标速度（与 `DurationSeconds` 互斥） |
| `DurationSeconds` | `double?` | `null` | Total duration in seconds (mutually exclusive with `Speed`) / 总时长（秒，与 `Speed` 互斥） |
| `Profile` | `TrajectoryProfile` | `Cubic` | Motion profile type / 运动曲线类型 |
| `Acceleration` | `double` | `1000.0` | Acceleration value for trapezoidal profile / 梯形曲线的加速度值 |

> **Note / 注意:** `Speed` and `DurationSeconds` are mutually exclusive. Set only one of them. If both are set, behavior is undefined.
> `Speed` 和 `DurationSeconds` 互斥，只能设置其中一个。如果同时设置，行为未定义。

**Example / 示例:**

```csharp
// Using Speed / 使用速度
var requestBySpeed = new TrajectoryRequest
{
    Space = TrajectorySpace.Joint,
    FrequencyHz = 250,
    Speed = 30,
    Profile = TrajectoryProfile.Trapezoidal,
    Acceleration = 800
};

// Using DurationSeconds / 使用时长
var requestByDuration = new TrajectoryRequest
{
    Space = TrajectorySpace.Joint,
    FrequencyHz = 250,
    DurationSeconds = 2.5,
    Profile = TrajectoryProfile.Cubic
};
```

---

## 4. TrajectoryPoint

**Sealed class / 密封类**

Represents a single point in a trajectory, containing a timestamp and a position array.
表示轨迹中的单个点，包含时间戳和位置数组。

### Properties / 属性

| Property / 属性 | Type / 类型 | Default / 默认值 | Description / 说明 |
|---|---|---|---|
| `TimeSeconds` | `double` | `0.0` | Time offset from trajectory start in seconds / 从轨迹开始的时间偏移（秒） |
| `Position` | `double[]` | `[]` (empty) | Position values (joint angles or Cartesian coordinates) / 位置值（关节角度或笛卡尔坐标） |

**Example / 示例:**

```csharp
var point = new TrajectoryPoint
{
    TimeSeconds = 0.016,
    Position = new double[] { 0.5, 1.0, 45.0, 0.0, 30.0, 10.0 }
};

Console.WriteLine($"t={point.TimeSeconds}s, pos=[{string.Join(", ", point.Position)}]");
```

---

## 5. TrajectorySpace

**Enum / 枚举**

Defines the coordinate space used for trajectory positions.
定义轨迹位置使用的坐标空间。

| Name / 名称 | Value / 值 | Description / 说明 |
|---|---|---|
| `Joint` | `0` | Joint space: positions are joint angles (deg or rad) / 关节空间：位置为关节角度（度或弧度） |
| `Cartesian` | `1` | Cartesian space: positions are tool pose (X, Y, Z, Rx, Ry, Rz) / 笛卡尔空间：位置为工具位姿 (X, Y, Z, Rx, Ry, Rz) |

**Example / 示例:**

```csharp
// Trajectory in joint space / 关节空间轨迹
var jointRequest = new TrajectoryRequest { Space = TrajectorySpace.Joint };

// Trajectory in Cartesian space / 笛卡尔空间轨迹
var cartRequest = new TrajectoryRequest { Space = TrajectorySpace.Cartesian };
```

---

## 6. TrajectoryProfile

**Enum / 枚举**

Defines the motion profile shape used during trajectory generation.
定义轨迹生成中使用的运动曲线形状。

| Name / 名称 | Value / 值 | Description / 说明 |
|---|---|---|
| `Cubic` | `0` | Cubic polynomial profile: smooth acceleration and deceleration / 三次多项式曲线：平滑加减速 |
| `Trapezoidal` | `1` | Trapezoidal velocity profile: constant acceleration phase, cruise phase, constant deceleration phase / 梯形速度曲线：恒加速段、匀速段、恒减速段 |

**Example / 示例:**

```csharp
// Cubic profile for smooth motion / 三次曲线用于平滑运动
var smoothRequest = new TrajectoryRequest
{
    Profile = TrajectoryProfile.Cubic,
    Speed = 30
};

// Trapezoidal profile with explicit acceleration / 梯形曲线，指定加速度
var preciseRequest = new TrajectoryRequest
{
    Profile = TrajectoryProfile.Trapezoidal,
    Speed = 50,
    Acceleration = 1000
};
```

---

## 7. CriRealtimePacketParser

**Static class / 静态类**

Parses raw CRI data packets received from the controller into structured `CriRealTimeData` objects. Automatically converts SI units (m to mm, rad to deg) for consistent use in application code.
将从控制器接收的原始 CRI 数据包解析为结构化的 `CriRealTimeData` 对象。自动转换单位（米转毫米，弧度转度），以便在应用代码中统一使用。

### Constants / 常量

| Constant / 常量 | Type / 类型 | Value / 值 | Description / 说明 |
|---|---|---|---|
| `PacketLength` | `int` | `308` | Expected length of a CRI data packet in bytes / CRI 数据包的预期字节长度 |
| `DefaultDecimalPlaces` | `int` | `3` | Default number of decimal places for rounding / 默认四舍五入小数位数 |

### Methods / 方法

#### Parse

```csharp
static CriRealTimeData Parse(byte[] packet)
```

Parses a raw CRI data packet into a `CriRealTimeData` object. Converts m to mm and rad to deg for all position and orientation values.
将原始 CRI 数据包解析为 `CriRealTimeData` 对象。将所有位置和方向值从米转换为毫米，弧度转换为度。

| Parameter / 参数 | Type / 类型 | Description / 说明 |
|---|---|---|
| `packet` | `byte[]` | Raw CRI data packet (must be 308 bytes) / 原始 CRI 数据包（必须为 308 字节） |

**Returns / 返回值:** `CriRealTimeData` -- parsed real-time data object / 解析后的实时数据对象

**Example / 示例:**

```csharp
byte[] rawPacket = ReceiveCriPacket(); // your packet source / 您的数据包来源
var data = CriRealtimePacketParser.Parse(rawPacket);

Console.WriteLine($"Joint Positions: [{string.Join(", ", data.JointPosition)}]");
Console.WriteLine($"TCP Pose: [{string.Join(", ", data.TcpPose)}]");
```

---

## 8. Accessing CRI Real-time Data / 访问 CRI 实时数据

There are **two ways** to access CRI real-time data after calling `StartCriDataPush`.

调用 `StartCriDataPush` 后，有**两种方式**访问 CRI 实时数据。

### Method 1: CriData Property (Polling) / 方式一：CriData 属性（轮询）

Read the latest cached snapshot at any time. This is a thread-safe deep clone.

随时读取最新的缓存快照。这是线程安全的深拷贝。

```csharp
// Start CRI push first / 先启动 CRI 推送
await robot.StartCriDataPush("192.168.8.150", 18888);

// Read latest data anytime / 随时读取最新数据
CriRealTimeData data = robot.CriData;

if (data != null)
{
    Console.WriteLine($"关节角度: [{string.Join(", ", data.JointPosition)}]");
    Console.WriteLine($"TCP 位姿: [{string.Join(", ", data.TcpPose)}]");
    Console.WriteLine($"是否运动中: {data.InMotion}");
    Console.WriteLine($"碰撞停止: {data.CollisionStopped}");
    Console.WriteLine($"急停按下: {data.EmergencyStopPressed}");
    Console.WriteLine($"有报警: {data.HasAlarm}");
}
```

**Use cases / 适用场景：**
- Sync motion APIs (internal polling) / 阻塞运动 API（内部轮询）
- Periodic state checking / 定期状态检查
- One-time position read / 一次性位置读取

### Method 2: CriDataReceived Event (Push) / 方式二：CriDataReceived 事件（推送）

Register an event handler that fires on every received CRI frame.

注册事件处理程序，每收到一帧 CRI 数据自动触发。

```csharp
// Register event handler BEFORE starting push / 在启动推送前注册事件处理程序
robot.CriDataReceived += (CriRealTimeData data) =>
{
    Console.WriteLine($"关节角度: [{string.Join(", ", data.JointPosition)}]");
    Console.WriteLine($"TCP 位姿: [{string.Join(", ", data.TcpPose)}]");
    Console.WriteLine($"是否运动中: {data.InMotion}");
};

// Then start push / 然后启动推送
await robot.StartCriDataPush("192.168.8.150", 18888);
```

**Use cases / 适用场景：**
- Real-time monitoring / 实时监控
- Data logging / 数据记录
- Event-driven workflows / 事件驱动工作流

### Comparison / 对比

| Feature / 特性 | `CriData` Property / 属性 | `CriDataReceived` Event / 事件 |
|----------------|---------------------------|--------------------------------|
| Access pattern / 访问模式 | Pull (poll when needed) / 拉取（需要时轮询） | Push (automatic callback) / 推送（自动回调） |
| Thread safety / 线程安全 | ✅ Deep clone / 深拷贝 | ✅ Runs on receive thread / 在接收线程运行 |
| Missed frames / 丢失帧 | May miss between reads / 读取间隔可能丢帧 | No loss / 不丢帧 |
| Complexity / 复杂度 | Simple / 简单 | Requires event handler / 需要事件处理程序 |
| Best for / 最适合 | Sync motion, one-time reads / 阻塞运动、一次性读取 | Monitoring, logging / 监控、记录 |

### Combined Example / 组合示例

```csharp
var robot = new CodroidClient("192.168.8.136");

// Register event for continuous monitoring / 注册事件用于持续监控
robot.CriDataReceived += (data) =>
{
    if (data.HasAlarm)
        Console.WriteLine($"⚠️ 报警: 错误码 {data.CriErrorCode}");
};

await robot.ConnectRemoteAndSwitchOn();
await robot.StartCriDataPush("192.168.8.150", 18888);

// Use CriData for sync motion / 使用 CriData 进行阻塞运动
robot.MovJSync(JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 }), speed: 40, acc: 100);

// Use CriData for one-time read / 使用 CriData 进行一次性读取
var current = robot.CriData;
Console.WriteLine($"当前位置: [{string.Join(", ", current.JointPosition)}]");
```

---

## 9. Complete CRI Control Flow Example / 完整 CRI 控制流程示例

This example demonstrates the full CRI control workflow: starting data reception, reading the current position, generating a trajectory, sending commands via the real-time dispatcher, and cleanly shutting down.
本示例展示完整的 CRI 控制流程：启动数据接收、读取当前位置、生成轨迹、通过实时调度器发送命令，以及安全关闭。

```csharp
using Codroid.CRI;
using Codroid.Robot;

// Initialize robot connection / 初始化机器人连接
var robot = new CodroidRobot("192.168.8.150");

// ========================================
// Step 1: Start CRI data push / 启动 CRI 数据推送
// ========================================
// Begin receiving real-time data from the controller on the specified port
// 开始从控制器在指定端口接收实时数据
await robot.StartCriDataPush("192.168.8.150", 18888);

// ========================================
// Step 2: Read current position / 读取当前位置
// ========================================
// Use the current joint position as the trajectory start point
// 使用当前关节位置作为轨迹起点
double[] start = robot.CriData.JointPosition;
Console.WriteLine($"Current position: [{string.Join(", ", start)}]");

// Define target position (joint angles in degrees) / 定义目标位置（关节角度，单位：度）
double[] target = new[] { 0, 0, 90, 0, 90, 0 };
Console.WriteLine($"Target position:  [{string.Join(", ", target)}]");

// ========================================
// Step 3: Generate trajectory / 生成轨迹
// ========================================
// Configure trajectory parameters / 配置轨迹参数
var request = new TrajectoryRequest
{
    Space = TrajectorySpace.Joint,       // Joint space / 关节空间
    Profile = TrajectoryProfile.Cubic,   // Smooth cubic profile / 平滑三次曲线
    FrequencyHz = 250,                   // 250Hz sampling / 250Hz 采样
    Speed = 30                           // 30 deg/s / 30 度/秒
};

// Generate the trajectory / 生成轨迹
var trajectory = TrajectoryGenerator.Generate(start, target, request).ToList();
Console.WriteLine($"Generated {trajectory.Count} points over {trajectory.Last().TimeSeconds:F3}s");

// ========================================
// Step 4: Start CRI control / 启动 CRI 控制
// ========================================
// Enable real-time control mode on the controller
// 在控制器上启用实时控制模式
//   filterType: 1  - Position filter type / 位置滤波类型
//   durationMs: 4  - Control loop period in ms / 控制循环周期（毫秒）
//   startBuffer: 5 - Initial buffer size / 初始缓冲区大小
await robot.StartCriControl(filterType: 1, durationMs: 4, startBuffer: 5);

try
{
    // ========================================
    // Step 5: Send trajectory / 下发轨迹
    // ========================================
    // Create a dispatcher and send the trajectory at 4ms intervals
    // 创建调度器，以 4ms 间隔发送轨迹
    using var dispatcher = new CriRealtimeDispatcher("192.168.8.136");
    await dispatcher.SendTrajectory(trajectory, TrajectorySpace.Joint, periodMs: 4);

    Console.WriteLine("Trajectory execution completed / 轨迹执行完成");
}
finally
{
    // ========================================
    // Step 6: Stop CRI control / 停止 CRI 控制
    // ========================================
    // Always stop CRI control and data push in finally block
    // 始终在 finally 块中停止 CRI 控制和数据推送
    await robot.StopCriControl();
    await robot.StopCriDataPush("192.168.8.150", 18888);

    Console.WriteLine("CRI control stopped / CRI 控制已停止");
}
```

### Workflow Diagram / 工作流程图

```
Start CRI Data Push / 启动数据推送
        |
        v
Read Current Position / 读取当前位置
        |
        v
Generate Trajectory / 生成轨迹
        |
        v
Start CRI Control / 启动 CRI 控制
        |
        v
Send Trajectory via Dispatcher / 通过调度器发送轨迹
        |
        v
Stop CRI Control / 停止 CRI 控制
        |
        v
Stop CRI Data Push / 停止数据推送
```

> **Important / 重要:** Always stop CRI control and data push in a `finally` block to ensure clean shutdown, even if an exception occurs during trajectory execution.
> 始终在 `finally` 块中停止 CRI 控制和数据推送，确保即使轨迹执行过程中发生异常也能安全关闭。
