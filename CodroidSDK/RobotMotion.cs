using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS1591 // 公共 API 含义见机器人运动控制协议 11.x；此处不逐成员重复 XML

namespace Codroid
{
    /// <summary>
    /// 点动模式：<c>1</c> 关节点动，<c>2</c> 直线点动（与协议 <c>Robot/jog</c> 一致）。
    /// </summary>
    public enum RobotJogMode
    {
        Joint = 1,
        Linear = 2
    }

    /// <summary>
    /// 点动所用坐标系：<c>0</c> 用户坐标系，<c>1</c> 工具坐标系。
    /// </summary>
    public enum RobotJogFrameType
    {
        User = 0,
        Tool = 1
    }

    /// <summary>
    /// <c>Robot/jog</c> 请求体 <c>db</c> 的字段集合。
    /// </summary>
    public sealed class RobotJogParameters
    {
        public int Mode { get; init; }
        public double Speed { get; init; }
        public int Index { get; init; }
        public int CoorType { get; init; }
        public int CoorId { get; init; }

        public static RobotJogParameters Create(
            RobotJogMode mode,
            double speed,
            int index,
            RobotJogFrameType frame,
            int coorId) =>
            new()
            {
                Mode = (int)mode,
                Speed = speed,
                Index = index,
                CoorType = (int)frame,
                CoorId = coorId
            };
    }

    /// <summary>
    /// <c>Robot/moveTo</c> 的 <c>type</c> 取值。
    /// </summary>
    public enum MoveToKind
    {
        Home = 0,
        Safe = 1,
        Candle = 2,
        Pack = 3,
        JointPlanned = 4,
        LinePlanned = 5,
        ProgramResume = 6
    }

    /// <summary>
    /// <c>moveTo</c> 中 <c>target</c>；仅在 <see cref="MoveToKind.JointPlanned"/> 或 <see cref="MoveToKind.LinePlanned"/> 时使用。
    /// </summary>
    public sealed class MoveToTarget
    {
        public double[]? Cp { get; init; }
        public double[]? Jp { get; init; }
        public double[]? Ep { get; init; }
    }

    /// <summary>
    /// 点动 / moveTo 心跳建议间隔（毫秒），与文档 0.5s 一致。
    /// </summary>
    public static class RobotMotionHeartbeat
    {
        public const int RecommendedIntervalMilliseconds = 500;
    }

    /// <summary>
    /// 运动指令类型字符串（<c>Robot/move</c> 中 <c>type</c>）。
    /// </summary>
    public static class MoveKinds
    {
        public const string MovJ = "movJ";
        public const string MovL = "movL";
        public const string MovC = "movC";
        public const string MovCircle = "movCircle";
    }

    /// <summary>
    /// <c>Robot/move</c> 单条指令中的 <c>targetPoint</c> / <c>middlePoint</c>。
    /// </summary>
    public sealed class MoveTargetPoint
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Jp { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Cp { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Rj { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Ep { get; init; }
    }

    /// <summary>
    /// <c>Robot/move</c> 单条运动指令。不要设置空的 <c>coor</c>/<c>tool</c> 数组，否则后端可能崩溃。
    /// </summary>
    public sealed class MoveInstruction
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = MoveKinds.MovJ;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CircleNum { get; init; }

        public double Speed { get; init; }
        public double Acc { get; init; }
        public double Blend { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RelativeBlend { get; init; }

        public MoveTargetPoint TargetPoint { get; init; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MoveTargetPoint? MiddlePoint { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Coor { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[]? Tool { get; init; }
    }

    /// <summary>
    /// 运动相关参数校验。
    /// </summary>
    public static class RobotMotionValidation
    {
        public static void ValidateJog(RobotJogParameters p)
        {
            ArgumentNullException.ThrowIfNull(p);
            if (p.Speed is < -1.0 or > 1.0)
            {
                throw new ArgumentException("点动 speed 须在 [-1, 1] 范围内。", nameof(p));
            }

            if (p.Index is < 1 or > 6)
            {
                throw new ArgumentException("点动 index 须在 1~6（关节轴 1~6 或直线 xyzabc）。", nameof(p));
            }
        }

        public static void ValidateMoveRatePercent(int percent)
        {
            if (percent is < 1 or > 100)
            {
                throw new ArgumentException("倍率须在 1~100。", nameof(percent));
            }
        }

        /// <summary>
        /// <c>Robot/setCollisionSensitivity</c> 的灵敏度参数校验（0~100）。
        /// </summary>
        public static void ValidateCollisionSensitivity(int sensitivity)
        {
            if (sensitivity is < 0 or > 100)
            {
                throw new ArgumentException("碰撞检测灵敏度须在 0~100。", nameof(sensitivity));
            }
        }

        /// <summary>
        /// <c>Robot/setPayload</c> 的负载编号校验（0~15）。
        /// </summary>
        public static void ValidatePayloadId(int payloadId)
        {
            if (payloadId is < 0 or > 15)
            {
                throw new ArgumentException("负载编号须在 0~15。", nameof(payloadId));
            }
        }

        /// <summary>
        /// 协议说明：不要传入空的 <c>coor</c>/<c>tool</c> 数组。
        /// </summary>
        public static void ValidateNonEmptyFrame(string name, double[]? frame)
        {
            if (frame == null)
            {
                return;
            }

            if (frame.Length == 0)
            {
                throw new ArgumentException(
                    $"不要传入空的 {name} 数组（已知会导致后端异常）。请省略该字段或传入有效 6 维坐标。",
                    nameof(frame));
            }
        }

        public static void ValidateMoveInstruction(MoveInstruction instruction)
        {
            ArgumentNullException.ThrowIfNull(instruction);
            ArgumentNullException.ThrowIfNull(instruction.TargetPoint);

            ValidateNonEmptyFrame(nameof(instruction.Coor), instruction.Coor);
            ValidateNonEmptyFrame(nameof(instruction.Tool), instruction.Tool);

            var t = instruction.Type;
            if (string.Equals(t, MoveKinds.MovC, StringComparison.Ordinal)
                || string.Equals(t, MoveKinds.MovCircle, StringComparison.Ordinal))
            {
                if (instruction.MiddlePoint == null)
                {
                    throw new ArgumentException($"{t} 必须提供 MiddlePoint。", nameof(instruction));
                }
            }

            if (string.Equals(t, MoveKinds.MovC, StringComparison.Ordinal)
                && instruction.TargetPoint.Cp == null)
            {
                throw new ArgumentException("movC 目标须使用笛卡尔点 targetPoint.cp。", nameof(instruction));
            }
        }
    }

    /// <summary>
    /// <c>Robot/move</c> 指令列表序列化选项：camelCase、忽略 null，避免多余字段。
    /// </summary>
    public static class MotionCommandJson
    {
        public static readonly JsonSerializerOptions MoveInstructionOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// 将指令列表序列化为 JSON 数组根节点，供作为请求 <c>db</c> 发送。
        /// </summary>
        public static JsonElement SerializeMoveInstructions(IReadOnlyList<MoveInstruction> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);
            if (commands.Count == 0)
            {
                throw new ArgumentException("至少提供一条运动指令。", nameof(commands));
            }

            foreach (var c in commands)
            {
                RobotMotionValidation.ValidateMoveInstruction(c);
            }

            var json = JsonSerializer.Serialize(commands, MoveInstructionOptions);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }
}
