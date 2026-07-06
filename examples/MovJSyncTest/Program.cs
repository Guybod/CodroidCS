using Codroid;

// =============================================================================
// MovJSync + 容差前置判断 测试示例
// =============================================================================
// 功能：测试笛卡尔目标 + 参考关节角的 MovJSync 调用，启用容差前置判断。
// 场景：目标和当前位置非常接近时（如微调），避免因机器人不运动而超时。
//
// 运行方式：
//   cd CodroidCS/examples/MovJSyncTest
//   dotnet run
// =============================================================================

const string robotIp = "192.168.8.136";
const string localIp = "192.168.8.150";
const int localUdpPort = 18888;

var robot = new CodroidClient(robotIp);

try
{
    // =========================================================================
    // 1. 连接并上电
    // =========================================================================
    Console.WriteLine("连接机器人...");
    await robot.ConnectRemoteAndSwitchOn();
    Console.WriteLine("✓ 已连接并上电");

    // =========================================================================
    // 2. 启动 CRI 数据推送（MovJSync 需要 CRI 数据判断是否停稳）
    // =========================================================================
    Console.WriteLine("启动 CRI 数据推送...");
    await robot.StartCriDataPush(localIp, localUdpPort);
    Console.WriteLine("✓ CRI 数据推送已启动");

    // =========================================================================
    // 3. 等待首帧 CRI 数据到达
    // =========================================================================
    Console.WriteLine("等待 CRI 数据...");
    await Task.Delay(2000);
    Console.WriteLine("✓ CRI 数据已就绪\n");

    // =========================================================================
    // 4. 测试 MovJSync + MmDegWithRef + 容差前置判断
    // =========================================================================

    // 目标位姿：X, Y, Z, C, B, A（单位：mm + 度）
    double[] pose = new double[] { 327.511, 112.89, 305.884, -179.832, 0.275, -89.691 };

    // 参考关节角：用于引导 IK 求解器找到正确的关节解
    // 实际使用时应替换为目标位姿对应的关节角
    double[] refJoints = new double[] { 0, 0, 90, 0, 90, 0 };

    Console.WriteLine($"目标位姿: [{string.Join(", ", pose)}]");
    Console.WriteLine($"参考关节: [{string.Join(", ", refJoints)}]");

    // 构造目标点（等同于 C# 的 CartesianPoint.MmDegWithRef）
    var target = CartesianPoint.MmDegWithRef(pose, refJoints);

    // =========================================================================
    // 5. 配置 MotionWaitOptions（启用容差前置判断）
    // =========================================================================
    var waitOptions = new MotionWaitOptions
    {
        // 整体超时（秒）
        Timeout = TimeSpan.FromSeconds(30),

        // 轮询间隔（毫秒）
        PollInterval = TimeSpan.FromMilliseconds(50),

        // 连续稳定采样数（InMotion=false 连续 N 次算停稳）
        SettledSamples = 3,

        // 【关键】启用容差前置判断
        // 当目标和当前位置在容差范围内时，直接返回 true，不等 InMotion
        UseTolerance = true,

        // 关节容差（度）：关节角误差在此范围内视为到达
        JointToleranceDeg = 0.5,

        // 笛卡尔位置容差（mm）：XYZ 欧氏距离在此范围内视为到达
        CartesianPositionToleranceMm = 1.0,

        // 姿态容差（度）：RPY 角度误差在此范围内视为到达
        CartesianOrientationToleranceDeg = 1.0,

        // 【关键】启动超时（秒）：等待 InMotion 变为 true 的超时
        // 如果机器人在该时间内从未启动运动（InMotion 一直为 false），直接报错
        // 避免用户等待完整超时（60 秒）才发现机器人没动
        MotionStartTimeout = TimeSpan.FromSeconds(1),
    };

    Console.WriteLine("\n容差配置：");
    Console.WriteLine($"  UseTolerance = {waitOptions.UseTolerance}");
    Console.WriteLine($"  JointToleranceDeg = {waitOptions.JointToleranceDeg}");
    Console.WriteLine($"  CartesianPositionToleranceMm = {waitOptions.CartesianPositionToleranceMm}");
    Console.WriteLine($"  CartesianOrientationToleranceDeg = {waitOptions.CartesianOrientationToleranceDeg}");
    Console.WriteLine($"  MotionStartTimeout = {waitOptions.MotionStartTimeout.TotalSeconds}s");

    // =========================================================================
    // 6. 执行 MovJSync（带容差前置判断）
    // =========================================================================
    Console.WriteLine("\n执行 MovJSync...");
    bool mFlag = robot.MovJSync(target, speed: 50, acc: 500, wait: waitOptions);
    Console.WriteLine($"✓ MovJSync 完成，结果: {mFlag}");

    // =========================================================================
    // 7. 读取当前位置验证
    // =========================================================================
    await Task.Delay(1000);
    if (robot.CriData != null)
    {
        Console.WriteLine($"\n实际 TCP 位姿: [{string.Join(", ", robot.CriData.TcpPose)}]");
        Console.WriteLine($"实际关节角: [{string.Join(", ", robot.CriData.JointPosition)}]");

        // 计算误差
        double posErr = Math.Sqrt(
            Math.Pow(robot.CriData.TcpPose[0] - pose[0], 2) +
            Math.Pow(robot.CriData.TcpPose[1] - pose[1], 2) +
            Math.Pow(robot.CriData.TcpPose[2] - pose[2], 2));
        Console.WriteLine($"位置误差: {posErr:F3} mm");
    }

    // =========================================================================
    // 8. 下电
    // =========================================================================
    Console.WriteLine("\n下电...");
    await robot.ToAuto();
    await robot.ToManual();
    await Task.Delay(1000);
    await robot.SwitchOff();
    Console.WriteLine("✓ 已退出");
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
}
finally
{
    robot.Disconnect();
}
