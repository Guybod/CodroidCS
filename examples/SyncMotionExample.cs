using Codroidsdk;

namespace SyncMotionExample;

/// <summary>
/// 阻塞式运动 API 使用示例
///
/// ⚠️ 重要：使用 *Sync 方法前必须先调用 StartCriDataPush 启动 CRI 数据推送！
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

            // 2. ⚠️ 必须启动 CRI 数据推送（阻塞运动依赖 CRI 判断到达）
            Console.WriteLine("启动 CRI 数据推送...");
            await robot.StartCriDataPush(localIp, localUdpPort);
            Console.WriteLine("CRI 数据推送已启动");

            // 3. 等待首帧 CRI 数据到达
            Console.WriteLine("等待 CRI 数据...");
            await robot.WaitForCriData(5.0);
            Console.WriteLine("CRI 数据已就绪");

            // 4. 使用阻塞式运动 API
            var home = JointPoint.Degrees([0, 0, 0, 0, 0, 0]);
            var pose1 = JointPoint.Degrees([0, 0, 90, 0, 90, 0]);
            var cartP1 = CartesianPoint.MmDeg([927.511, 214.489, 486.524, 179.999, 0.0, -89.999]);

            // MovJSync - 阻塞式关节运动
            Console.WriteLine("\n[1] MovJSync(JointPoint)");
            robot.MovJSync(pose1, speed: 40, acc: 100);
            Console.WriteLine("  ✓ 到达目标");

            // MovLSync - 阻塞式直线运动
            Console.WriteLine("\n[2] MovLSync(CartesianPoint)");
            robot.MovLSync(cartP1, speed: 150, acc: 500);
            Console.WriteLine("  ✓ 到达目标");

            // 自定义等待参数
            Console.WriteLine("\n[3] 自定义 MotionWaitOptions");
            var opts = new MotionWaitOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                SettledSamples = 2
            };
            robot.MovJSync(home, speed: 40, acc: 100, wait: opts);
            Console.WriteLine("  ✓ 到达目标（自定义容差）");

            // MoveSync - 阻塞式多段路径
            Console.WriteLine("\n[4] MoveSync(多段路径)");
            var path = new List<MoveInstruction>
            {
                MoveInstruction.MovJ(pose1, speed: 40, acc: 100),
                MoveInstruction.MovL(cartP1, speed: 150, acc: 500),
                MoveInstruction.MovL(home, speed: 150, acc: 500)
            };
            robot.MoveSync(path);
            Console.WriteLine("  ✓ 全部到达");

            Console.WriteLine("\n所有阻塞运动演示完成！");
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
