using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Codroid
{

/// <summary>
/// 控制器通过 TCP 返回的通用 JSON 响应结构（字段名与协议一致：id、ty、db、err）。
/// </summary>
public class CommonResponse
{
    /// <summary>
    /// 与请求对应的报文序号（协议字段 id）。
    /// </summary>
    public object? id { get; set; }
    /// <summary>
    /// 响应类型或路由标识（协议字段 ty）。
    /// </summary>
    public string? ty { get; set; }
    /// <summary>
    /// 业务数据载荷；结构随接口变化，使用 <see cref="JsonElement"/> 便于按需读取。
    /// </summary>
    public JsonElement db { get; set; }
    /// <summary>
    /// 非空时表示控制器报告的错误信息；此时调用方应将本次调用视为失败。
    /// </summary>
    public string? err { get; set; }
}


/// <summary>
/// CRI 实时数据快照：由 UDP 二进制包解析得到，关节角类量为度，线位移为毫米（详见各属性说明）。
/// </summary>
public class CriRealTimeData
{
    /// <summary>
    /// 时间戳（毫秒，含义以控制器协议为准）。
    /// </summary>
    public long TimestampMs { get; set; }
    /// <summary>
    /// 状态字 1 的原始 16 位值（解析后见各类布尔状态属性）。
    /// </summary>
    public ushort Status1Raw { get; set; }
    /// <summary>
    /// 状态字 2 的原始 16 位值（含实时控制模式位与错误码高字节）。
    /// </summary>
    public ushort Status2Raw { get; set; }

    /// <summary>工程正在运行。</summary>
    public bool ProjectRunning { get; set; }
    /// <summary>工程已停止。</summary>
    public bool ProjectStopped { get; set; }
    /// <summary>工程已暂停。</summary>
    public bool ProjectPaused { get; set; }
    /// <summary>正在使能过程中。</summary>
    public bool Enabling { get; set; }
    /// <summary>未处于使能就绪状态。</summary>
    public bool NotEnabled { get; set; }
    /// <summary>手动模式。</summary>
    public bool ManualMode { get; set; }
    /// <summary>拖拽（示教）相关状态有效。</summary>
    public bool Dragging { get; set; }
    /// <summary>机构运动中。</summary>
    public bool InMotion { get; set; }

    /// <summary>碰撞检测导致停止。</summary>
    public bool CollisionStopped { get; set; }
    /// <summary>处于安全位置。</summary>
    public bool InSafetyPosition { get; set; }
    /// <summary>存在报警。</summary>
    public bool HasAlarm { get; set; }
    /// <summary>仿真模式。</summary>
    public bool SimulationMode { get; set; }
    /// <summary>急停已按下。</summary>
    public bool EmergencyStopPressed { get; set; }
    /// <summary>救援模式。</summary>
    public bool RescueMode { get; set; }
    /// <summary>自动模式。</summary>
    public bool AutoMode { get; set; }
    /// <summary>远程模式。</summary>
    public bool RemoteMode { get; set; }

    /// <summary>CRI 错误码（取自状态字 2 的高 8 位）。</summary>
    public byte CriErrorCode { get; set; }
    /// <summary>实时控制模式有效。</summary>
    public bool RealTimeControlMode { get; set; }

    /// <summary>六轴关节位置（度）。</summary>
    public double[] JointPosition { get; set; } = new double[6];
    /// <summary>六轴关节角速度（度/秒）。</summary>
    public double[] JointVelocity { get; set; } = new double[6];
    /// <summary>TCP 位姿：前三维 x,y,z 为毫米；后三维 rx,ry,rz 为度。</summary>
    public double[] TcpPose { get; set; } = new double[6];
    /// <summary>TCP 速度：线速度分量为毫米/秒；角速度分量为度/秒。</summary>
    public double[] TcpVelocity { get; set; } = new double[6];
    /// <summary>TCP 线速度标量（毫米/秒）。</summary>
    public double TcpLinearVelocity { get; set; }
    /// <summary>关节输出力矩（控制器原始单位，由协议定义）。</summary>
    public double[] JointOutputTorque { get; set; } = new double[6];
    /// <summary>关节外力（控制器原始单位，由协议定义）。</summary>
    public double[] JointExternalForce { get; set; } = new double[6];
    /// <summary>附加轴位置；当前解析配置为六轴无附加轴时通常为空数组。</summary>
    public double[] ExternalAxisPosition { get; set; } = Array.Empty<double>();

    /// <summary>
    /// 用另一快照覆盖当前实例的所有字段（数组为深拷贝引用新数组）。
    /// </summary>
    /// <param name="source">作为数据源的实时数据对象，不可为 null。</param>
    public void UpdateFrom(CriRealTimeData source)
    {
        TimestampMs = source.TimestampMs;
        Status1Raw = source.Status1Raw;
        Status2Raw = source.Status2Raw;
        ProjectRunning = source.ProjectRunning;
        ProjectStopped = source.ProjectStopped;
        ProjectPaused = source.ProjectPaused;
        Enabling = source.Enabling;
        NotEnabled = source.NotEnabled;
        ManualMode = source.ManualMode;
        Dragging = source.Dragging;
        InMotion = source.InMotion;
        CollisionStopped = source.CollisionStopped;
        InSafetyPosition = source.InSafetyPosition;
        HasAlarm = source.HasAlarm;
        SimulationMode = source.SimulationMode;
        EmergencyStopPressed = source.EmergencyStopPressed;
        RescueMode = source.RescueMode;
        AutoMode = source.AutoMode;
        RemoteMode = source.RemoteMode;
        CriErrorCode = source.CriErrorCode;
        RealTimeControlMode = source.RealTimeControlMode;
        JointPosition = (double[])source.JointPosition.Clone();
        JointVelocity = (double[])source.JointVelocity.Clone();
        TcpPose = (double[])source.TcpPose.Clone();
        TcpVelocity = (double[])source.TcpVelocity.Clone();
        TcpLinearVelocity = source.TcpLinearVelocity;
        JointOutputTorque = (double[])source.JointOutputTorque.Clone();
        JointExternalForce = (double[])source.JointExternalForce.Clone();
        ExternalAxisPosition = (double[])source.ExternalAxisPosition.Clone();
    }

    /// <summary>
    /// 创建当前数据的深拷贝，便于在回调或跨线程传递时避免共享可变数组。
    /// </summary>
    /// <returns>与当前字段值一致的新 <see cref="CriRealTimeData"/> 实例。</returns>
    public CriRealTimeData Clone()
    {
        return new CriRealTimeData
        {
            TimestampMs = TimestampMs,
            Status1Raw = Status1Raw,
            Status2Raw = Status2Raw,
            ProjectRunning = ProjectRunning,
            ProjectStopped = ProjectStopped,
            ProjectPaused = ProjectPaused,
            Enabling = Enabling,
            NotEnabled = NotEnabled,
            ManualMode = ManualMode,
            Dragging = Dragging,
            InMotion = InMotion,
            CollisionStopped = CollisionStopped,
            InSafetyPosition = InSafetyPosition,
            HasAlarm = HasAlarm,
            SimulationMode = SimulationMode,
            EmergencyStopPressed = EmergencyStopPressed,
            RescueMode = RescueMode,
            AutoMode = AutoMode,
            RemoteMode = RemoteMode,
            CriErrorCode = CriErrorCode,
            RealTimeControlMode = RealTimeControlMode,
            JointPosition = (double[])JointPosition.Clone(),
            JointVelocity = (double[])JointVelocity.Clone(),
            TcpPose = (double[])TcpPose.Clone(),
            TcpVelocity = (double[])TcpVelocity.Clone(),
            TcpLinearVelocity = TcpLinearVelocity,
            JointOutputTorque = (double[])JointOutputTorque.Clone(),
            JointExternalForce = (double[])JointExternalForce.Clone(),
            ExternalAxisPosition = (double[])ExternalAxisPosition.Clone()
        };
    }

}
}
