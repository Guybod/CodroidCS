# Changelog

## [2.1.2] — 2026-05-22

### Added

- 机器人设置（协议 19.2~19.7）：`GetRobotParameters`、`SetDefaultPayloadId` / `SetDefaultToolId` / `SetDefaultUserCoordinateId`（0~15）、`SaveToolFrames` / `SavePayloadFrames` / `SaveUserCoordinateFrames`、`SetToolFrame` / `SetPayloadFrame` / `SetUserCoordinateFrame`（仅 1~15；id=0 不可改）。

## [2.1.1] — 2026-05-22

### Breaking

- `MoveTargetPoint` 重命名为 **`MovePoint`**（无兼容别名）。
- 运动目标须使用 **`JointPoint`** / **`CartesianPoint`**，不再支持裸 `double[]` 作 `MovJ`/`MovL` 目标。
- `MoveToTarget` 请用 **`MoveToTarget.Joint`** / **`MoveToTarget.Cartesian`** 构造。

### Added

- `JointPoint.Degrees`、`CartesianPoint.MmDeg` / `MmDegWithRef`。
- `MovePoint.FromJoint` / `FromCartesian`。
- `MoveInstruction` 静态工厂：`MovJ`、`MovL`、`MovC`、`MovCircle`。
- `CodroidClient` 单段门面：`MovJ`、`MovL`、`MovC`、`MovCircle`（`Move` 多段路径保留）。
- `MotionPointPacker`：`jp` 优先；`cp` 且 `rj` 空时默认 `[20,20,20,20,20,20]`。
- `ConsoleUtf8.InitConsoleUtf8()`（Windows 控制台 UTF-8）。

### Notes

- TCP/JSON 协议字段与单位（mm/deg）未变；仅 SDK 表达层 Breaking。

## [2.0.0]

- 初始公开 NuGet 版本；公共 API 无 `Async` 后缀；CRI 轨迹与实时下发等。
