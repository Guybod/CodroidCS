using System;

namespace Codroid
{
    /// <summary>
    /// 六轴关节目标点（度）。用于 <c>movJ</c> 关节目标或 <c>movL</c> 关节目标。
    /// </summary>
    public sealed class JointPoint
    {
        /// <summary>六轴关节角，单位：度。</summary>
        public double[] Jp { get; init; } = null!;

        /// <summary>由六轴关节角（度）构造关节点。</summary>
        /// <param name="jointsDeg">长度必须为 6。</param>
        public static JointPoint Degrees(double[] jointsDeg)
        {
            MotionPointValidation.ValidateSix(nameof(jointsDeg), jointsDeg);
            return new JointPoint { Jp = (double[])jointsDeg.Clone() };
        }
    }

    /// <summary>
    /// TCP 位姿目标（mm + 度）。用于 <c>movJ</c>/<c>movL</c> 笛卡尔目标及圆弧段。
    /// </summary>
    public sealed class CartesianPoint
    {
        /// <summary>TCP 位姿 <c>[x,y,z,rx,ry,rz]</c>，前三 mm、后三度。</summary>
        public double[] Cp { get; init; } = null!;

        /// <summary>逆解参考关节角（度）；打包时若为空则使用默认 <c>[20,20,20,20,20,20]</c>。</summary>
        public double[]? Rj { get; init; }

        /// <summary>仅设 TCP 位姿；下发时 <c>rj</c> 为空则使用默认参考关节。</summary>
        public static CartesianPoint MmDeg(double[] poseMmDeg)
        {
            MotionPointValidation.ValidateSix(nameof(poseMmDeg), poseMmDeg);
            return new CartesianPoint { Cp = (double[])poseMmDeg.Clone() };
        }

        /// <summary>设 TCP 位姿与逆解参考关节（推荐 movJ/movL 到 TCP 时使用）。</summary>
        public static CartesianPoint MmDegWithRef(double[] poseMmDeg, double[] refJointsDeg)
        {
            MotionPointValidation.ValidateSix(nameof(poseMmDeg), poseMmDeg);
            MotionPointValidation.ValidateSix(nameof(refJointsDeg), refJointsDeg);
            return new CartesianPoint
            {
                Cp = (double[])poseMmDeg.Clone(),
                Rj = (double[])refJointsDeg.Clone()
            };
        }
    }

    /// <summary>运动点数组长度与互斥校验。</summary>
    internal static class MotionPointValidation
    {
        public const int AxisCount = 6;

        public static void ValidateSix(string name, double[]? values)
        {
            ArgumentNullException.ThrowIfNull(values, name);
            if (values.Length != AxisCount)
            {
                throw new ArgumentException($"{name} 长度必须为 {AxisCount}。", name);
            }
        }

        public static void ValidateExclusiveJpCp(MovePoint point, string paramName)
        {
            ArgumentNullException.ThrowIfNull(point, paramName);
            bool hasJp = point.Jp is { Length: > 0 };
            bool hasCp = point.Cp is { Length: > 0 };
            if (hasJp && hasCp)
            {
                throw new ArgumentException("同一路点不能同时设置 jp 与 cp。", paramName);
            }
        }
    }
}
