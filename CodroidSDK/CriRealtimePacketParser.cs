using System;

namespace Codroid
{
    /// <summary>
    /// CRI 实时数据 UDP 二进制包解析：六轴、无附加轴、mask=0xFFFF、高精度推送下固定 308 字节布局。
    /// </summary>
    public static class CriRealtimePacketParser
    {
        /// <summary>
        /// 当前实现所期望的 UDP 载荷长度（字节）。
        /// </summary>
        public const int PacketLength = 308;

        /// <summary>
        /// 解析后浮点数默认保留小数位数。
        /// </summary>
        public const int DefaultDecimalPlaces = 3;

        private const double RadToDeg = 180.0 / Math.PI;
        private const double MToMm = 1000.0;

        /// <summary>
        /// 将一帧固定布局的二进制 CRI 数据解析为 <see cref="CriRealTimeData"/>（关节角等为度，线位移为毫米等，见 <see cref="CriRealTimeData"/> 属性说明）。
        /// </summary>
        /// <param name="packet">原始 UDP 载荷；长度必须等于 <see cref="PacketLength"/>。</param>
        /// <returns>填充完毕的实时数据对象。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="packet"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="packet"/> 长度不是 <see cref="PacketLength"/>。</exception>
        public static CriRealTimeData Parse(byte[] packet)
        {
            ArgumentNullException.ThrowIfNull(packet);
            if (packet.Length != PacketLength)
            {
                throw new ArgumentException($"CRI 包长度必须为 {PacketLength} 字节，实际为 {packet.Length}。", nameof(packet));
            }

            int offset = 0;
            var data = new CriRealTimeData
            {
                TimestampMs = ReadInt64(packet, ref offset),
                Status1Raw = ReadUInt16(packet, ref offset),
                Status2Raw = ReadUInt16(packet, ref offset)
            };

            ParseStatus1(data, data.Status1Raw);
            ParseStatus2(data, data.Status2Raw);

            data.JointPosition = ConvertArray(ReadDoubleArray(packet, ref offset, 6), RadToDeg);
            data.JointVelocity = ConvertArray(ReadDoubleArray(packet, ref offset, 6), RadToDeg);
            data.TcpPose = ConvertPoseMmDeg(ReadDoubleArray(packet, ref offset, 6));
            data.TcpVelocity = ConvertPoseMmDeg(ReadDoubleArray(packet, ref offset, 6));
            data.TcpLinearVelocity = ReadDouble(packet, ref offset) * MToMm;
            data.JointOutputTorque = ReadDoubleArray(packet, ref offset, 6);
            data.JointExternalForce = ReadDoubleArray(packet, ref offset, 6);
            data.ExternalAxisPosition = Array.Empty<double>();
            ApplyDecimalPlaces(data, DefaultDecimalPlaces);
            return data;
        }

        private static void ParseStatus1(CriRealTimeData data, ushort raw)
        {
            data.ProjectRunning = (raw & (1 << 0)) != 0;
            data.ProjectStopped = (raw & (1 << 1)) != 0;
            data.ProjectPaused = (raw & (1 << 2)) != 0;
            data.Enabling = (raw & (1 << 3)) != 0;
            data.NotEnabled = (raw & (1 << 4)) != 0;
            data.ManualMode = (raw & (1 << 5)) != 0;
            data.Dragging = (raw & (1 << 6)) != 0;
            data.InMotion = (raw & (1 << 7)) != 0;

            data.CollisionStopped = (raw & (1 << 8)) != 0;
            data.InSafetyPosition = (raw & (1 << 9)) != 0;
            data.HasAlarm = (raw & (1 << 10)) != 0;
            data.SimulationMode = (raw & (1 << 11)) != 0;
            data.EmergencyStopPressed = (raw & (1 << 12)) != 0;
            data.RescueMode = (raw & (1 << 13)) != 0;
            data.AutoMode = (raw & (1 << 14)) != 0;
            data.RemoteMode = (raw & (1 << 15)) != 0;
        }

        private static void ParseStatus2(CriRealTimeData data, ushort raw)
        {
            data.RealTimeControlMode = (raw & (1 << 0)) != 0;
            data.CriErrorCode = (byte)((raw >> 8) & 0xFF);
        }

        private static ushort ReadUInt16(byte[] packet, ref int offset)
        {
            ushort value = BitConverter.ToUInt16(packet, offset);
            offset += sizeof(ushort);
            return value;
        }

        private static long ReadInt64(byte[] packet, ref int offset)
        {
            long value = BitConverter.ToInt64(packet, offset);
            offset += sizeof(long);
            return value;
        }

        private static double ReadDouble(byte[] packet, ref int offset)
        {
            double value = BitConverter.ToDouble(packet, offset);
            offset += sizeof(double);
            return value;
        }

        private static double[] ReadDoubleArray(byte[] packet, ref int offset, int count)
        {
            var values = new double[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ReadDouble(packet, ref offset);
            }
            return values;
        }

        private static double[] ConvertArray(double[] source, double scale)
        {
            var output = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                output[i] = source[i] * scale;
            }
            return output;
        }

        private static void ApplyDecimalPlaces(CriRealTimeData data, int decimals)
        {
            data.JointPosition = RoundArrayToDecimals(data.JointPosition, decimals);
            data.JointVelocity = RoundArrayToDecimals(data.JointVelocity, decimals);
            data.TcpPose = RoundArrayToDecimals(data.TcpPose, decimals);
            data.TcpVelocity = RoundArrayToDecimals(data.TcpVelocity, decimals);
            data.TcpLinearVelocity = RoundToDecimals(data.TcpLinearVelocity, decimals);
            data.JointOutputTorque = RoundArrayToDecimals(data.JointOutputTorque, decimals);
            data.JointExternalForce = RoundArrayToDecimals(data.JointExternalForce, decimals);
            data.ExternalAxisPosition = RoundArrayToDecimals(data.ExternalAxisPosition, decimals);
        }

        private static double[] RoundArrayToDecimals(double[] values, int decimals)
        {
            var rounded = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                rounded[i] = RoundToDecimals(values[i], decimals);
            }
            return rounded;
        }

        private static double RoundToDecimals(double value, int decimals)
        {
            if (value == 0.0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }

            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            return rounded == 0.0 ? 0.0 : rounded;
        }

        private static double[] ConvertPoseMmDeg(double[] source)
        {
            var output = (double[])source.Clone();
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = i < 3 ? output[i] * MToMm : output[i] * RadToDeg;
            }
            return output;
        }
    }
}
