using System;
using System.Text.Json;

namespace Codroid
{
    /// <summary>
    /// 主题订阅请求体中的 <c>tc</c>（毫秒）默认值；与 <see cref="CodroidClient.SubscribePublishTopicAsync"/> 的默认参数一致。
    /// </summary>
    public static class PublishSubscribeDefaults
    {
        /// <summary>订阅帧 <c>tc</c> 字段默认 <c>100</c>。</summary>
        public const int TcMilliseconds = 100;
    }

    /// <summary>
    /// 控制器经 TCP 下发的主题推送（无 <c>id</c> 字段）；业务载荷在 <see cref="Db"/>，原始 JSON 在 <see cref="RawJson"/>。
    /// </summary>
    public sealed class PublishNotification
    {
        /// <summary>与协议 <c>ty</c> 一致，例如 <c>publish/ProjectState</c>。</summary>
        public string Ty { get; init; } = "";

        /// <summary>协议 <c>db</c>；缺省时为 <see cref="JsonValueKind.Undefined"/>。</summary>
        public JsonElement Db { get; init; }

        /// <summary>本条消息的完整 JSON 文本。</summary>
        public string RawJson { get; init; } = "";
    }

    /// <summary>
    /// 文档 15.x 常见主题名称（<c>ty</c>），订阅时传入 <see cref="CodroidClient.SubscribePublishTopicAsync"/> 的主题字符串参数。
    /// </summary>
    public static class PublishTopics
    {
#pragma warning disable CS1591
        public const string ProjectState = "publish/ProjectState";
        public const string VarUpdate = "publish/VarUpdate";
        public const string RobotStatus = "publish/RobotStatus";
        public const string RobotPosture = "publish/RobotPosture";
        public const string RobotCoordinate = "publish/RobotCoordinate";
        public const string Log = "publish/Log";
        public const string Error = "publish/Error";
#pragma warning restore CS1591
    }

    /// <summary>
    /// 取消 TCP 主题推送回调注册；实现 <see cref="IDisposable"/>，释放后不再接收该 <see cref="TopicTy"/> 的回调。
    /// </summary>
    /// <remarks>不会向控制器发送「取消订阅」报文；断开 TCP 后所有订阅失效，且 SDK 会清空注册。</remarks>
    public sealed class PublishTopicSubscription : IDisposable
    {
        private FutureTcpClient? _client;
        private readonly string _topicTy;
        private readonly Action<PublishNotification> _handler;

        internal PublishTopicSubscription(FutureTcpClient client, string topicTy, Action<PublishNotification> handler)
        {
            _client = client;
            _topicTy = topicTy;
            _handler = handler;
        }

        /// <summary>协议中的主题名（<c>ty</c>）。</summary>
        public string TopicTy => _topicTy;

        /// <inheritdoc />
        public void Dispose()
        {
            FutureTcpClient? c = Interlocked.Exchange(ref _client, null);
            c?.RemovePublishHandler(_topicTy, _handler);
        }
    }
}
