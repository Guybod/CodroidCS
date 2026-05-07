using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Codroid
{
    /// <summary>
    /// 基于 TCP 的异步 JSON 客户端：发送 { id, ty, db } 格式指令，按完整 JSON 对象匹配响应 id 并完成等待；
    /// 无整数 <c>id</c> 的下行报文按 <c>ty</c> 分发给主题订阅回调（见 <see cref="RegisterPublishHandlerAndSubscribeAsync"/>）。
    /// </summary>
    public class FutureTcpClient
    {
        private TcpClient _client = new();
        private NetworkStream? _stream;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _promises = new();

        /// <summary>已向控制器发送过订阅帧的主题 <c>ty</c>（每连接至多下发一次，断开连接后清空）。</summary>
        private readonly ConcurrentDictionary<string, byte> _publishWireSent = new();

        private readonly object _publishHandlerLock = new();
        private readonly Dictionary<string, List<Action<PublishNotification>>> _publishHandlers = new();

        /// <summary>串行化 TCP 发送，避免 <see cref="SendCommand"/> 与订阅帧交错写入。</summary>
        private readonly SemaphoreSlim _tcpWriteGate = new(1, 1);

        /// <summary>
        /// 与控制器建立 TCP 连接并启动后台接收任务，用于持续解析下行 JSON。
        /// </summary>
        /// <param name="ip">控制器 IP 地址或主机名。</param>
        /// <param name="port">控制器 TCP 端口。</param>
        /// <returns>表示连接操作完成的任务。</returns>
        public async Task ConnectAsync(string ip, int port)
        {
            await _client.ConnectAsync(ip, port);
            _stream = _client.GetStream();
            _ = Task.Run(ReceiveWorker);
        }

        /// <summary>
        /// 序列化并发送一条 JSON 指令，等待相同 <paramref name="id"/> 的响应或超时。
        /// </summary>
        /// <param name="id">本次请求的唯一整数序号，须与响应中的 id 一致。</param>
        /// <param name="type">指令类型字符串（协议 ty 字段），例如 <c>project/run</c>。</param>
        /// <param name="data">业务参数对象，将序列化为 db 字段（可为匿名对象或值类型）。</param>
        /// <returns>解析成功且控制器未在 <c>err</c> 中报告错误时的响应对象。</returns>
        /// <exception cref="InvalidOperationException">尚未连接或响应无法反序列化。</exception>
        /// <exception cref="TimeoutException">在 10 秒内未收到匹配 id 的响应。</exception>
        /// <exception cref="CodroidCommandException">控制器响应中 <c>err</c> 非空，或其它已包装的执行失败。</exception>
        public async Task<CommonResponse> SendCommand(int id, string type, object data)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("未连接到服务器；请先调用 ConnectAsync 完成 TCP 连接。");
            }

            var requestObj = new { id = id, ty = type, db = data };
            string json = JsonSerializer.Serialize(requestObj);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            var promise = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _promises[id] = promise;

            try
            {
                await _tcpWriteGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }
                finally
                {
                    _tcpWriteGate.Release();
                }

                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(promise.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException(
                        $"等待控制器响应超时（10 秒）。请求 ID: {id}，指令: {type}");
                }

                string rawResponse = await promise.Task;

                var responseObj = JsonSerializer.Deserialize<CommonResponse>(rawResponse);

                if (responseObj == null)
                {
                    throw new InvalidOperationException(
                        $"无法将控制器返回内容反序列化为响应对象。请求 ID: {id}，指令: {type}。原始内容: {TruncateForMessage(rawResponse)}");
                }

                if (!string.IsNullOrEmpty(responseObj.err))
                {
                    throw new CodroidCommandException(
                        id,
                        type,
                        $"控制器报告错误: {responseObj.err}",
                        responseObj.err,
                        responseObj);
                }

                return responseObj;
            }
            catch (CodroidCommandException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CodroidCommandException(
                    id,
                    type,
                    $"执行指令时发生异常: {ex.Message}",
                    innerException: ex);
            }
            finally
            {
                _promises.TryRemove(id, out _);
            }
        }

        /// <summary>
        /// 注册主题推送回调，并在该 TCP 连接上首次订阅该 <paramref name="topicTy"/> 时下发 <c>{ ty, tc }</c>（<b>不含</b> <c>id</c>，不等待响应）。
        /// </summary>
        /// <param name="topicTy">主题名，与推送报文 <c>ty</c> 一致，例如 <c>publish/RobotStatus</c>。</param>
        /// <param name="handler">收到推送时在线程池触发；请勿阻塞。</param>
        /// <param name="tcMilliseconds">协议 <c>tc</c>（毫秒）；SDK 默认 <c>100</c>。</param>
        public async Task RegisterPublishHandlerAndSubscribeAsync(
            string topicTy,
            Action<PublishNotification> handler,
            int tcMilliseconds = PublishSubscribeDefaults.TcMilliseconds)
        {
            if (string.IsNullOrEmpty(topicTy))
            {
                throw new ArgumentException("主题 ty 不能为空。", nameof(topicTy));
            }

            ArgumentNullException.ThrowIfNull(handler);

            lock (_publishHandlerLock)
            {
                if (!_publishHandlers.TryGetValue(topicTy, out var list))
                {
                    list = new List<Action<PublishNotification>>();
                    _publishHandlers[topicTy] = list;
                }

                list.Add(handler);
            }

            if (_publishWireSent.TryAdd(topicTy, 0))
            {
                await SendPublishSubscribeFrameAsync(topicTy, tcMilliseconds).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 移除由 <see cref="RegisterPublishHandlerAndSubscribeAsync"/> 注册的回调。
        /// </summary>
        public void RemovePublishHandler(string topicTy, Action<PublishNotification> handler)
        {
            if (string.IsNullOrEmpty(topicTy))
            {
                throw new ArgumentException("主题 ty 不能为空。", nameof(topicTy));
            }

            ArgumentNullException.ThrowIfNull(handler);

            lock (_publishHandlerLock)
            {
                if (!_publishHandlers.TryGetValue(topicTy, out var list))
                {
                    return;
                }

                list.Remove(handler);
                if (list.Count == 0)
                {
                    _publishHandlers.Remove(topicTy);
                }
            }
        }

        /// <summary>
        /// 发送主题订阅帧：<c>{ "ty", "tc" }</c>（无 <c>id</c>、无 <c>db</c>）；推送仅在数据变化或首次订阅时出现。
        /// </summary>
        private async Task SendPublishSubscribeFrameAsync(string topicTy, int tcMilliseconds)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("未连接到服务器；请先调用 ConnectAsync。");
            }

            var frame = new { ty = topicTy, tc = tcMilliseconds };
            string json = JsonSerializer.Serialize(frame);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _tcpWriteGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            finally
            {
                _tcpWriteGate.Release();
            }
        }

        private static string TruncateForMessage(string text, int maxChars = 512)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "(空)";
            }

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= maxChars ? text : text.Substring(0, maxChars) + "…";
        }

        /// <summary>
        /// 从网络流读取字节并按花括号配对拼接完整 JSON，再分发到对应请求的 <see cref="TaskCompletionSource{TResult}"/>。
        /// </summary>
        /// <returns>表示接收循环生命周期的任务；流关闭或异常时结束。</returns>
        private async Task ReceiveWorker()
        {
            byte[] buffer = new byte[4096];
            StringBuilder sb = new StringBuilder();
            int braceCount = 0;

            try
            {
                while (true)
                {
                    int n = await _stream!.ReadAsync(buffer, 0, buffer.Length);
                    if (n == 0) break;

                    string chunk = Encoding.UTF8.GetString(buffer, 0, n);
                    foreach (char c in chunk)
                    {
                        sb.Append(c);
                        if (c == '{') braceCount++;
                        else if (c == '}')
                        {
                            braceCount--;
                            if (braceCount == 0 && sb.Length > 0)
                            {
                                string completeJson = sb.ToString();
                                sb.Clear();
                                ProcessSingleMessage(completeJson);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[接收线程异常] {ex.Message}");
            }
        }

        /// <summary>
        /// 解析单条 JSON，若包含整数类型的 id 且存在等待中的 Promise，则完成该 Promise。
        /// </summary>
        /// <param name="json">一条完整的 JSON 文本。</param>
        private void ProcessSingleMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("id", out var idElement)
                    && idElement.ValueKind == JsonValueKind.Number
                    && idElement.TryGetInt32(out int cmdId))
                {
                    if (_promises.TryRemove(cmdId, out var promise))
                    {
                        promise.SetResult(json);
                    }

                    return;
                }

                if (!root.TryGetProperty("ty", out var tyEl))
                {
                    return;
                }

                string? ty = tyEl.GetString();
                if (string.IsNullOrEmpty(ty))
                {
                    return;
                }

                JsonElement dbClone = default;
                if (root.TryGetProperty("db", out var dbEl))
                {
                    dbClone = dbEl.Clone();
                }

                var notification = new PublishNotification
                {
                    Ty = ty,
                    Db = dbClone,
                    RawJson = json
                };

                InvokePublishHandlers(ty, notification);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[消息处理异常] {ex.Message}");
            }
        }

        private void InvokePublishHandlers(string ty, PublishNotification notification)
        {
            List<Action<PublishNotification>>? snapshot = null;
            lock (_publishHandlerLock)
            {
                if (_publishHandlers.TryGetValue(ty, out var list) && list.Count > 0)
                {
                    snapshot = new List<Action<PublishNotification>>(list);
                }
            }

            if (snapshot == null)
            {
                return;
            }

            foreach (Action<PublishNotification> h in snapshot)
            {
                Action<PublishNotification> captured = h;
                _ = Task.Run(() =>
                {
                    try
                    {
                        captured(notification);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{ty} 回调异常] {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// 关闭网络流与底层 TCP 连接；未取消进行中的等待，调用方需自行处理超时与失败。
        /// </summary>
        public void Disconnect()
        {
            _publishWireSent.Clear();
            lock (_publishHandlerLock)
            {
                _publishHandlers.Clear();
            }

            _stream?.Close();
            _client.Close();
        }
    }
}
