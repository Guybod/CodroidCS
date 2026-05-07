using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

#pragma warning disable CS1591

namespace Codroid
{
    /// <summary>
    /// <c>IOManager/GetIOValue</c>、<c>IOManager/SetIOValue</c> 协议中的 <c>type</c> 取值。
    /// </summary>
    public static class IoPortKind
    {
        public const string Di = "DI";
        public const string Do = "DO";
        public const string Ai = "AI";
        public const string Ao = "AO";
    }

    /// <summary>
    /// 解析 <c>IOManager/GetIOValue</c> 的 <see cref="CommonResponse.db"/>。
    /// </summary>
    public static class IoGetResponseParser
    {
        /// <summary>
        /// 从响应数组中查找指定 DI/DO，将 <c>value</c> 规范为 <c>0</c> 或 <c>1</c>。
        /// </summary>
        /// <param name="response">控制器对 <c>IOManager/GetIOValue</c> 的响应。</param>
        /// <param name="ioType">IO 类型，通常为 <see cref="IoPortKind.Di"/> 或 <see cref="IoPortKind.Do"/>。</param>
        /// <param name="port">端口号。</param>
        /// <returns>数字量值，固定为 <c>0</c> 或 <c>1</c>。</returns>
        /// <exception cref="InvalidOperationException">响应格式不是数组、找不到目标端口、缺少 value 字段，或 value 不能转换为 0/1。</exception>
        public static int ParseDigital(CommonResponse response, string ioType, int port)
        {
            foreach (var el in EnumerateItems(response))
            {
                if (!Match(el, ioType, port))
                {
                    continue;
                }

                if (!el.TryGetProperty("value", out var v))
                {
                    throw new InvalidOperationException("响应项缺少 value 字段。");
                }

                return JsonValueToZeroOne(v);
            }

            throw new InvalidOperationException($"响应中未找到 {ioType} port={port}。");
        }

        /// <summary>
        /// 从响应数组中查找指定 AI/AO，读取 <c>value</c> 为 <see cref="double"/>。
        /// </summary>
        /// <param name="response">控制器对 <c>IOManager/GetIOValue</c> 的响应。</param>
        /// <param name="ioType">IO 类型，通常为 <see cref="IoPortKind.Ai"/> 或 <see cref="IoPortKind.Ao"/>。</param>
        /// <param name="port">端口号。</param>
        /// <returns>模拟量浮点值。</returns>
        /// <exception cref="FormatException">字符串 value 不是合法数字。</exception>
        /// <exception cref="InvalidOperationException">响应格式不是数组、找不到目标端口、缺少 value 字段，或 value 不能转换为数字。</exception>
        public static double ParseAnalog(CommonResponse response, string ioType, int port)
        {
            foreach (var el in EnumerateItems(response))
            {
                if (!Match(el, ioType, port))
                {
                    continue;
                }

                if (!el.TryGetProperty("value", out var v))
                {
                    throw new InvalidOperationException("响应项缺少 value 字段。");
                }

                return v.ValueKind switch
                {
                    JsonValueKind.Number => v.GetDouble(),
                    JsonValueKind.String => double.Parse(v.GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    _ => throw new InvalidOperationException($"无法将模拟量 value 解析为数字：{v.ValueKind}")
                };
            }

            throw new InvalidOperationException($"响应中未找到 {ioType} port={port}。");
        }

        /// <summary>
        /// 构造 <c>GetIOValue</c> 请求体：对象数组，每项含 <c>type</c>、<c>port</c>。
        /// </summary>
        /// <param name="pins">要查询的 IO 点列表。</param>
        /// <returns>可直接作为 <c>db</c> 下发的 JSON 数组。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pins"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="pins"/> 为空。</exception>
        public static JsonElement BuildGetQuery(IReadOnlyList<(string Type, int Port)> pins)
        {
            ArgumentNullException.ThrowIfNull(pins);
            if (pins.Count == 0)
            {
                throw new ArgumentException("至少指定一个 IO 点。", nameof(pins));
            }

            var payload = pins.Select(p => new { type = p.Type, port = p.Port }).ToArray();
            return JsonSerializer.SerializeToElement(payload);
        }

        private static IEnumerable<JsonElement> EnumerateItems(CommonResponse response)
        {
            var db = response.db;
            if (db.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"GetIOValue 响应 db 应为 JSON 数组，实际为 {db.ValueKind}。");
            }

            foreach (var el in db.EnumerateArray())
            {
                yield return el;
            }
        }

        private static bool Match(JsonElement el, string type, int port) =>
            el.TryGetProperty("type", out var t)
            && string.Equals(t.GetString(), type, StringComparison.Ordinal)
            && el.TryGetProperty("port", out var p)
            && p.GetInt32() == port;

        private static int JsonValueToZeroOne(JsonElement v)
        {
            return v.ValueKind switch
            {
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                JsonValueKind.Number => v.GetDouble() != 0.0 ? 1 : 0,
                JsonValueKind.String => ParseStringZeroOne(v.GetString()),
                _ => throw new InvalidOperationException($"无法将数字量 IO 解析为 0/1：{v.ValueKind}")
            };
        }

        private static int ParseStringZeroOne(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return 0;
            }

            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                return d != 0.0 ? 1 : 0;
            }

            return 0;
        }
    }
}

#pragma warning restore CS1591
