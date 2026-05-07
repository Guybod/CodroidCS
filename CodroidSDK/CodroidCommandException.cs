using System;

namespace Codroid
{
    /// <summary>
    /// 表示向控制器发送指令后的失败：尤其是响应中 <see cref="CommonResponse.err"/> 非空时抛出，并附带完整响应供调用方读取 <see cref="CommonResponse.db"/>。
    /// </summary>
    public sealed class CodroidCommandException : Exception
    {
        /// <summary>
        /// 与本次请求对应的协议 id。
        /// </summary>
        public int RequestId { get; }

        /// <summary>
        /// 本次请求的指令类型（协议 ty，如 <c>project/run</c>）。
        /// </summary>
        public string CommandType { get; }

        /// <summary>
        /// 控制器在响应中填写的 <c>err</c> 字段；若非控制器报错（例如包装后的网络异常）则为 null。
        /// </summary>
        public string? ControllerError { get; }

        /// <summary>
        /// 反序列化后的完整响应；控制器报错时通常非 null，便于读取 <see cref="CommonResponse.db"/>。
        /// </summary>
        public CommonResponse? Response { get; }

        /// <summary>
        /// 构造带可选控制器错误与响应体的异常。
        /// </summary>
        /// <param name="requestId">请求 id。</param>
        /// <param name="commandType">指令类型字符串。</param>
        /// <param name="message">对外展示的主要说明（应包含 <paramref name="controllerError"/> 要点）。</param>
        /// <param name="controllerError">控制器 err 字段原文，无则为 null。</param>
        /// <param name="response">完整响应对象，无则为 null。</param>
        /// <param name="innerException">内部异常，无则为 null。</param>
        public CodroidCommandException(
            int requestId,
            string commandType,
            string message,
            string? controllerError = null,
            CommonResponse? response = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            RequestId = requestId;
            CommandType = commandType;
            ControllerError = controllerError;
            Response = response;
        }
    }
}
