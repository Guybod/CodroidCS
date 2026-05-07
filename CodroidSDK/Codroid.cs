using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

namespace Codroid
{
    /// <summary>
    /// Codroid 机器人控制器客户端：通过 TCP（默认端口 9001）发送 JSON 指令，并可接收 CRI 实时数据 UDP 推送。
    /// </summary>
    public class CodroidClient
    {
        private string _ip;
        private int _port;
        private FutureTcpClient _TcpClient;
        private int _id;
        private readonly object _criDataLock = new();
        private readonly CriRealTimeData _criData = new();
        private UdpClient? _criUdpClient;
        private CancellationTokenSource? _criUdpCts;
        private Task? _criUdpTask;

        private const ushort CriMaskFixed = 0xFFFF;
        private const bool CriHighPrecisionFixed = true;
        private const int CriDurationMsFixed = 100;

        /// <summary>
        /// 每收到一帧合法长度的 CRI UDP 包并完成解析后触发；参数为当前数据的线程安全快照（克隆）。
        /// </summary>
        public event Action<CriRealTimeData>? CriDataReceived;

        /// <summary>
        /// 使用指定控制器地址构造客户端；TCP 端口固定为 9001。
        /// </summary>
        /// <param name="ip">控制器的 IPv4 地址字符串（也用于 CRI UDP 本地绑定解析）。</param>
        public CodroidClient(string ip)
        {
            _ip = ip;
            _port = 9001;
            _TcpClient = new FutureTcpClient();
            _id = 0;
        }

        /// <summary>
        /// 生成单调递增的请求序号，用于匹配 JSON 响应。
        /// </summary>
        /// <returns>新的正整数 id。</returns>
        private int NextId() => Interlocked.Increment(ref _id);

        /// <summary>
        /// 线程安全地读取当前 CRI 实时数据的克隆副本；适合短时取用，避免长期持有内部缓冲区引用。
        /// </summary>
        /// <value>当前实时数据的深拷贝。</value>
        public CriRealTimeData CriData
        {
            get
            {
                lock (_criDataLock)
                {
                    return _criData.Clone();
                }
            }
        }

        /// <summary>
        /// 后台 UDP 接收线程持续更新的同一实例；直接读取需注意并发，必要时结合锁或仅用只读快照属性 <see cref="CriData"/>。
        /// </summary>
        /// <value>内部实时数据对象的直接引用。</value>
        public CriRealTimeData Data => _criData;

        /// <summary>
        /// 与控制器建立 TCP 连接（地址为构造时传入的 IP，端口 9001）。
        /// </summary>
        /// <returns>表示异步连接操作的任务。</returns>
        public async Task Connect()
        {
            await _TcpClient.ConnectAsync(_ip, _port);
        }

        /// <summary>
        /// 建立 TCP 后：先 <see cref="EnterRemoteModeViaAutoAsync"/>（自动→远程），再 <see cref="SwitchOn"/> 上电/使能。
        /// 仅建立连接请用 <see cref="Connect"/>。
        /// </summary>
        /// <exception cref="TimeoutException">某步指令等待超时。</exception>
        /// <exception cref="CodroidCommandException">控制器返回错误。</exception>
        public async Task ConnectRemoteAndSwitchOnAsync()
        {
            await Connect();
            await EnterRemoteModeViaAutoAsync();
            await SwitchOn();
        }

        /// <summary>
        /// 订阅 TCP 主题推送（协议 15.x）：<paramref name="topicTy"/> 与下行报文 <c>ty</c> 一致（如 <see cref="PublishTopics.RobotStatus"/>）。
        /// 首次在本连接上订阅该主题时发送 <c>ty</c>、<c>tc</c> 帧（<b>不含</b> <c>id</c>），<b>不等待</b>控制器响应；之后凡<b>无整数</b> <c>id</c> 的 JSON 将按 <c>ty</c> 分发给 <paramref name="handler"/>（在线程池执行）。
        /// </summary>
        /// <param name="topicTy">主题名，例如 <c>publish/VarUpdate</c>。</param>
        /// <param name="handler">自行解析 <see cref="PublishNotification.Db"/> 或 <see cref="PublishNotification.RawJson"/>；请勿长时间阻塞。</param>
        /// <param name="tcMilliseconds">协议字段 <c>tc</c>（毫秒）；默认 <c>100</c>。实际推送仍取决于数据是否变化。</param>
        /// <returns>释放以取消回调；<b>不会</b>向控制器发「退订」报文。TCP 断开后订阅与注册均失效，须重连后再次调用本方法。</returns>
        public async Task<PublishTopicSubscription> SubscribePublishTopicAsync(
            string topicTy,
            Action<PublishNotification> handler,
            int tcMilliseconds = PublishSubscribeDefaults.TcMilliseconds)
        {
            if (string.IsNullOrEmpty(topicTy))
            {
                throw new ArgumentException("主题 ty 不能为空。", nameof(topicTy));
            }

            ArgumentNullException.ThrowIfNull(handler);
            await _TcpClient.RegisterPublishHandlerAndSubscribeAsync(topicTy, handler, tcMilliseconds);
            return new PublishTopicSubscription(_TcpClient, topicTy, handler);
        }

        /// <summary>
        /// 请求进入远程脚本模式（指令：<c>Robot/enterRemoteScriptMode</c>）。
        /// </summary>
        /// <returns>控制器返回的 <see cref="CommonResponse"/>（业务数据在 <see cref="CommonResponse.db"/>）。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器在 <c>err</c> 中报告错误，或其它执行失败（见 <see cref="Exception.Message"/> 与 <see cref="CodroidCommandException.ControllerError"/>）。</exception>
        public async Task<CommonResponse> EnterRemoteScriptMode()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/enterRemoteScriptMode", new { });
        }

        /// <summary>
        /// 按工程 ID 启动运行（指令：<c>project/run</c>）。
        /// </summary>
        /// <param name="projectID">工程标识字符串。</param>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> Run(string projectID)
        {
            int currentId = NextId();
            var data = new { id = projectID };
            return await _TcpClient.SendCommand(currentId, "project/run", data);
        }

        /// <summary>
        /// 按工程列表索引启动运行（指令：<c>project/runByIndex</c>）。
        /// </summary>
        /// <param name="index">工程在非负整数索引。</param>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> RunByIndex(int index)
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "project/runByIndex", index);
        }

        /// <summary>
        /// 按工程 ID 单步运行（指令：<c>project/runStep</c>）。
        /// </summary>
        /// <param name="projectID">工程标识字符串。</param>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> RunStep(string projectID)
        {
            int currentId = NextId();
            var data = new { id = projectID };
            return await _TcpClient.SendCommand(currentId, "project/runStep", data);
        }

        /// <summary>
        /// 暂停当前工程（指令：<c>project/pause</c>）。
        /// </summary>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> PauseProject()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "project/pause", new { });
        }

        /// <summary>
        /// 恢复已暂停的工程（指令：<c>project/resume</c>）。
        /// </summary>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> ResumeProject()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "project/resume", new { });
        }

        /// <summary>
        /// 停止当前工程（指令：<c>project/stop</c>）。
        /// </summary>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> StopProject()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "project/stop", new { });
        }

        /// <summary>
        /// 读取全局变量列表（指令：<c>globalVar/getVars</c>）。
        /// </summary>
        /// <returns>控制器返回的响应对象（具体结构见 <see cref="CommonResponse.db"/>）。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> GetGlobalVars()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "globalVar/getVars", new { });
        }

        /// <summary>
        /// 获取所有全局变量并解析为字典（指令：<c>globalVar/getVars</c>），等价于 <see cref="GetGlobalVars"/> 后对 <see cref="GlobalVarCatalogParser.Parse"/> 的封装。
        /// </summary>
        /// <returns>变量名到条目（值、备注）的只读映射。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<IReadOnlyDictionary<string, GlobalVarCatalogEntry>> GetGlobalVarsCatalogAsync()
        {
            var resp = await GetGlobalVars();
            return GlobalVarCatalogParser.Parse(resp);
        }

        /// <summary>
        /// 增量保存全局变量（指令：<c>globalVar/saveVars</c>）；同名变量会更新值与备注（若本次提供了备注）。
        /// </summary>
        /// <param name="name">变量名，须符合 <see cref="GlobalVarNaming.Validate"/>。</param>
        /// <param name="value">变量值：数值、字符串、数组、字典等会经 JSON 序列化写入 <c>val</c>；已拼好的 JSON 文本请用 <see cref="GlobalVarRawJson"/> 包装。</param>
        /// <param name="remark">备注，可为中文；null 或空白则不发送 <c>nm</c>。</param>
        /// <returns>控制器响应。</returns>
        /// <exception cref="ArgumentException">变量名非法。</exception>
        public Task<CommonResponse> SaveGlobalVar(string name, object value, string? remark = null)
        {
            return SaveGlobalVars(new[] { new GlobalVarSaveItem(name, value, remark) });
        }

        /// <summary>
        /// 批量增量保存全局变量（指令：<c>globalVar/saveVars</c>）。
        /// </summary>
        /// <param name="items">一项或多条保存说明；批次内变量名不得重复。</param>
        /// <returns>控制器响应。</returns>
        /// <exception cref="ArgumentException">项为空、变量名非法或批次内重名。</exception>
        public async Task<CommonResponse> SaveGlobalVars(IReadOnlyCollection<GlobalVarSaveItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (items.Count == 0)
            {
                throw new ArgumentException("至少需要提供一项变量。", nameof(items));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var db = new Dictionary<string, Dictionary<string, object>>();

            foreach (var item in items)
            {
                GlobalVarNaming.Validate(item.Name);
                if (!seen.Add(item.Name))
                {
                    throw new ArgumentException($"批次中存在重复的变量名: {item.Name}", nameof(items));
                }

                var wireVal = GlobalVarValueFormatter.ToWireString(item.Value);
                var body = new Dictionary<string, object> { ["val"] = wireVal };
                if (!string.IsNullOrEmpty(item.Remark))
                {
                    body["nm"] = item.Remark!;
                }

                db[item.Name] = body;
            }

            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "globalVar/saveVars", db);
        }

        /// <summary>
        /// 删除指定全局变量（指令：<c>globalVar/removeVars</c>）；删除不存在的变量不会报错。
        /// </summary>
        /// <param name="names">要删除的变量名列表，每个名字须符合 <see cref="GlobalVarNaming.Validate"/>。</param>
        /// <returns>控制器响应。</returns>
        /// <exception cref="ArgumentException">列表为空或某变量名非法。</exception>
        public async Task<CommonResponse> RemoveGlobalVars(IEnumerable<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);
            var arr = names as string[] ?? names.ToArray();
            if (arr.Length == 0)
            {
                throw new ArgumentException("至少提供一个变量名。", nameof(names));
            }

            foreach (var n in arr)
            {
                GlobalVarNaming.Validate(n);
            }

            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "globalVar/removeVars", arr);
        }

        /// <summary>
        /// 机器人上电 / 使能打开（指令：<c>Robot/switchOn</c>）。
        /// </summary>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> SwitchOn()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/switchOn", new { });
        }

        /// <summary>
        /// 机器人下电 / 使能关闭（指令：<c>Robot/switchOff</c>，<c>db</c> 为空字符串）。
        /// </summary>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        public async Task<CommonResponse> SwitchOff()
        {
            return await SendCommandEmptyDbAsync("Robot/switchOff");
        }

        /// <summary>
        /// 进入手动模式（指令：<c>Robot/toManual</c>）。需固件 2.3.2.6+；不能从远程模式直接跳入，须先经自动模式（见 <see cref="EnterManualModeViaAutoAsync"/>）。
        /// </summary>
        public Task<CommonResponse> ToManualAsync() => SendCommandEmptyDbAsync("Robot/toManual");

        /// <summary>
        /// 进入自动模式（指令：<c>Robot/toAuto</c>）。需固件 2.3.2.6+。
        /// </summary>
        public Task<CommonResponse> ToAutoAsync() => SendCommandEmptyDbAsync("Robot/toAuto");

        /// <summary>
        /// 进入远程模式（指令：<c>Robot/toRemote</c>）。需固件 2.3.2.6+；不能从手动模式直接跳入，须先经自动模式（见 <see cref="EnterRemoteModeViaAutoAsync"/>）。
        /// </summary>
        public Task<CommonResponse> ToRemoteAsync() => SendCommandEmptyDbAsync("Robot/toRemote");

        /// <summary>
        /// 先 <see cref="ToAutoAsync"/> 再 <see cref="ToManualAsync"/>，用于在远程与手动之间切换时满足控制器「必须先切自动」的限制。
        /// </summary>
        /// <returns>最后一次（进入手动）请求的响应。</returns>
        public async Task<CommonResponse> EnterManualModeViaAutoAsync()
        {
            await ToAutoAsync();
            return await ToManualAsync();
        }

        /// <summary>
        /// 先 <see cref="ToAutoAsync"/> 再 <see cref="ToRemoteAsync"/>，用于在手动与远程之间切换时满足控制器「必须先切自动」的限制。
        /// </summary>
        /// <returns>最后一次（进入远程）请求的响应。</returns>
        public async Task<CommonResponse> EnterRemoteModeViaAutoAsync()
        {
            await ToAutoAsync();
            return await ToRemoteAsync();
        }

        /// <summary>
        /// 进入仿真模式（指令：<c>Robot/toSimulation</c>）。
        /// </summary>
        public Task<CommonResponse> ToSimulationAsync() => SendCommandEmptyDbAsync("Robot/toSimulation");

        /// <summary>
        /// 进入实机模式（指令：<c>Robot/toActual</c>）。
        /// </summary>
        public Task<CommonResponse> ToActualAsync() => SendCommandEmptyDbAsync("Robot/toActual");

        /// <summary>
        /// 进入拖拽模式（指令：<c>Robot/startDrag</c>）。需固件 2.3.2.6+；仅远程或手动模式下可用。
        /// </summary>
        public Task<CommonResponse> StartDragAsync() => SendCommandEmptyDbAsync("Robot/startDrag");

        /// <summary>
        /// 退出拖拽模式（指令：<c>Robot/stopDrag</c>）。需固件 2.3.2.6+。
        /// </summary>
        public Task<CommonResponse> StopDragAsync() => SendCommandEmptyDbAsync("Robot/stopDrag");

        /// <summary>
        /// 清除错误（指令：<c>System/clearError</c>）。
        /// </summary>
        public Task<CommonResponse> ClearSystemErrorAsync() => SendCommandEmptyDbAsync("System/clearError");

        /// <summary>
        /// 发送 <c>db</c> 为空字符串的 JSON 指令（与协议示例一致）。
        /// </summary>
        private Task<CommonResponse> SendCommandEmptyDbAsync(string type) =>
            _TcpClient.SendCommand(NextId(), type, string.Empty);

        /// <summary>
        /// 批量查询 IO 当前值（指令：<c>IOManager/GetIOValue</c>）；结果在 <see cref="CommonResponse.db"/> 数组中。
        /// </summary>
        public async Task<CommonResponse> GetIoValuesAsync(IReadOnlyList<(string Type, int Port)> pins)
        {
            var db = IoGetResponseParser.BuildGetQuery(pins);
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "IOManager/GetIOValue", db);
        }

        /// <summary>
        /// 读取数字量输入 DI，返回 <c>0</c> 或 <c>1</c>。
        /// </summary>
        public async Task<int> GetDiAsync(int port)
        {
            var resp = await GetIoValuesAsync(new[] { (IoPortKind.Di, port) });
            return IoGetResponseParser.ParseDigital(resp, IoPortKind.Di, port);
        }

        /// <summary>
        /// 读取数字量输出 DO 当前状态，返回 <c>0</c> 或 <c>1</c>。
        /// </summary>
        public async Task<int> GetDoAsync(int port)
        {
            var resp = await GetIoValuesAsync(new[] { (IoPortKind.Do, port) });
            return IoGetResponseParser.ParseDigital(resp, IoPortKind.Do, port);
        }

        /// <summary>
        /// 读取模拟量输入 AI，返回浮点值。
        /// </summary>
        public async Task<double> GetAiAsync(int port)
        {
            var resp = await GetIoValuesAsync(new[] { (IoPortKind.Ai, port) });
            return IoGetResponseParser.ParseAnalog(resp, IoPortKind.Ai, port);
        }

        /// <summary>
        /// 读取模拟量输出 AO 当前值，返回浮点值。
        /// </summary>
        public async Task<double> GetAoAsync(int port)
        {
            var resp = await GetIoValuesAsync(new[] { (IoPortKind.Ao, port) });
            return IoGetResponseParser.ParseAnalog(resp, IoPortKind.Ao, port);
        }

        /// <summary>
        /// 写入数字量输出 DO（指令：<c>IOManager/SetIOValue</c>），<paramref name="value"/> 只能为 <c>0</c> 或 <c>1</c>。
        /// </summary>
        public async Task<CommonResponse> SetDoAsync(int port, int value)
        {
            if (value is not (0 or 1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "DO 只能写入 0 或 1。");
            }

            int currentId = NextId();
            var db = new { type = IoPortKind.Do, port, value };
            return await _TcpClient.SendCommand(currentId, "IOManager/SetIOValue", db);
        }

        /// <summary>
        /// 写入模拟量输出 AO（指令：<c>IOManager/SetIOValue</c>）。
        /// </summary>
        public async Task<CommonResponse> SetAoAsync(int port, double value)
        {
            int currentId = NextId();
            var db = new { type = IoPortKind.Ao, port, value };
            return await _TcpClient.SendCommand(currentId, "IOManager/SetIOValue", db);
        }

        /// <summary>
        /// 读取单个寄存器（指令：<c>RegisterManager/GetRegisterValue</c>）。
        /// 返回值使用 <see cref="RegisterReadValue.GetInt32"/> 或 <see cref="RegisterReadValue.GetDouble"/> 按实际类型读取。
        /// </summary>
        public async Task<RegisterReadValue> GetRegisterValueAsync(int address)
        {
            IReadOnlyList<RegisterReadValue> batch = await GetRegisterValuesAsync(new[] { address });
            return batch[0];
        }

        /// <summary>
        /// 批量读取寄存器（指令：<c>RegisterManager/GetRegisterValue</c>）；返回数组顺序与 <paramref name="addresses"/> 一致。
        /// </summary>
        public async Task<IReadOnlyList<RegisterReadValue>> GetRegisterValuesAsync(IReadOnlyList<int> addresses)
        {
            RegisterValidation.ValidateAddresses(addresses);
            int currentId = NextId();
            int[] db = addresses.ToArray();
            CommonResponse resp = await _TcpClient.SendCommand(currentId, "RegisterManager/GetRegisterValue", db);
            return RegisterResponseParser.ParseAligned(resp, addresses);
        }

        /// <summary>
        /// 写入寄存器整型值（指令：<c>RegisterManager/SetRegisterValue</c>）。
        /// </summary>
        public async Task<CommonResponse> SetRegisterValueAsync(int address, int value)
        {
            int currentId = NextId();
            var db = new { address, value };
            return await _TcpClient.SendCommand(currentId, "RegisterManager/SetRegisterValue", db);
        }

        /// <summary>
        /// 写入寄存器浮点值（指令：<c>RegisterManager/SetRegisterValue</c>）。
        /// </summary>
        public async Task<CommonResponse> SetRegisterValueAsync(int address, double value)
        {
            int currentId = NextId();
            var db = new { address, value };
            return await _TcpClient.SendCommand(currentId, "RegisterManager/SetRegisterValue", db);
        }

        /// <summary>
        /// 设置扩展数组元素数据类型（指令：<c>RegisterManager/setExtendArrayType</c>）；<paramref name="index"/> 为 0~999，<paramref name="type"/> 见 <see cref="RegisterExtendArrayValueType"/>。
        /// </summary>
        public async Task<CommonResponse> SetExtendArrayTypeAsync(int index, string type)
        {
            RegisterValidation.ValidateExtendIndex(index);
            RegisterValidation.ValidateExtendType(type);
            int currentId = NextId();
            var db = new { index, type };
            return await _TcpClient.SendCommand(currentId, "RegisterManager/setExtendArrayType", db);
        }

        /// <summary>
        /// 删除扩展数组指定索引并重置数据（指令：<c>RegisterManager/removeExtendArray</c>）；<paramref name="index"/> 为 0~999。
        /// </summary>
        public async Task<CommonResponse> RemoveExtendArrayAsync(int index)
        {
            RegisterValidation.ValidateExtendIndex(index);
            int currentId = NextId();
            var db = new { index };
            return await _TcpClient.SendCommand(currentId, "RegisterManager/removeExtendArray", db);
        }

        /// <summary>
        /// 关节空间 → 笛卡尔空间正解（指令：<c>Robot/apostocpos</c>）。
        /// </summary>
        /// <param name="jointDegrees">六轴关节角，单位：度。</param>
        /// <param name="userFrame">用户坐标系 [x,y,z,rx,ry,rz]，单位：毫米、度（与协议一致）。</param>
        /// <param name="toolFrame">工具坐标系，同上。</param>
        /// <param name="externalAxisPositions">附加轴位置 <c>ep</c>；无附加轴时传 null 或空数组。</param>
        /// <returns>控制器响应；笛卡尔结果为 <see cref="CommonResponse.db"/> 中长度为 6 的数组。</returns>
        /// <exception cref="ArgumentException">向量长度不是 6。</exception>
        /// <exception cref="InvalidOperationException">未连接等（见 <see cref="FutureTcpClient.SendCommand"/>）。</exception>
        /// <exception cref="TimeoutException">等待响应超时。</exception>
        /// <exception cref="CodroidCommandException">控制器报错。</exception>
        public async Task<CommonResponse> AposToCposAsync(
            double[] jointDegrees,
            double[] userFrame,
            double[] toolFrame,
            double[]? externalAxisPositions = null)
        {
            RobotKinematics.RequireVector6(nameof(jointDegrees), jointDegrees);
            RobotKinematics.RequireVector6(nameof(userFrame), userFrame);
            RobotKinematics.RequireVector6(nameof(toolFrame), toolFrame);
            var ep = externalAxisPositions ?? Array.Empty<double>();

            int currentId = NextId();
            var db = new
            {
                jp = jointDegrees,
                coor = userFrame,
                tool = toolFrame,
                ep
            };
            return await _TcpClient.SendCommand(currentId, "Robot/apostocpos", db);
        }

        /// <summary>
        /// 正解并解析 <c>db</c> 为六维笛卡尔位姿（单位：毫米、度，与控制器约定一致）。
        /// </summary>
        /// <exception cref="InvalidOperationException"><c>db</c> 无法解析为 6 维数组。</exception>
        public async Task<double[]> AposToCposPoseAsync(
            double[] jointDegrees,
            double[] userFrame,
            double[] toolFrame,
            double[]? externalAxisPositions = null)
        {
            var resp = await AposToCposAsync(jointDegrees, userFrame, toolFrame, externalAxisPositions);
            return RobotKinematics.ParseDbAsVector6(resp.db);
        }

        /// <summary>
        /// 笛卡尔空间 → 关节空间逆解（指令：<c>Robot/cpostoapos</c>）。
        /// </summary>
        /// <param name="cartesianMmDeg">末端位姿 [x,y,z,rx,ry,rz]，线位移毫米、姿态度。</param>
        /// <param name="referenceJointDegrees">参考关节角（度）；无解时可尝试修改该组角。</param>
        /// <param name="externalAxisPositions">可选附加轴 <c>ep</c>；无则 null 或空数组。</param>
        /// <returns>控制器响应；关节角在 <see cref="CommonResponse.db"/> 中（度）。</returns>
        /// <exception cref="ArgumentException">向量长度不是 6。</exception>
        /// <exception cref="TimeoutException">等待响应超时。</exception>
        /// <exception cref="CodroidCommandException">控制器报错。</exception>
        public async Task<CommonResponse> CposToAposAsync(
            double[] cartesianMmDeg,
            double[] referenceJointDegrees,
            double[]? externalAxisPositions = null)
        {
            RobotKinematics.RequireVector6(nameof(cartesianMmDeg), cartesianMmDeg);
            RobotKinematics.RequireVector6(nameof(referenceJointDegrees), referenceJointDegrees);
            var ep = externalAxisPositions ?? Array.Empty<double>();

            int currentId = NextId();
            var db = new
            {
                cp = cartesianMmDeg,
                rj = referenceJointDegrees,
                ep
            };
            return await _TcpClient.SendCommand(currentId, "Robot/cpostoapos", db);
        }

        /// <summary>
        /// 逆解并解析 <c>db</c> 为六轴关节角（度）。若控制器返回空数组则抛异常，请调整 <paramref name="referenceJointDegrees"/>。
        /// </summary>
        /// <exception cref="InvalidOperationException">返回空数组或无法解析。</exception>
        public async Task<double[]> CposToAposJointsAsync(
            double[] cartesianMmDeg,
            double[] referenceJointDegrees,
            double[]? externalAxisPositions = null)
        {
            var resp = await CposToAposAsync(cartesianMmDeg, referenceJointDegrees, externalAxisPositions);
            return RobotKinematics.ParseDbAsVector6(resp.db);
        }

        /// <summary>
        /// 笛卡尔相对位姿/偏移计算（指令：<c>Robot/calculateRelativePose</c>）。
        /// </summary>
        /// <param name="tcpPoseWorld">当前末端 TCP 在世界系下的位姿 [x,y,z,a,b,c]，毫米与度。</param>
        /// <param name="offset">偏移 [x,y,z,a,b,c]，毫米与度。</param>
        /// <param name="coorType">在工具系或用户系下施加偏移。</param>
        /// <param name="tcpPoseInPosCoorFrame">
        /// 可选。当前末端在「posCoor 坐标系」下的位姿；传入后结果也在该坐标系下解释（见控制器文档）。
        /// </param>
        /// <param name="userCoorFrame">
        /// 当 <paramref name="coorType"/> 为 <see cref="RelativePoseCoorType.User"/> 时可选，偏移所用用户坐标系；默认协议为世界系。
        /// </param>
        /// <returns>控制器响应；偏移后位姿在 <see cref="CommonResponse.db"/> 中。</returns>
        /// <exception cref="ArgumentException">向量长度不是 6。</exception>
        /// <exception cref="TimeoutException">等待响应超时。</exception>
        /// <exception cref="CodroidCommandException">控制器报错。</exception>
        public async Task<CommonResponse> CalculateRelativePoseAsync(
            double[] tcpPoseWorld,
            double[] offset,
            RelativePoseCoorType coorType,
            double[]? tcpPoseInPosCoorFrame = null,
            double[]? userCoorFrame = null)
        {
            RobotKinematics.RequireVector6(nameof(tcpPoseWorld), tcpPoseWorld);
            RobotKinematics.RequireVector6(nameof(offset), offset);
            if (tcpPoseInPosCoorFrame != null)
            {
                RobotKinematics.RequireVector6(nameof(tcpPoseInPosCoorFrame), tcpPoseInPosCoorFrame);
            }

            if (userCoorFrame != null)
            {
                RobotKinematics.RequireVector6(nameof(userCoorFrame), userCoorFrame);
            }

            var wireType = RobotKinematics.ToWireCoorType(coorType);
            var db = new Dictionary<string, object>
            {
                ["pos"] = tcpPoseWorld,
                ["offset"] = offset,
                ["coorType"] = wireType
            };

            if (tcpPoseInPosCoorFrame != null)
            {
                db["posCoor"] = tcpPoseInPosCoorFrame;
            }

            if (coorType == RelativePoseCoorType.User && userCoorFrame != null)
            {
                db["coor"] = userCoorFrame;
            }

            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/calculateRelativePose", db);
        }

        /// <summary>
        /// 相对位姿计算并解析结果为六维位姿（毫米、度）。
        /// </summary>
        /// <exception cref="InvalidOperationException"><c>db</c> 无法解析为 6 维数组。</exception>
        public async Task<double[]> CalculateRelativePoseResultAsync(
            double[] tcpPoseWorld,
            double[] offset,
            RelativePoseCoorType coorType,
            double[]? tcpPoseInPosCoorFrame = null,
            double[]? userCoorFrame = null)
        {
            var resp = await CalculateRelativePoseAsync(
                tcpPoseWorld,
                offset,
                coorType,
                tcpPoseInPosCoorFrame,
                userCoorFrame);
            return RobotKinematics.ParseDbAsVector6(resp.db);
        }

        /// <summary>
        /// 启动点动（指令：<c>Robot/jog</c>）。启动后须每约 <see cref="RobotMotionHeartbeat.RecommendedIntervalMilliseconds"/> ms 调用 <see cref="JogHeartbeatAsync"/>。
        /// </summary>
        public async Task<CommonResponse> StartJogAsync(RobotJogParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            RobotMotionValidation.ValidateJog(parameters);
            int currentId = NextId();
            var db = new
            {
                mode = parameters.Mode,
                speed = parameters.Speed,
                index = parameters.Index,
                coorType = parameters.CoorType,
                coorId = parameters.CoorId
            };
            return await _TcpClient.SendCommand(currentId, "Robot/jog", db);
        }

        /// <summary>
        /// 停止点动（指令：<c>Robot/stopJog</c>）。
        /// </summary>
        public async Task<CommonResponse> StopJogAsync()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/stopJog", string.Empty);
        }

        /// <summary>
        /// 点动心跳（指令：<c>Robot/jogHeartbeat</c>），维持点动状态。
        /// </summary>
        public async Task<CommonResponse> JogHeartbeatAsync()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/jogHeartbeat", string.Empty);
        }

        /// <summary>
        /// 运动到预设/规划位置（指令：<c>Robot/moveTo</c>）。类型为 <see cref="MoveToKind.JointPlanned"/> 或 <see cref="MoveToKind.LinePlanned"/> 时必须提供 <paramref name="target"/>。
        /// 启动后须每约 <see cref="RobotMotionHeartbeat.RecommendedIntervalMilliseconds"/> ms 调用 <see cref="MoveToHeartbeatAsync"/>。
        /// </summary>
        public async Task<CommonResponse> MoveToAsync(MoveToKind kind, MoveToTarget? target = null)
        {
            int currentId = NextId();
            object db;
            if (kind is MoveToKind.JointPlanned or MoveToKind.LinePlanned)
            {
                if (target == null)
                {
                    throw new ArgumentException(
                        "type 为 JointPlanned(4) 或 LinePlanned(5) 时必须提供 target。",
                        nameof(target));
                }

                var t = new Dictionary<string, object>();
                if (target.Cp != null)
                {
                    t["cp"] = target.Cp;
                }

                if (target.Jp != null)
                {
                    t["jp"] = target.Jp;
                }

                if (target.Ep != null)
                {
                    t["ep"] = target.Ep;
                }

                if (t.Count == 0)
                {
                    throw new ArgumentException("target 至少包含 cp、jp 或 ep 之一。", nameof(target));
                }

                db = new { type = (int)kind, target = t };
            }
            else
            {
                db = new { type = (int)kind };
            }

            return await _TcpClient.SendCommand(currentId, "Robot/moveTo", db);
        }

        /// <summary>
        /// moveTo 心跳（指令：<c>Robot/moveToHeartbeat</c>），维持 RunTo/moveTo 运动。
        /// </summary>
        public async Task<CommonResponse> MoveToHeartbeatAsync()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/moveToHeartbeat", string.Empty);
        }

        /// <summary>
        /// 设置手动运动倍率（指令：<c>Robot/setManualMoveRate</c>），<paramref name="percent"/> 为 1~100。
        /// </summary>
        public async Task<CommonResponse> SetManualMoveRateAsync(int percent)
        {
            RobotMotionValidation.ValidateMoveRatePercent(percent);
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/setManualMoveRate", percent);
        }

        /// <summary>
        /// 设置自动运动倍率（指令：<c>Robot/setAutoMoveRate</c>），<paramref name="percent"/> 为 1~100。
        /// </summary>
        public async Task<CommonResponse> SetAutoMoveRateAsync(int percent)
        {
            RobotMotionValidation.ValidateMoveRatePercent(percent);
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/setAutoMoveRate", percent);
        }

        /// <summary>
        /// 设置碰撞检测灵敏度（指令：<c>Robot/setCollisionSensitivity</c>）。需固件 2.3.2.10+；<paramref name="sensitivity"/> 为 0~100；成功时响应 <c>db</c> 为布尔。
        /// </summary>
        public async Task<CommonResponse> SetCollisionSensitivityAsync(int sensitivity)
        {
            RobotMotionValidation.ValidateCollisionSensitivity(sensitivity);
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/setCollisionSensitivity", sensitivity);
        }

        /// <summary>
        /// 设置负载（指令：<c>Robot/setPayload</c>）。需固件 2.3.2.10+；<paramref name="payloadId"/> 为 0~15；成功时响应 <c>db</c> 可能为 null。
        /// </summary>
        public async Task<CommonResponse> SetPayloadAsync(int payloadId)
        {
            RobotMotionValidation.ValidatePayloadId(payloadId);
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/setPayload", payloadId);
        }

        /// <summary>
        /// 下发运动指令列表（指令：<c>Robot/move</c>）。不要设置空的 <c>coor</c>/<c>tool</c> 数组。
        /// </summary>
        public async Task<CommonResponse> MoveAsync(IReadOnlyList<MoveInstruction> instructions)
        {
            ArgumentNullException.ThrowIfNull(instructions);
            JsonElement payload = MotionCommandJson.SerializeMoveInstructions(instructions);
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/move", payload);
        }

        /// <summary>
        /// 暂停当前运动（指令：<c>Robot/pause</c>）；与工程级 <c>project/pause</c> 不同。
        /// </summary>
        public async Task<CommonResponse> PauseRobotMotionAsync()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/pause", string.Empty);
        }

        /// <summary>
        /// 恢复运动（指令：<c>Robot/resume</c>）。
        /// </summary>
        public async Task<CommonResponse> ResumeRobotMotionAsync()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/resume", string.Empty);
        }

        /// <summary>
        /// 停止运动（指令：<c>Robot/stopMove</c>）。
        /// </summary>
        public async Task<CommonResponse> StopRobotMoveAsync()
        {
            int currentId = NextId();
            return await _TcpClient.SendCommand(currentId, "Robot/stopMove", string.Empty);
        }

        /// <summary>
        /// 在本地指定地址与端口启动 UDP 监听，并向控制器请求开始向该端点推送 CRI 实时数据（指令：<c>CRI/StartDataPush</c>）。
        /// 推送参数在本客户端内固定为：周期 100 ms、高精度、mask 0xFFFF，对应 308 字节 UDP 包（六轴、无附加轴）。
        /// </summary>
        /// <param name="udpIp">本机用于绑定与告知控制器的 IPv4 地址（须与控制器路由可达）。</param>
        /// <param name="udpPort">本机 UDP 监听端口。</param>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        /// <remarks>若 TCP 指令失败，会在抛出异常前关闭已启动的本地 UDP 监听。</remarks>
        public async Task<CommonResponse> StartCriDataPush(string udpIp, int udpPort)
        {
            var localIp = IPAddress.Parse(udpIp);
            await StartCriUdpListener(localIp, udpPort);

            int currentId = NextId();
            var data = new
            {
                ip = udpIp,
                port = udpPort,
                duration = CriDurationMsFixed,
                highPercision = CriHighPrecisionFixed,
                mask = CriMaskFixed
            };

            try
            {
                return await _TcpClient.SendCommand(currentId, "CRI/StartDataPush", data);
            }
            catch
            {
                StopCriUdpListener();
                throw;
            }
        }

        /// <summary>
        /// 请求控制器停止 CRI 数据推送（指令：<c>CRI/StopDataPush</c>），并始终关闭本地 UDP 监听。
        /// </summary>
        /// <param name="udpIp">可选；若与 <paramref name="udpPort"/> 同时提供，则作为停止推送的目标地址参数传入协议。</param>
        /// <param name="udpPort">可选；与 <paramref name="udpIp"/> 配对使用。</param>
        /// <returns>控制器返回的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接 TCP，或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">等待响应超时（10 秒）。</exception>
        /// <exception cref="CodroidCommandException">控制器报错或其它执行失败。</exception>
        /// <remarks>无论 TCP 指令是否成功，本地 UDP 监听都会在方法结束时关闭。</remarks>
        public async Task<CommonResponse> StopCriDataPush(string? udpIp = null, int? udpPort = null)
        {
            int currentId = NextId();
            object data = (string.IsNullOrEmpty(udpIp) || !udpPort.HasValue)
                ? new { }
                : new { ip = udpIp, port = udpPort.Value };

            try
            {
                return await _TcpClient.SendCommand(currentId, "CRI/StopDataPush", data);
            }
            finally
            {
                StopCriUdpListener();
            }
        }

        /// <summary>
        /// 停止已有 UDP 任务后，在指定本地终结点上绑定 <see cref="UdpClient"/> 并启动后台接收循环。
        /// </summary>
        /// <param name="localIp">本机绑定 IP。</param>
        /// <param name="localPort">本机绑定端口。</param>
        /// <returns>表示监听初始化完成的任务。</returns>
        private async Task StartCriUdpListener(IPAddress localIp, int localPort)
        {
            StopCriUdpListener();

            _criUdpCts = new CancellationTokenSource();
            _criUdpClient = new UdpClient(new IPEndPoint(localIp, localPort));
            _criUdpTask = Task.Run(() => CriUdpReceiveLoop(_criUdpCts.Token));
            await Task.CompletedTask;
        }

        /// <summary>
        /// 取消 UDP 接收、释放套接字与相关任务引用。
        /// </summary>
        private void StopCriUdpListener()
        {
            try
            {
                _criUdpCts?.Cancel();
                _criUdpClient?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRI UDP关闭异常] {ex.Message}");
            }
            finally
            {
                _criUdpClient = null;
                _criUdpCts?.Dispose();
                _criUdpCts = null;
                _criUdpTask = null;
            }
        }

        /// <summary>
        /// 循环接收 UDP 数据报；仅处理长度等于 <see cref="CriRealtimePacketParser.PacketLength"/> 的包，解析后更新内部缓存并触发 <see cref="CriDataReceived"/>。
        /// </summary>
        /// <param name="token">取消令牌；取消时退出循环。</param>
        /// <returns>表示接收循环生命周期的任务。</returns>
        private async Task CriUdpReceiveLoop(CancellationToken token)
        {
            if (_criUdpClient == null)
            {
                return;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult received = await _criUdpClient.ReceiveAsync(token);
                    byte[] packet = received.Buffer;
                    if (packet.Length != CriRealtimePacketParser.PacketLength)
                    {
                        continue;
                    }

                    var parsed = CriRealtimePacketParser.Parse(packet);
                    CriRealTimeData snapshot;
                    lock (_criDataLock)
                    {
                        _criData.UpdateFrom(parsed);
                        snapshot = _criData.Clone();
                    }

                    try
                    {
                        CriDataReceived?.Invoke(snapshot);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CRI回调异常] {ex.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CRI UDP接收异常] {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 停止 CRI UDP 监听并断开 TCP 连接。
        /// </summary>
        public void Disconnect()
        {
            StopCriUdpListener();
            _TcpClient.Disconnect();
        }
    }

}
