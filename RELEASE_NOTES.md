# Codroid C# SDK 版本说明

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
