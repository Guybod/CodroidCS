# Codroid C# SDK 版本说明

## v2.1.4（2026-05-30）

### Bug Fixes

- **修复阻塞运动欧拉角到达判定**：`180°` 和 `-180°` 是同一姿态，但之前直接算差值 `|180-(-180)|=360°`，导致判定永远不通过。现在归一化到 `[-180, 180]` 后再比较

### Breaking Change

- **`blend` 参数类型变更**：`double` → `double?`，默认值从 `25` 改为 `null`
  - 之前不传 `blend` 会自动应用 25mm 平滑过渡，现在不传表示**无过渡**
  - 如需保持旧行为，请显式传入 `blend: 25`
- **`relativeBlend` 参数类型变更**：`double?`（默认 `null`）
  - 之前不传会使用默认值，现在不传表示**不使用相对平滑**
  - 如需保持旧行为，请显式传入 `relativeBlend: 0`
- **`blend` 与 `relativeBlend` 互斥**：同时传入时 `relativeBlend` 无效
- **`coor` / `tool` 语义明确**：`null` 表示指令中不包含该字段（非"使用默认坐标系"）

### 涉及方法

- `MoveInstruction` 工厂方法：`MovJ`、`MovL`、`MovC`、`MovCircle`
- `CodroidClient`：`MovJ`、`MovL`、`MovC`、`MovCircle` 及其 `*Sync` 变体（共 12 个方法）

### 文档

- 所有 API 文档参数表同步更新，补充 `blend`/`relativeBlend` 互斥说明和 `coor`/`tool` 的 null 语义
- SDK 手册版本号升级至 v2.1.4

---

## v2.1.3（2026-05-29）

### 修复

- 修复 `AsyncTcpClient.cs` 中 `InvokePublishHandlers` 参数 `ty` 的空引用警告
- 修复 `MotionPoints.cs` 中 `ValidateSix` 方法 `values` 的空引用警告
- 修复 `CodroidIo.cs` 中 `ParseStringZeroOne` 方法 `s` 的空引用警告

---

## v2.1.2（2026-05-29）

### 新增

- **阻塞式运动 API**（Sync Motion）：
  - `MoveSync`、`MovJSync`（JointPoint / CartesianPoint）、`MovLSync`（CartesianPoint / JointPoint）、`MovCSync`、`MovCircleSync`
  - `MotionWaitOptions` 类：可配置超时、轮询间隔、CRI 过期判定、稳定采样数、关节/笛卡尔容差
  - **⚠️ 前置条件**：使用 `*Sync` 方法前必须先调用 `StartCriDataPush` 启动 CRI 数据推送
- **`StopMoveTo()`**：发送 `type=-1` 停止 MoveTo 运动
- **`WaitForCriData(timeout)`**：阻塞等待首帧 CRI 数据到达
- `MoveToType.Stop = -1` 枚举值
- **机器人设置 API（协议 19.x）**：
  - `GetRobotParameters`、`SetCollisionSensitivity`、`SetPayload`
  - `SetDefaultPayloadId`、`SetDefaultToolId`、`SetDefaultUserCoordinateId`
  - `SetToolFrame`、`SetPayloadFrame`、`SetUserCoordinateFrame`
  - `SaveToolFrames`、`SavePayloadFrames`、`SaveUserCoordinateFrames`
- **运动 API 强类型**：
  - `JointPoint`、`CartesianPoint`、`MoveInstruction` 及工厂方法
  - `MovePoint`、`MoveToTarget`
- **.NET Framework 4.6.2 支持**
- 新增示例：阻塞运动演示、机器人设置演示

### 依赖

- 控制器固件 **≥ 2.3.2.10**

---

## v2.1.1（2026-05-21）

### 功能

- 基础 TCP JSON 指令通道
- IO 接口：`GetDi`、`GetDo`、`GetAi`、`GetAo`、`SetDo`、`SetAo`
- 寄存器接口：`GetRegisterValue`、`SetRegisterValue`、`SetExtendArrayType`、`RemoveExtendArray`
- 运动控制：`MovJ`、`MovL`、`MovC`、`MovCircle`、`Move`、`PauseRobotMotion`、`ResumeRobotMotion`、`StopRobotMove`
- Jog：`StartJog`、`StopJog`、`JogHeartbeat`
- MoveTo：`MoveTo`、`MoveToHeartbeat`
- 工程/脚本：`RunScript`、`Run`、`RunByIndex`、`RunStep`、`PauseProject`、`ResumeProject`、`StopProject`
- 全局变量：`GetGlobalVars`、`SaveGlobalVar`、`SaveGlobalVars`、`RemoveGlobalVars`
- 运动学：`AposToCpos`、`CposToApos`、`CalculateRelativePose`
- CRI 实时数据：`StartCriDataPush`、`StopCriDataPush`、`StartCriControl`、`StopCriControl`
- Publish/Subscribe：`SubscribePublishTopic`
