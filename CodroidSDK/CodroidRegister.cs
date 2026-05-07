using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Codroid
{
    /// <summary>
    /// <c>RegisterManager/GetRegisterValue</c> 单条结果：<see cref="Value"/> 可能为 JSON 整数或浮点数等。
    /// </summary>
    public readonly struct RegisterReadValue
    {
        /// <summary>协议字段 <c>address</c>。</summary>
        public int Address { get; init; }

        /// <summary>协议字段 <c>value</c> 的原始 JSON（按需使用 <see cref="GetInt32"/> / <see cref="GetDouble"/>）。</summary>
        public JsonElement Value { get; init; }

        /// <summary>
        /// 将 <see cref="Value"/> 读为 <see cref="double"/>（JSON 数字、布尔或数字字符串）。
        /// </summary>
        /// <returns>转换后的浮点值；布尔值会转换为 1 或 0。</returns>
        /// <exception cref="FormatException">字符串值不是合法数字。</exception>
        /// <exception cref="InvalidOperationException"><see cref="Value"/> 的 JSON 类型无法转换为数字。</exception>
        public double GetDouble()
        {
            var v = Value;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetDouble(),
                JsonValueKind.String => double.Parse(v.GetString()!, CultureInfo.InvariantCulture),
                JsonValueKind.True => 1d,
                JsonValueKind.False => 0d,
                _ => throw new InvalidOperationException($"无法将寄存器值解析为数字：{v.ValueKind}")
            };
        }

        /// <summary>
        /// 将 <see cref="Value"/> 读为 <see cref="int"/>；若为小数或非数字则抛出。
        /// </summary>
        /// <returns>转换后的 32 位整数。</returns>
        /// <exception cref="InvalidOperationException"><see cref="Value"/> 不能无损表示为 <see cref="int"/>。</exception>
        public int GetInt32()
        {
            if (TryGetInt32(out var i))
            {
                return i;
            }

            throw new InvalidOperationException(
                $"寄存器地址 {Address} 的值无法用整型表示（当前 JSON：{Value.ValueKind}）。");
        }

        /// <summary>
        /// 尝试将值读为整型：JSON 整数、或可无损转为 <see cref="int"/> 的数字。
        /// </summary>
        /// <param name="value">若转换成功，返回整数值；否则返回 0。</param>
        /// <returns>成功转换为整型返回 true；否则返回 false。</returns>
        public bool TryGetInt32(out int value)
        {
            var v = Value;
            switch (v.ValueKind)
            {
                case JsonValueKind.Number:
                    if (v.TryGetInt32(out value))
                    {
                        return true;
                    }

                    var d = v.GetDouble();
                    if (double.IsFinite(d)
                        && Math.Abs(d - Math.Truncate(d)) < 1e-9
                        && d >= int.MinValue
                        && d <= int.MaxValue)
                    {
                        value = (int)d;
                        return true;
                    }

                    value = default;
                    return false;

                case JsonValueKind.String:
                    if (int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    {
                        return true;
                    }

                    value = default;
                    return false;

                case JsonValueKind.True:
                    value = 1;
                    return true;

                case JsonValueKind.False:
                    value = 0;
                    return true;

                default:
                    value = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// <c>RegisterManager/setExtendArrayType</c> 支持的 <c>type</c> 字符串（与协议一致）。
    /// </summary>
#pragma warning disable CS1591 // 各常量与协议字面量一一对应
    public static class RegisterExtendArrayValueType
    {
        public const string Bool = "Bool";
        public const string UInt8 = "UInt8";
        public const string Int8 = "Int8";
        public const string UInt16 = "UInt16";
        public const string Int16 = "Int16";
        public const string UInt32 = "UInt32";
        public const string Int32 = "Int32";
        public const string Float32 = "Float32";

        internal static bool IsKnown(string type) =>
            type is RegisterExtendArrayValueType.Bool
                or RegisterExtendArrayValueType.UInt8
                or RegisterExtendArrayValueType.Int8
                or RegisterExtendArrayValueType.UInt16
                or RegisterExtendArrayValueType.Int16
                or RegisterExtendArrayValueType.UInt32
                or RegisterExtendArrayValueType.Int32
                or RegisterExtendArrayValueType.Float32;
    }

#pragma warning restore CS1591

    internal static class RegisterResponseParser
    {
        /// <summary>
        /// 按响应数组下标与请求地址列表对齐解析（支持同一地址重复读取）；逐项校验 <c>address</c> 与请求一致。
        /// </summary>
        public static IReadOnlyList<RegisterReadValue> ParseAligned(CommonResponse response, IReadOnlyList<int> requestedAddresses)
        {
            var db = response.db;
            if (db.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"GetRegisterValue 响应 db 应为 JSON 数组，实际为 {db.ValueKind}。");
            }

            var items = db.EnumerateArray().ToArray();
            if (items.Length != requestedAddresses.Count)
            {
                throw new InvalidOperationException(
                    $"请求读取 {requestedAddresses.Count} 个寄存器，响应包含 {items.Length} 项。");
            }

            var result = new RegisterReadValue[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var el = items[i];
                if (!el.TryGetProperty("address", out var aEl) || !el.TryGetProperty("value", out var val))
                {
                    throw new InvalidOperationException("寄存器响应项须包含 address 与 value。");
                }

                int respondedAddress = aEl.GetInt32();
                int expected = requestedAddresses[i];
                if (respondedAddress != expected)
                {
                    throw new InvalidOperationException(
                        $"第 {i} 项寄存器地址不一致：请求 {expected}，响应 {respondedAddress}。");
                }

                result[i] = new RegisterReadValue { Address = respondedAddress, Value = val.Clone() };
            }

            return result;
        }
    }

    internal static class RegisterValidation
    {
        public static void ValidateAddresses(IReadOnlyList<int> addresses)
        {
            ArgumentNullException.ThrowIfNull(addresses);
            if (addresses.Count == 0)
            {
                throw new ArgumentException("至少指定一个寄存器地址。", nameof(addresses));
            }
        }

        public static void ValidateExtendIndex(int index)
        {
            if (index is < 0 or > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "扩展数组索引须在 0~999。");
            }
        }

        public static void ValidateExtendType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("扩展数组类型不能为空。", nameof(type));
            }

            if (!RegisterExtendArrayValueType.IsKnown(type))
            {
                throw new ArgumentException(
                    $"不支持的扩展数组类型 \"{type}\"；须为 Bool、UInt8、Int8、UInt16、Int16、UInt32、Int32、Float32 之一。",
                    nameof(type));
            }
        }
    }
}
