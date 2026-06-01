using System;

namespace Codroid
{
    /// <summary>
    /// 将 <see cref="MovePoint"/> 规范为 <c>Robot/move</c> 下发形状（与 C++ <c>packInstruction</c> 一致）。
    /// </summary>
    internal static class MotionPointPacker
    {
        /// <summary>笛卡尔点未提供 <c>rj</c> 时的默认逆解参考关节（度）。</summary>
        public static readonly double[] DefaultReferenceJointsDeg = { 20, 20, 20, 20, 20, 20 };

        /// <summary>
        /// 打包单路点：<c>jp</c> 优先；否则 <c>cp</c> + <c>rj</c>（<c>rj</c> 空则用默认）。
        /// </summary>
        public static MovePoint Pack(MovePoint point)
        {
            Polyfills.ThrowIfNull(point);
            MotionPointValidation.ValidateExclusiveJpCp(point, nameof(point));

            if (point.Jp is { Length: > 0 })
            {
                MotionPointValidation.ValidateSix(nameof(point.Jp), point.Jp);
                return new MovePoint
                {
                    Jp = point.Jp,
                    Ep = point.Ep
                };
            }

            if (point.Cp is { Length: > 0 })
            {
                MotionPointValidation.ValidateSix(nameof(point.Cp), point.Cp);
                double[]? rj = point.Rj is { Length: > 0 } ? point.Rj : DefaultReferenceJointsDeg;
                if (point.Rj is { Length: > 0 })
                {
                    MotionPointValidation.ValidateSix(nameof(point.Rj), point.Rj);
                }

                return new MovePoint
                {
                    Cp = point.Cp,
                    Rj = rj,
                    Ep = point.Ep
                };
            }

            if (point.Ep is { Length: > 0 })
            {
                return new MovePoint { Ep = point.Ep };
            }

            throw new ArgumentException("路点至少包含 jp、cp 或 ep 之一。", nameof(point));
        }

        /// <summary>打包整条运动指令的 target / middle 路点。</summary>
        public static MoveInstruction PackInstruction(MoveInstruction instruction)
        {
            Polyfills.ThrowIfNull(instruction);
            // blend 与 relativeBlend 互斥：同时传入时 relativeBlend 不下发
            double? blend = instruction.Blend;
            double? relativeBlend = blend.HasValue ? null : instruction.RelativeBlend;
            return new MoveInstruction
            {
                Type = instruction.Type,
                CircleNum = instruction.CircleNum,
                Speed = instruction.Speed,
                Acc = instruction.Acc,
                Blend = blend,
                RelativeBlend = relativeBlend,
                TargetPoint = Pack(instruction.TargetPoint),
                MiddlePoint = instruction.MiddlePoint != null ? Pack(instruction.MiddlePoint) : null,
                Coor = instruction.Coor,
                Tool = instruction.Tool
            };
        }
    }
}
