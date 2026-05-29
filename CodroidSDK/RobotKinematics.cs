using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Codroid
{
    /// <summary>
    /// 笛卡尔相对位姿计算时 <c>coorType</c> 的取值（协议字符串 <c>user</c> / <c>tool</c>）。
    /// </summary>
    public enum RelativePoseCoorType
    {
        /// <summary>用户坐标系，可与 <c>coor</c> 一起指定偏移所用坐标系。</summary>
        User,

        /// <summary>工具坐标系。</summary>
        Tool
    }

    /// <summary>
    /// 机器人正逆解等计算接口的公共校验与响应解析。
    /// </summary>
    public static class RobotKinematics
    {
        /// <summary>标准六维向量长度（关节或位姿）。</summary>
        public const int Vector6Length = 6;

        /// <summary>
        /// 将枚举转为协议中的 <c>coorType</c> 字符串。
        /// </summary>
        public static string ToWireCoorType(RelativePoseCoorType type) =>
            type == RelativePoseCoorType.User ? "user" : "tool";

        /// <summary>
        /// 校验数组非 null 且长度为 6。
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException">长度不是 6。</exception>
        public static void RequireVector6(string paramName, double[] values)
        {
            Polyfills.ThrowIfNull(values);
            if (values.Length != Vector6Length)
            {
                throw new ArgumentException($"参数须为 {Vector6Length} 个 double（与控制器协议一致）。", paramName);
            }
        }

        /// <summary>
        /// 从成功响应的 <see cref="CommonResponse.db"/> 解析为 6 个 double（正逆解、相对位姿等返回的一维数组）。
        /// </summary>
        /// <param name="db">响应中的 <c>db</c> 字段。</param>
        /// <returns>长度为 6 的数组。</returns>
        /// <exception cref="InvalidOperationException"><c>db</c> 不是数组、长度不为 6，或为空数组。</exception>
        public static double[] ParseDbAsVector6(JsonElement db)
        {
            if (db.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"无法解析位姿/关节数组：db 应为 JSON 数组，实际为 {db.ValueKind}。");
            }

            var list = new List<double>(Vector6Length);
            foreach (var el in db.EnumerateArray())
            {
                list.Add(el.ValueKind switch
                {
                    JsonValueKind.Number => el.GetDouble(),
                    _ => throw new InvalidOperationException($"数组元素类型不支持: {el.ValueKind}")
                });
            }

            if (list.Count == 0)
            {
                throw new InvalidOperationException(
                    "控制器返回空数组。逆解时可尝试调整参考关节角 rj 后再试。");
            }

            if (list.Count != Vector6Length)
            {
                throw new InvalidOperationException(
                    $"期望 {Vector6Length} 个分量，实际为 {list.Count}。");
            }

            return list.ToArray();
        }
    }
}
