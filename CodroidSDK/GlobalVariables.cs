using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Codroid
{
    /// <summary>
    /// 保存全局变量时的一项：Name 须通过 <see cref="GlobalVarNaming.Validate"/>；
    /// Value 为任意可 JSON 序列化的对象，或使用 <see cref="GlobalVarRawJson"/> 传入已写好的 JSON 字面量；
    /// Remark 可为中文，为 null 或空白时不发送协议字段 <c>nm</c>。
    /// </summary>
    public readonly record struct GlobalVarSaveItem(string Name, object Value, string? Remark = null);

    /// <summary>
    /// 包装「已格式化好的 JSON 值字面量」，写入 <c>globalVar/saveVars</c> 时不再二次 JSON 序列化，避免双重转义。
    /// </summary>
    public readonly record struct GlobalVarRawJson(string Literal);

    /// <summary>
    /// 解析 <c>globalVar/getVars</c> 返回的 <see cref="CommonResponse.db"/> 中单个变量的展示信息。
    /// </summary>
    public sealed class GlobalVarCatalogEntry
    {
        /// <summary>
        /// 协议中的 <c>val</c>，可能是 JSON 字符串、数字等。
        /// </summary>
        public JsonElement Value { get; init; }

        /// <summary>
        /// 协议中的 <c>nm</c> 备注；缺省为空字符串。
        /// </summary>
        public string Remark { get; init; } = "";
    }

    /// <summary>
    /// 全局变量命名校验（Lua 风格：字母或下划线开头；避免双下划线开头；避开主要系统保留标识）。
    /// </summary>
    public static class GlobalVarNaming
    {
        private static readonly Regex s_namePattern = new(
            "^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// 控制器文档列出的主要保留字（区分大小写）；命中则不宜用作变量名。
        /// </summary>
        public static IReadOnlyCollection<string> ReservedNames => ReservedSet;

        private static readonly HashSet<string> ReservedSet = new(StringComparer.Ordinal)
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in",
            "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while", "table", "math",
            "DO", "DOGroup", "DIO", "DIOGroup", "AO", "AIO", "ModbusTCP", "setSpeedJ", "setAccJ", "setSpeedL", "setAccL",
            "setBlender", "setMoveRate", "getCoor", "getTool", "setCoor", "editCoor", "setTool", "editTool", "setPayload",
            "enableVibrationSuppression", "disableVibrationSuppression", "setCollisionDetectionSensitivity",
            "initComplianceControl", "enableComplianceControl", "disableComplianceControl", "forceControlZeroCalibrate",
            "setFilterPeriod", "searchSuccessed", "getJoint", "getTCP", "aposToCpos", "cposToApos", "cposToCpos",
            "posOffset", "posTrans", "coorRel", "toolRel", "getJointTorque", "getJointExternalTorque", "createTray",
            "getTrayPos", "posInverse", "distance", "interPos", "planeTrans", "getTrajStart", "getTrajEnd", "arrayAdd",
            "arraySub", "coorTrans", "movJ", "movL", "movC", "movCircle", "movLW", "movCW", "movTraj", "setWeave",
            "weaveStart", "weaveEnd", "setDO", "getDI", "getDO", "setDOGroup", "getDIGroup", "getDOGroup", "setAO",
            "getAI", "getAO", "getRegisterBool", "setRegisterBool", "getRegisterInt", "setRegisterInt", "getRegisterFloat",
            "setRegisterFloat", "RS485init", "RS485flush", "RS485write", "RS485read", "readCoils", "readDiscreteInputs",
            "readHoldingRegisters", "readInputRegisters", "writeSingleCoil", "writeSingleRegister", "writeMultipleCoils",
            "writeMultipleRegisters", "createSocketClient", "connectSocketClient", "writeSocketClient", "readSocketClient",
            "closeSocketClient", "wait", "waitCondition", "systemTime", "stopProject", "pauseProject", "runScript",
            "pauseScript", "resumeScript", "stopScript", "callModule", "print", "setInterruptInterval",
            "setInterruptCondition", "clearInterrupt", "strcmp", "strToNumberArray", "arrayToStr", "enableMultiWeld",
            "getCurSeam", "isMultiWeldFinished", "setMultiWeldOffset", "weldNextSeam", "resetMultiWeld", "searchStart",
            "setMasterFlag", "getOffsetValue", "search", "searchEnd", "searchOffset", "searchOffsetEnd", "searchError"
        };

        /// <summary>
        /// 校验变量名是否符合约定；不通过时抛出 <see cref="ArgumentException"/>。
        /// </summary>
        /// <param name="name">待校验的变量名。</param>
        /// <exception cref="ArgumentException">名为空、格式非法、双下划线开头或与保留字冲突。</exception>
        public static void Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("变量名不能为空。", nameof(name));
            }

            if (!s_namePattern.IsMatch(name))
            {
                throw new ArgumentException(
                    "变量名须以英文字母或下划线开头，且仅含字母、数字、下划线（符合 Lua 标识符习惯）。",
                    nameof(name));
            }

            if (name.StartsWith("__", StringComparison.Ordinal))
            {
                throw new ArgumentException("变量名不应以双下划线 __ 开头。", nameof(name));
            }

            if (ReservedSet.Contains(name))
            {
                throw new ArgumentException($"变量名与系统保留标识冲突: {name}", nameof(name));
            }
        }
    }

    /// <summary>
    /// 将保存项中的「值」转为协议 <c>val</c> 所需的字符串（JSON 片段文本）。
    /// </summary>
    public static class GlobalVarValueFormatter
    {
        /// <summary>
        /// 将 <paramref name="value"/> 转为写入 <c>val</c> 的字符串。
        /// </summary>
        public static string ToWireString(object value)
        {
            Polyfills.ThrowIfNull(value);

            return value switch
            {
                GlobalVarRawJson raw => raw.Literal ?? throw new ArgumentException("GlobalVarRawJson.Literal 不能为 null。", nameof(value)),
                JsonElement je => je.GetRawText(),
                _ => JsonSerializer.Serialize(value)
            };
        }
    }

    /// <summary>
    /// 解析 <c>globalVar/getVars</c> 响应中的 <c>db</c> 对象。
    /// </summary>
    public static class GlobalVarCatalogParser
    {
        /// <summary>
        /// 从成功的 <see cref="CommonResponse"/> 中解析变量字典；若 <c>db</c> 不是 JSON 对象则返回空字典。
        /// </summary>
        public static IReadOnlyDictionary<string, GlobalVarCatalogEntry> Parse(CommonResponse response)
        {
            var db = response.db;
            if (db.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, GlobalVarCatalogEntry>();
            }

            var dict = new Dictionary<string, GlobalVarCatalogEntry>(StringComparer.Ordinal);
            foreach (var prop in db.EnumerateObject())
            {
                var remark = "";
                if (prop.Value.TryGetProperty("nm", out var nmEl))
                {
                    remark = nmEl.ValueKind == JsonValueKind.String ? nmEl.GetString() ?? "" : nmEl.GetRawText();
                }

                if (!prop.Value.TryGetProperty("val", out var valEl))
                {
                    continue;
                }

                dict[prop.Name] = new GlobalVarCatalogEntry
                {
                    Value = valEl.Clone(),
                    Remark = remark
                };
            }

            return dict;
        }
    }
}
