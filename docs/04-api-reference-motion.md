# Motion API Reference / 运动 API 参考

This document covers all motion-related types in the CodroidCS SDK, including joint/Cartesian point definitions, move instructions, jog parameters, and motion wait options.

本文档涵盖 CodroidCS SDK 中所有与运动相关的类型，包括关节/笛卡尔点定义、运动指令、点动参数和运动等待选项。

---

## Table of Contents / 目录

1. [JointPoint](#1-jointpoint)
2. [CartesianPoint](#2-cartesianpoint)
3. [MovePoint](#3-movepoint)
4. [MoveInstruction](#4-moveinstruction)
5. [MoveToTarget](#5-movetotarget)
6. [MoveToKind](#6-movetokind)
7. [MoveKinds](#7-movekinds)
8. [MotionWaitOptions](#8-motionwaitoptions)
9. [RobotJogParameters](#9-robotjogparameters)
10. [RobotJogMode](#10-robotjogmode)
11. [RobotJogFrameType](#11-robotjogframetype)

---

## 1. JointPoint

**A sealed class representing a robot target defined by joint angles.**

**一个密封类，表示由关节角度定义的机器人目标点。**

`JointPoint` stores 6 joint angles in degrees and is used when you want to move the robot to an exact joint configuration without ambiguity.

`JointPoint` 以度为单位存储 6 个关节角度，当您希望将机器人移动到精确的关节构型而无歧义时使用。

### Properties / 属性

| Property | Type | Description / 描述 |
|----------|------|---------------------|
| `Jp` | `double[]` | 6 joint angles in degrees / 6 个关节角度（单位：度） |

### Factory Methods / 工厂方法

| Method | Description / 描述 |
|--------|---------------------|
| `JointPoint.Degrees(double[] jointsDeg)` | Create from 6 joint angles in degrees. The array **must** be exactly length 6. / 从 6 个关节角度（度）创建。数组**必须**恰好为长度 6。 |

### Example / 示例

```csharp
// Create a joint point with all 6 joint angles in degrees
// 创建一个包含 6 个关节角度（度）的关节点
var jp = JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 });

// Use in a move instruction
// 在运动指令中使用
await robot.Move(MoveInstruction.MovJ(jp, speed: 40, acc: 100));
```

---

## 2. CartesianPoint

**A sealed class representing a robot target defined by Cartesian (TCP) pose, with optional reference joints for inverse kinematics.**

**一个密封类，表示由笛卡尔（工具中心点）位姿定义的机器人目标点，可选参考关节用于逆运动学求解。**

`CartesianPoint` stores a TCP pose as `[x, y, z, rx, ry, rz]` in millimeters and degrees. When only a pose is provided, the controller uses default reference joints `[20, 20, 20, 20, 20, 20]` for inverse kinematics. You can supply explicit reference joints to guide the IK solver toward a specific configuration.

`CartesianPoint` 以毫米和度为单位存储 TCP 位姿 `[x, y, z, rx, ry, rz]`。当仅提供位姿时，控制器使用默认参考关节 `[20, 20, 20, 20, 20, 20]` 进行逆运动学求解。您可以提供显式参考关节来引导 IK 求解器朝特定构型求解。

### Properties / 属性

| Property | Type | Description / 描述 |
|----------|------|---------------------|
| `Cp` | `double[]` | TCP pose `[x, y, z, rx, ry, rz]` — position in mm, orientation in degrees / TCP 位姿 `[x, y, z, rx, ry, rz]` — 位置单位 mm，姿态单位度 |
| `Rj` | `double[]?` | Reference joints for IK (6 joint angles in degrees). `null` uses default `[20,20,20,20,20,20]` / 用于逆运动学的参考关节（6 个关节角度，度）。`null` 时使用默认值 `[20,20,20,20,20,20]` |

### Factory Methods / 工厂方法

| Method | Description / 描述 |
|--------|---------------------|
| `CartesianPoint.MmDeg(double[] poseMmDeg)` | Create with TCP pose only (uses default reference joints) / 仅使用 TCP 位姿创建（使用默认参考关节） |
| `CartesianPoint.MmDegWithRef(double[] poseMmDeg, double[] refJointsDeg)` | Create with TCP pose and explicit reference joints for IK / 使用 TCP 位姿和显式参考关节创建 |

### Examples / 示例

```csharp
// Create a Cartesian point with TCP pose only (position + orientation)
// 仅使用 TCP 位姿创建笛卡尔点（位置 + 姿态）
var cp = CartesianPoint.MmDeg(new[] { 400, 0, 300, 180, 0, 0 });

// Create a Cartesian point with explicit reference joints from current robot state
// 使用机器人当前状态的显式参考关节创建笛卡尔点
var refJ = robot.CriData.JointPosition;
var cpWithRef = CartesianPoint.MmDegWithRef(
    new[] { 400, 0, 300, 180, 0, 0 },
    refJ
);

// Use in a linear move instruction
// 在直线运动指令中使用
await robot.Move(MoveInstruction.MovL(cp, speed: 150, acc: 500));
```

---

## 3. MovePoint

**A sealed class used internally for serialization of move target points.**

**一个密封类，内部用于运动目标点的序列化。**

`MovePoint` is the serialization wrapper used when sending move instructions to the controller. It holds the optional joint (`Jp`), Cartesian (`Cp`), reference joint (`Rj`), and external (`Ep`) arrays. You typically do not create `MovePoint` instances directly; use the factory methods on `MoveInstruction` instead.

`MovePoint` 是向控制器发送运动指令时使用的序列化包装器。它包含可选的关节 (`Jp`)、笛卡尔 (`Cp`)、参考关节 (`Rj`) 和外部 (`Ep`) 数组。通常不需要直接创建 `MovePoint` 实例，而是使用 `MoveInstruction` 上的工厂方法。

### Properties / 属性

| Property | Type | Description / 描述 |
|----------|------|---------------------|
| `Jp` | `double[]?` | Joint angles (degrees), null if Cartesian target / 关节角度（度），笛卡尔目标时为 null |
| `Cp` | `double[]?` | TCP pose (mm + deg), null if joint target / TCP 位姿（mm + 度），关节目标时为 null |
| `Rj` | `double[]?` | Reference joints for IK / 用于逆运动学的参考关节 |
| `Ep` | `double[]?` | External axes / 外部轴 |

> All properties use `[JsonIgnoreWhenNull]` — they are omitted from JSON serialization when null.
>
> 所有属性使用 `[JsonIgnoreWhenNull]` — 当值为 null 时在 JSON 序列化中被忽略。

### Factory Methods / 工厂方法

| Method | Description / 描述 |
|--------|---------------------|
| `MovePoint.FromJoint(JointPoint jp)` | Create from a `JointPoint` / 从 `JointPoint` 创建 |
| `MovePoint.FromCartesian(CartesianPoint cp)` | Create from a `CartesianPoint` / 从 `CartesianPoint` 创建 |

### Example / 示例

```csharp
// Typically you do not create MovePoint directly. Use MoveInstruction factories.
// 通常不会直接创建 MovePoint，而是使用 MoveInstruction 的工厂方法。

// If you need to wrap a point explicitly:
// 如果需要显式包装一个点：
var jp = JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 });
var movePoint = MovePoint.FromJoint(jp);

var cp = CartesianPoint.MmDeg(new[] { 400, 0, 300, 180, 0, 0 });
var movePointFromCart = MovePoint.FromCartesian(cp);
```

---

## 4. MoveInstruction

**A sealed class that defines a single motion segment in a robot move command.**

**一个密封类，定义机器人运动命令中的单个运动段。**

`MoveInstruction` is the primary type for building motion paths. Each instance describes one segment with a motion type (joint, linear, or circular), speed/acceleration parameters, blending settings, and optional coordinate system and tool offsets.

`MoveInstruction` 是构建运动路径的主要类型。每个实例描述一个运动段，包含运动类型（关节、直线或圆弧）、速度/加速度参数、混合设置以及可选的坐标系和工具偏移。

### Properties / 属性

| Property | Type | Default | Description / 描述 |
|----------|------|---------|---------------------|
| `Type` | `string` | `"movJ"` | Motion type: `"movJ"`, `"movL"`, `"movC"`, `"movCircle"` / 运动类型 |
| `CircleNum` | `int?` | `null` | Number of full circles (only for `movCircle`) / 整圆圈数（仅用于 `movCircle`） |
| `Speed` | `double` | — | Speed value (mm/s for linear, deg/s for joint) / 速度值（直线 mm/s，关节 deg/s） |
| `Acc` | `double` | — | Acceleration value / 加速度值 |
| `Blend` | `double?` | `null` | Blend radius (mm for linear, deg for joint). Mutually exclusive with `RelativeBlend`. Omit for no transition / 混合半径（直线 mm，关节 deg）。与 `RelativeBlend` 互斥。不传表示无过渡 |
| `RelativeBlend` | `double?` | `null` | Relative blend ratio (0–1). Mutually exclusive with `Blend` — if both set, this is ignored / 相对混合比（0–1）。与 `Blend` 互斥——同时设置时此属性无效 |
| `TargetPoint` | `MovePoint` | — | The target point for this segment / 本段的目标点 |
| `MiddlePoint` | `MovePoint?` | `null` | Middle/via point (required for `movC` and `movCircle`) / 中间/经过点（`movC` 和 `movCircle` 必需） |
| `Coor` | `double[]?` | `null` | Coordinate system definition / 坐标系定义 |
| `Tool` | `double[]?` | `null` | Tool definition / 工具定义 |

### Factory Methods / 工厂方法

All factories share common optional parameters: `coor` (coordinate system), `tool` (tool offset), and `relativeBlend` (relative blend ratio).

所有工厂方法共享可选参数：`coor`（坐标系）、`tool`（工具偏移）和 `relativeBlend`（相对混合比）。

| Method | Motion Type | Target Types | Description / 描述 |
|--------|-------------|--------------|---------------------|
| `MoveInstruction.MovJ(JointPoint, speed, acc, blend, ...)` | Joint | JointPoint | Joint move to joint target / 关节运动到关节目标 |
| `MoveInstruction.MovJ(CartesianPoint, speed, acc, blend, ...)` | Joint | CartesianPoint | Joint move to Cartesian target / 关节运动到笛卡尔目标 |
| `MoveInstruction.MovL(CartesianPoint, speed, acc, blend, ...)` | Linear | CartesianPoint | Linear move to Cartesian target / 直线运动到笛卡尔目标 |
| `MoveInstruction.MovL(JointPoint, speed, acc, blend, ...)` | Linear | JointPoint | Linear move to joint target / 直线运动到关节目标 |
| `MoveInstruction.MovC(CartesianPoint middle, CartesianPoint target, speed, acc, blend, ...)` | Circular | 2x CartesianPoint | Circular arc through middle to target / 经过中间点到目标的圆弧运动 |
| `MoveInstruction.MovCircle(CartesianPoint middle, CartesianPoint target, int circleNum, speed, acc, blend, ...)` | Full Circle | 2x CartesianPoint + circleNum | Full circle motion / 整圆运动 |

### Parameter Reference / 参数参考

| Parameter | Type | Default | Description / 描述 |
|-----------|------|---------|---------------------|
| `speed` | `double` | — | Required. Speed (mm/s or deg/s) / 必需。速度（mm/s 或 deg/s） |
| `acc` | `double` | — | Required. Acceleration / 必需。加速度 |
| `blend` | `double?` | `null` | Blend radius. Mutually exclusive with `relativeBlend` — if both set, `relativeBlend` is ignored. Omit for no transition / 平滑半径。与 `relativeBlend` 互斥——同时传入时 `relativeBlend` 无效。不传表示无过渡 |
| `coor` | `double[]?` | `null` | User coordinate frame. `null` = omitted from command / 用户坐标系。`null` 时指令中不包含该字段 |
| `tool` | `double[]?` | `null` | Tool coordinate frame. `null` = omitted from command / 工具坐标系。`null` 时指令中不包含该字段 |
| `relativeBlend` | `double?` | `null` | Relative blend (0–1). Mutually exclusive with `blend` — if both set, this is ignored / 相对混合比（0–1）。与 `blend` 互斥——同时传入时此参数无效 |

### Examples / 示例

```csharp
// Single joint move to a joint target
// 关节运动到关节目标（单段）
var j1 = JointPoint.Degrees(new[] { 20, 20, 90, 0, 45, 0 });
await robot.Move(MoveInstruction.MovJ(j1, speed: 40, acc: 100));

// Single linear move to a Cartesian target
// 直线运动到笛卡尔目标（单段）
var cp = CartesianPoint.MmDeg(new[] { 400, 0, 300, 180, 0, 0 });
await robot.Move(MoveInstruction.MovL(cp, speed: 150, acc: 500));

// Multi-segment path: joint move followed by linear move
// 多段路径：关节运动后接直线运动
var p2 = new[] { 500, 100, 400, 180, 0, 0 };
var refJ = robot.CriData.JointPosition;
await robot.Move(new[]
{
    MoveInstruction.MovJ(JointPoint.Degrees(j1), 40, 100),
    MoveInstruction.MovL(CartesianPoint.MmDegWithRef(p2, refJ), 150, 500),
});

// Circular arc motion
// 圆弧运动
var mid = CartesianPoint.MmDeg(new[] { 450, 50, 350, 180, 0, 0 });
var end = CartesianPoint.MmDeg(new[] { 500, 0, 300, 180, 0, 0 });
await robot.Move(MoveInstruction.MovC(mid, end, speed: 100, acc: 300));

// Full circle motion (2 full rotations)
// 整圆运动（2 圈）
await robot.Move(MoveInstruction.MovCircle(mid, end, circleNum: 2, speed: 80, acc: 200));

// With custom blend and coordinate system
// 自定义混合和坐标系
await robot.Move(MoveInstruction.MovL(cp, speed: 100, acc: 300, blend: 10, coor: userCoord));
```

---

## 5. MoveToTarget

**A sealed class representing a target for pre-defined move-to commands (home, safe, pack, etc.).**

**一个密封类，表示预定义移动命令（回零、安全位、打包位等）的目标。**

`MoveToTarget` wraps a target point for use with `MoveToKind` commands. It can represent a joint target, a Cartesian target, or raw external axis data.

`MoveToTarget` 为 `MoveToKind` 命令包装目标点。它可以表示关节目标、笛卡尔目标或原始外部轴数据。

### Properties / 属性

| Property | Type | Description / 描述 |
|----------|------|---------------------|
| `Cp` | `double[]?` | Cartesian pose `[x, y, z, rx, ry, rz]` (mm + deg) / 笛卡尔位姿 `[x, y, z, rx, ry, rz]`（mm + 度） |
| `Jp` | `double[]?` | Joint angles (degrees) / 关节角度（度） |
| `Ep` | `double[]?` | External axes / 外部轴 |

### Factory Methods / 工厂方法

| Method | Description / 描述 |
|--------|---------------------|
| `MoveToTarget.Joint(JointPoint jp)` | Create from a `JointPoint` / 从 `JointPoint` 创建 |
| `MoveToTarget.Cartesian(CartesianPoint cp)` | Create from a `CartesianPoint` / 从 `CartesianPoint` 创建 |

### Example / 示例

```csharp
// Create a moveTo target from a joint point
// 从关节点创建 moveTo 目标
var homeTarget = MoveToTarget.Joint(JointPoint.Degrees(new[] { 0, 0, 0, 0, 0, 0 }));

// Create a moveTo target from a Cartesian point
// 从笛卡尔点创建 moveTo 目标
var safeTarget = MoveToTarget.Cartesian(
    CartesianPoint.MmDeg(new[] { 300, 0, 400, 180, 0, 0 })
);
```

---

## 6. MoveToKind

**An enum specifying pre-defined move-to target types.**

**一个枚举，指定预定义的移动目标类型。**

`MoveToKind` is used with the robot's `MoveTo` method to command the robot to move to well-known positions or resume program execution.

`MoveToKind` 与机器人的 `MoveTo` 方法配合使用，命令机器人移动到已知位置或恢复程序执行。

### Values / 值

| Name | Value | Description / 描述 |
|------|-------|---------------------|
| `Stop` | -1 | Stop the moveTo operation / 停止 moveTo 操作 |
| `Home` | 0 | Home position / 原点位置 |
| `Safe` | 1 | Safe position / 安全位置 |
| `Candle` | 2 | Candle (vertical) position / 烛台（垂直）位置 |
| `Pack` | 3 | Pack (transport) position / 打包（运输）位置 |
| `JointPlanned` | 4 | Joint planned move to target / 关节规划运动到目标 |
| `LinePlanned` | 5 | Linear planned move to target / 直线规划运动到目标 |
| `ProgramResume` | 6 | Resume program execution / 恢复程序执行 |

### Example / 示例

```csharp
// Move the robot to the home position
// 将机器人移动到原点位置
await robot.MoveTo(MoveToKind.Home);

// Stop an in-progress moveTo operation
// 停止正在进行的 moveTo 操作
await robot.MoveTo(MoveToKind.Stop);

// Move to a specific joint position with joint planning
// 使用关节规划移动到特定关节位置
var target = MoveToTarget.Joint(JointPoint.Degrees(new[] { 10, 20, 90, 0, 45, 0 }));
await robot.MoveTo(MoveToKind.JointPlanned, target);

// Move to a Cartesian position with linear planning
// 使用直线规划移动到笛卡尔位置
var cartTarget = MoveToTarget.Cartesian(
    CartesianPoint.MmDeg(new[] { 400, 0, 300, 180, 0, 0 })
);
await robot.MoveTo(MoveToKind.LinePlanned, cartTarget);

// Resume a paused program
// 恢复暂停的程序
await robot.MoveTo(MoveToKind.ProgramResume);
```

---

## 7. MoveKinds

**A static class providing string constants for motion type identifiers.**

**一个静态类，提供运动类型标识符的字符串常量。**

`MoveKinds` defines the string constants used in `MoveInstruction.Type`. These correspond to the four supported motion modes in the CodroidCS controller.

`MoveKinds` 定义了 `MoveInstruction.Type` 中使用的字符串常量。它们对应 CodroidCS 控制器支持的四种运动模式。

### Constants / 常量

| Name | Value | Description / 描述 |
|------|-------|---------------------|
| `MovJ` | `"movJ"` | Joint move / 关节运动 |
| `MovL` | `"movL"` | Linear move / 直线运动 |
| `MovC` | `"movC"` | Circular arc move / 圆弧运动 |
| `MovCircle` | `"movCircle"` | Full circle move / 整圆运动 |

### Example / 示例

```csharp
// Compare the motion type of an instruction
// 比较指令的运动类型
var instruction = MoveInstruction.MovJ(
    JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 }),
    speed: 40, acc: 100
);

if (instruction.Type == MoveKinds.MovJ)
{
    Console.WriteLine("This is a joint move.");
    // 这是一个关节运动。
}
else if (instruction.Type == MoveKinds.MovL)
{
    Console.WriteLine("This is a linear move.");
    // 这是一个直线运动。
}
```

---

## 8. MotionWaitOptions

**A sealed class that configures how the SDK waits for motion completion.**

**一个密封类，配置 SDK 等待运动完成的方式。**

`MotionWaitOptions` controls the polling behavior, tolerance thresholds, and timeout when waiting for a robot motion to complete. You can customize these parameters to suit different precision and responsiveness requirements.

`MotionWaitOptions` 控制等待机器人运动完成时的轮询行为、容差阈值和超时时间。您可以自定义这些参数以适应不同的精度和响应要求。

### Properties / 属性

| Property | Type | Default | Description / 描述 |
|----------|------|---------|---------------------|
| `Timeout` | `TimeSpan` | 60 seconds | Maximum time to wait for motion completion / 等待运动完成的最长时间 |
| `PollInterval` | `TimeSpan` | 50 ms | Interval between each poll to check motion status / 检查运动状态的轮询间隔 |
| `CriStaleTimeout` | `TimeSpan` | 500 ms | Maximum age of CRI data before considered stale / CRI 数据被视为过期的最长时间 |
| `SettledSamples` | `int` | 3 | Number of consecutive settled samples required to confirm motion is complete / 确认运动完成所需的连续稳定采样数 |
| `JointToleranceDeg` | `double` | 0.2 | Joint position tolerance in degrees / 关节位置容差（度） |
| `CartesianPositionToleranceMm` | `double` | 1.0 | Cartesian position tolerance in millimeters / 笛卡尔位置容差（毫米） |
| `CartesianOrientationToleranceDeg` | `double` | 1.0 | Cartesian orientation tolerance in degrees / 笛卡尔姿态容差（度） |

### Example / 示例

```csharp
// Use default wait options (most common)
// 使用默认等待选项（最常见）
await robot.Move(MoveInstruction.MovJ(jp, speed: 40, acc: 100));

// Customize wait options for high-precision motion
// 为高精度运动自定义等待选项
var preciseWait = new MotionWaitOptions
{
    Timeout = TimeSpan.FromSeconds(120),
    PollInterval = TimeSpan.FromMilliseconds(20),
    SettledSamples = 5,
    JointToleranceDeg = 0.05,
    CartesianPositionToleranceMm = 0.2,
    CartesianOrientationToleranceDeg = 0.5,
};

await robot.Move(MoveInstruction.MovL(cp, speed: 50, acc: 200), preciseWait);

// Use a short timeout for quick motions
// 为快速运动使用较短的超时时间
var quickWait = new MotionWaitOptions
{
    Timeout = TimeSpan.FromSeconds(10),
    PollInterval = TimeSpan.FromMilliseconds(30),
};

await robot.Move(MoveInstruction.MovJ(jp, speed: 100, acc: 300), quickWait);

// Fire-and-forget: set a very long timeout to effectively not wait
// 发送即忘：设置很长的超时时间以实现不等待效果
var longWait = new MotionWaitOptions
{
    Timeout = TimeSpan.FromHours(1),
};
```

---

## 8b. Synchronous (Blocking) Motion APIs / 阻塞式运动 API

**⚠️ Prerequisite: You must call `StartCriDataPush` before using any `*Sync` method!**

**⚠️ 前置条件：使用任何 `*Sync` 方法前必须先调用 `StartCriDataPush`！**

The `*Sync` methods send motion commands and then automatically poll CRI data until the robot reaches the target. They block the calling thread until completion or timeout.

`*Sync` 方法发送运动指令后自动轮询 CRI 数据，直到机器人到达目标。它们会阻塞调用线程直到完成或超时。

### Methods / 方法

| Method | Parameters | Description / 描述 |
|--------|------------|---------------------|
| `MoveSync` | `instructions, wait?` | Blocking path execution / 阻塞式路径执行 |
| `MovJSync(JointPoint)` | `target, speed, acc, wait?, blend?, coor?, tool?, relativeBlend?` | Blocking joint move to joint target / 阻塞式关节运动到关节目标 |
| `MovJSync(CartesianPoint)` | `target, speed, acc, wait?, blend?, coor?, tool?, relativeBlend?` | Blocking joint move to Cartesian target / 阻塞式关节运动到笛卡尔目标 |
| `MovLSync(CartesianPoint)` | `target, speed, acc, wait?, blend?, coor?, tool?, relativeBlend?` | Blocking linear move to Cartesian target / 阻塞式直线运动到笛卡尔目标 |
| `MovLSync(JointPoint)` | `target, speed, acc, wait?, blend?, coor?, tool?, relativeBlend?` | Blocking linear move to joint target / 阻塞式直线运动到关节目标 |
| `MovCSync` | `middle, target, speed, acc, wait?, blend?, coor?, tool?, relativeBlend?` | Blocking circular arc move / 阻塞式圆弧运动 |
| `MovCircleSync` | `middle, target, circleNum, speed, acc, wait?, blend?, coor?, tool?, relativeBlend?` | Blocking full circle move / 阻塞式整圆运动 |
| `WaitForCriData` | `timeout` | Wait for first CRI frame / 等待首帧 CRI 数据 |

### Parameter Reference / 参数参考

| Parameter | Type | Default | Required | Description / 描述 |
|-----------|------|---------|----------|---------------------|
| `target` | `JointPoint` / `CartesianPoint` | — | Yes | Target position / 目标位置 |
| `speed` | `double` | — | Yes | Speed (mm/s for linear, deg/s for joint) / 速度（直线 mm/s，关节 deg/s） |
| `acc` | `double` | — | Yes | Acceleration / 加速度 |
| `wait` | `MotionWaitOptions?` | `null` | No | Wait options (timeout, tolerance, etc.) / 等待选项（超时、容差等） |
| `blend` | `double?` | `null` | No | Blend radius (mm). Mutually exclusive with `relativeBlend` — if both are set, `relativeBlend` is ignored. Omit for no transition / 平滑半径（mm）。与 `relativeBlend` 互斥——同时传入时 `relativeBlend` 无效。不传表示无过渡 |
| `coor` | `double[]?` | `null` | No | User coordinate frame (6 elements). `null` = omitted from command / 用户坐标系（6 个元素）。`null` 时指令中不包含该字段 |
| `tool` | `double[]?` | `null` | No | Tool coordinate frame (6 elements). `null` = omitted from command / 工具坐标系（6 个元素）。`null` 时指令中不包含该字段 |
| `relativeBlend` | `double?` | `null` | No | Relative blend ratio (0–1). Mutually exclusive with `blend` — if both are set, this is ignored / 相对平滑比（0–1）。与 `blend` 互斥——同时传入时此参数无效 |
| `instructions` | `IReadOnlyList<MoveInstruction>` | — | Yes | List of move instructions / 运动指令列表 |
| `middle` | `CartesianPoint` | — | Yes | Intermediate/via point (for `MovC`/`MovCircle`) / 中间/经过点（`MovC`/`MovCircle` 用） |
| `circleNum` | `int` | — | Yes | Number of full circles (for `MovCircleSync`) / 整圆圈数（`MovCircleSync` 用） |
| `timeout` | `double` | — | Yes | Timeout in seconds for `WaitForCriData` / `WaitForCriData` 超时时间（秒） |

### Setup / 设置

```csharp
// 1. Connect and power on / 连接并上电
var robot = new CodroidClient("192.168.8.136");
await robot.ConnectRemoteAndSwitchOn();

// 2. ⚠️ MUST start CRI data push first / 必须先启动 CRI 数据推送
await robot.StartCriDataPush("192.168.8.150", 18888);

// 3. Wait for first CRI frame / 等待首帧 CRI 数据
await robot.WaitForCriData(5.0);

// 4. Now you can use *Sync methods / 现在可以使用 *Sync 方法
robot.MovJSync(JointPoint.Degrees([0, 0, 90, 0, 90, 0]), speed: 40, acc: 100);
```

### Examples / 示例

```csharp
// Basic usage with default options / 使用默认选项的基本用法
robot.MovJSync(JointPoint.Degrees([0, 0, 90, 0, 90, 0]), speed: 40, acc: 100);
robot.MovLSync(CartesianPoint.MmDeg([400, 200, 500, 180, 0, 90]), speed: 150, acc: 500);

// Custom wait options / 自定义等待选项
var opts = new MotionWaitOptions
{
    Timeout = TimeSpan.FromSeconds(30),
    JointToleranceDeg = 0.5,
    SettledSamples = 2,
};
robot.MovJSync(target, speed: 40, acc: 100, wait: opts);

// Multi-segment path / 多段路径
var path = new List<MoveInstruction>
{
    MoveInstruction.MovJ(jp1, speed: 40, acc: 100),
    MoveInstruction.MovL(cp1, speed: 150, acc: 500),
    MoveInstruction.MovL(jp2, speed: 150, acc: 500),
};
robot.MoveSync(path);

// Circular arc / 圆弧
robot.MovCSync(middle, target, speed: 120, acc: 400);
robot.MovCircleSync(middle, target, circleNum: 1, speed: 120, acc: 400);
```

### Error Handling / 错误处理

The `*Sync` methods throw exceptions in these cases:

`*Sync` 方法在以下情况抛出异常：

- `TimeoutException` - Motion timed out / 运动超时
- `InvalidOperationException` - Abnormal state (collision, emergency stop, alarm) / 异常状态（碰撞、急停、报警）
- `InvalidOperationException` - Motion stopped but target not reached / 运动停止但未到达目标

```csharp
try
{
    robot.MovJSync(target, speed: 40, acc: 100);
    Console.WriteLine("Reached target");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Timeout: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

---

## 9. RobotJogParameters

**A sealed class defining parameters for robot jog (manual) movements.**

**一个密封类，定义机器人点动（手动）运动的参数。**

`RobotJogParameters` specifies the jog mode (joint or linear), speed, axis index, and coordinate frame for manual robot jogging operations.

`RobotJogParameters` 指定手动点动操作的点动模式（关节或直线）、速度、轴索引和坐标系。

### Properties / 属性

| Property | Type | Description / 描述 |
|----------|------|---------------------|
| `Mode` | `RobotJogMode` | Jog mode: Joint or Linear / 点动模式：关节或直线 |
| `Speed` | `double` | Jog speed (deg/s for joint, mm/s for linear) / 点动速度（关节 deg/s，直线 mm/s） |
| `Index` | `int` | Axis index (0–5 for joint mode) / 轴索引（关节模式为 0–5） |
| `CoorType` | `RobotJogFrameType` | Coordinate frame type: User or Tool / 坐标系类型：用户或工具 |
| `CoorId` | `int` | Coordinate frame ID / 坐标系 ID |

### Factory Method / 工厂方法

| Method | Description / 描述 |
|--------|---------------------|
| `RobotJogParameters.Create(RobotJogMode mode, double speed, int index, RobotJogFrameType frame, int coorId)` | Create jog parameters / 创建点动参数 |

### Example / 示例

```csharp
// Jog joint 0 at 20 deg/s in user coordinate frame 0
// 在用户坐标系 0 中以 20 deg/s 点动关节 0
var jogParams = RobotJogParameters.Create(
    mode: RobotJogMode.Joint,
    speed: 20,
    index: 0,
    frame: RobotJogFrameType.User,
    coorId: 0
);
await robot.Jog(jogParams);

// Jog linear along X axis at 50 mm/s in tool coordinate frame 1
// 在工具坐标系 1 中以 50 mm/s 沿 X 轴直线点动
var linearJog = RobotJogParameters.Create(
    mode: RobotJogMode.Linear,
    speed: 50,
    index: 0,  // X axis / X 轴
    frame: RobotJogFrameType.Tool,
    coorId: 1
);
await robot.Jog(linearJog);
```

---

## 10. RobotJogMode

**An enum specifying the jog motion mode.**

**一个枚举，指定点动运动模式。**

### Values / 值

| Name | Value | Description / 描述 |
|------|-------|---------------------|
| `Joint` | 1 | Jog individual joints / 点动单个关节 |
| `Linear` | 2 | Jog linearly in Cartesian space / 在笛卡尔空间中直线点动 |

### Example / 示例

```csharp
// Switch between joint and linear jog modes
// 在关节和直线点动模式之间切换
if (jogMode == RobotJogMode.Joint)
{
    Console.WriteLine("Joint jog mode - move individual joints.");
    // 关节点动模式 - 移动单个关节。
}
else if (jogMode == RobotJogMode.Linear)
{
    Console.WriteLine("Linear jog mode - move in Cartesian space.");
    // 直线点动模式 - 在笛卡尔空间中移动。
}
```

---

## 11. RobotJogFrameType

**An enum specifying the coordinate frame type for jog operations.**

**一个枚举，指定点动操作的坐标系类型。**

### Values / 值

| Name | Value | Description / 描述 |
|------|-------|---------------------|
| `User` | 0 | User-defined coordinate frame / 用户定义的坐标系 |
| `Tool` | 1 | Tool coordinate frame / 工具坐标系 |

### Example / 示例

```csharp
// Choose coordinate frame for jog operation
// 为点动操作选择坐标系
var jogParams = RobotJogParameters.Create(
    mode: RobotJogMode.Linear,
    speed: 30,
    index: 1,  // Y axis / Y 轴
    frame: RobotJogFrameType.User,  // Use user frame / 使用用户坐标系
    coorId: 0
);
await robot.Jog(jogParams);
```

---

## Complete Multi-Segment Path Example / 完整多段路径示例

The following example demonstrates building a complete motion program using multiple types from this API reference.

以下示例演示如何使用本 API 参考中的多个类型构建完整的运动程序。

```csharp
using CodroidCS.Sdk;

// 1. Define waypoints / 定义路径点
var homeJoint = JointPoint.Degrees(new[] { 0, 0, 90, 0, 90, 0 });
var pickJoint = JointPoint.Degrees(new[] { 30, -20, 80, 10, 60, -5 });

var refJ = robot.CriData.JointPosition;
var pickCart = CartesianPoint.MmDegWithRef(
    new[] { 450, -100, 200, 180, 0, 0 },
    refJ
);
var placeCart = CartesianPoint.MmDeg(
    new[] { 450, 100, 200, 180, 0, 0 }
);
var viaPoint = CartesianPoint.MmDeg(
    new[] { 475, 0, 250, 180, 0, 0 }
);

// 2. Build a multi-segment path / 构建多段路径
var path = new[]
{
    // Move to home with joint motion / 关节运动回原点
    MoveInstruction.MovJ(homeJoint, speed: 60, acc: 120),

    // Move to pick position with joint motion / 关节运动到拾取位置
    MoveInstruction.MovJ(pickJoint, speed: 40, acc: 100),

    // Linear approach to pick Cartesian point / 直线接近拾取笛卡尔点
    MoveInstruction.MovL(pickCart, speed: 80, acc: 300, blend: 5),

    // Arc motion from pick to place via waypoint / 经过路径点从拾取到放置的圆弧运动
    MoveInstruction.MovC(viaPoint, placeCart, speed: 100, acc: 300),

    // Return home with linear motion / 直线运动回原点
    MoveInstruction.MovL(
        CartesianPoint.MmDeg(new[] { 300, 0, 400, 180, 0, 0 }),
        speed: 150, acc: 500
    ),
};

// 3. Execute with custom wait options / 使用自定义等待选项执行
var waitOptions = new MotionWaitOptions
{
    Timeout = TimeSpan.FromSeconds(90),
    SettledSamples = 4,
    JointToleranceDeg = 0.1,
};

await robot.Move(path, waitOptions);

// 4. Verify completion / 验证完成
Console.WriteLine("Path execution complete.");
// 路径执行完成。
```
