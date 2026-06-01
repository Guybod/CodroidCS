using Codroidsdk;

namespace TrajectoryMultiSegmentExample;

/// <summary>
/// 多段轨迹拼接示例（GenerateMultiSegment）
///
/// 演示如何使用 TrajectoryGenerator.GenerateMultiSegment 生成多段轨迹，
/// 然后通过 CRI 实时控制下发到机器人。
///
/// 与 C++ TrajectoryGenerator::GenerateMultiSegment / Python TrajectoryGenerator.generate_multi_segment 对齐。
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        const string robotIp = "192.168.8.136";
        const string localIp = "192.168.8.150";
        const int localUdpPort = 18888;

        var robot = new CodroidClient(robotIp);

        try
        {
            // 1. 连接并上电
            Console.WriteLine("连接机器人...");
            await robot.ConnectRemoteAndSwitchOn();
            Console.WriteLine("已连接并上电");

            // 2. 启动 CRI 数据推送
            Console.WriteLine("启动 CRI 数据推送...");
            await robot.StartCriDataPush(localIp, localUdpPort);
            await robot.WaitForCriData(5.0);
            Console.WriteLine("CRI 数据已就绪");

            // 3. 读取当前关节角作为起点
            var current = robot.CriData.JointPosition;
            Console.WriteLine($"当前关节角: [{string.Join(", ", current.Select(v => v.ToString("F1")))}]");

            // 4. 定义多段路点（关节空间）
            var waypoints = new[]
            {
                current,                                        // 起点：当前位置
                new[] { 0.0, 0.0, 90.0, 0.0, 90.0, 0.0 },    // P1
                new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },      // P2（Home）
                new[] { 0.0, 0.0, 90.0, 0.0, 90.0, 0.0 },    // P3
            };
            Console.WriteLine($"\n路点数量: {waypoints.Length}，轨迹段数: {waypoints.Length - 1}");

            // 5. 生成多段轨迹（关节空间 + 梯形规划）
            var request = new TrajectoryRequest
            {
                Space = TrajectorySpace.Joint,
                FrequencyHz = 250,
                Profile = TrajectoryProfile.Trapezoidal,
                Speed = 60.0,           // 关节速度 60 deg/s
                Acceleration = 150.0,   // 关节加速度 150 deg/s²
            };

            var trajectory = TrajectoryGenerator.GenerateMultiSegment(waypoints, request);
            Console.WriteLine($"轨迹点数: {trajectory.Count}");
            Console.WriteLine($"总时长: {trajectory[^1].TimeSeconds:F3} s");

            // 6. 启动 CRI 实时控制并下发轨迹
            Console.WriteLine("\n启动 CRI 实时控制...");
            await robot.StartCriControl(filterType: 1, durationMs: 4, startBuffer: 5);

            // 等待进入实时控制模式
            for (int i = 0; i < 50; i++)
            {
                if (robot.CriData.Status.RtControlMode) break;
                await Task.Delay(100);
            }

            if (!robot.CriData.Status.RtControlMode)
            {
                Console.WriteLine("⚠ 未进入实时控制模式，跳过下发");
                return;
            }

            Console.WriteLine("已进入实时控制模式，开始下发轨迹...");

            // 使用 CriRealtimeDispatcher 发送 64 字节 UDP 包
            using var dispatcher = new CriRealtimeDispatcher(robotIp, 9030, convertToSi: true);
            dispatcher.SendTrajectory(trajectory, TrajectorySpace.Joint, periodMs: 4);

            Console.WriteLine("轨迹下发完成！");

            // 7. 停止 CRI 控制
            await robot.StopCriControl();
            await robot.StopCriDataPush(localIp, localUdpPort);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
        finally
        {
            robot.Disconnect();
            Console.WriteLine("已断开连接");
        }
    }
}
