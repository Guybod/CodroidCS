using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS1591

namespace Codroid
{
    /// <summary>工具坐标系 / 用户坐标系单帧（<c>x,y,z,a,b,c</c>）。</summary>
    public sealed class RobotFrame
    {
        public int Id { get; init; }

        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public double A { get; init; }
        public double B { get; init; }
        public double C { get; init; }
    }

    /// <summary>负载坐标系单帧（<c>m, mx, my, mz</c>）。</summary>
    public sealed class RobotPayloadFrame
    {
        public int Id { get; init; }

        public double M { get; init; }
        public double Mx { get; init; }
        public double My { get; init; }
        public double Mz { get; init; }
    }

    /// <summary><c>Robot/GetRobotParameter</c> 返回的设置界面参数快照。</summary>
    public sealed class RobotParameters
    {
        [JsonPropertyName("defaultToolId")]
        public int DefaultToolId { get; init; }

        [JsonPropertyName("defaultPayloadId")]
        public int DefaultPayloadId { get; init; }

        [JsonPropertyName("defaultCoordinateId")]
        public int DefaultCoordinateId { get; init; }

        [JsonPropertyName("maxPayload")]
        public double MaxPayload { get; init; }

        [JsonPropertyName("Tool")]
        public List<RobotFrame> Tool { get; init; } = new();

        [JsonPropertyName("Payload")]
        public List<RobotPayloadFrame> Payload { get; init; } = new();

        [JsonPropertyName("Coordinate")]
        public List<RobotFrame> Coordinate { get; init; } = new();
    }

    internal static class RobotSettingsValidation
    {
        public const int MinSlotId = 0;
        public const int MaxSlotId = 15;
        public const int WritableMinSlotId = 1;
        public const double ZeroEpsilon = 1e-9;

        public static void ValidateDefaultSlotId(int id, string paramName)
        {
            if (id is < MinSlotId or > MaxSlotId)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    id,
                    $"默认编号须在 {MinSlotId}~{MaxSlotId}。");
            }
        }

        public static void ValidateWritableFrameId(int frameId, string paramName)
        {
            if (frameId is < WritableMinSlotId or > MaxSlotId)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    frameId,
                    $"可修改的坐标系/工具槽位 id 须为 {WritableMinSlotId}~{MaxSlotId}；id=0 为保留项不可修改。");
            }
        }

        public static void ValidateFrameIdMatches(int frameId, RobotFrame frame)
        {
            Polyfills.ThrowIfNull(frame);
            if (frame.Id != frameId)
            {
                throw new ArgumentException(
                    $"frame.Id（{frame.Id}）须与 frameId（{frameId}）一致。",
                    nameof(frame));
            }
        }

        public static void ValidateFrameIdMatches(int frameId, RobotPayloadFrame frame)
        {
            Polyfills.ThrowIfNull(frame);
            if (frame.Id != frameId)
            {
                throw new ArgumentException(
                    $"frame.Id（{frame.Id}）须与 frameId（{frameId}）一致。",
                    nameof(frame));
            }
        }

        public static void ValidateReservedToolFrameUnchanged(RobotFrame frame)
        {
            EnsureReservedSlotZero(frame.Id, nameof(frame));
            EnsureToolFrameIsZero(frame);
        }

        public static void ValidateReservedPayloadFrameUnchanged(RobotPayloadFrame frame)
        {
            EnsureReservedSlotZero(frame.Id, nameof(frame));
            EnsurePayloadFrameIsZero(frame);
        }

        public static void ValidateToolFramesForSave(IReadOnlyList<RobotFrame> frames, string paramName)
        {
            ValidateFullSlotList(frames, paramName, f => f.Id, ValidateReservedToolFrameUnchanged);
        }

        public static void ValidatePayloadFramesForSave(
            IReadOnlyList<RobotPayloadFrame> frames,
            string paramName)
        {
            ValidateFullSlotList(frames, paramName, f => f.Id, ValidateReservedPayloadFrameUnchanged);
        }

        private static void EnsureReservedSlotZero(int id, string paramName)
        {
            if (id == 0)
            {
                return;
            }

            throw new ArgumentException("id=0 为控制器保留默认项，不允许通过写接口修改。", paramName);
        }

        private static void EnsureToolFrameIsZero(RobotFrame frame)
        {
            if (!IsZero(frame.X) || !IsZero(frame.Y) || !IsZero(frame.Z)
                || !IsZero(frame.A) || !IsZero(frame.B) || !IsZero(frame.C))
            {
                throw new ArgumentException(
                    "id=0 的工具/用户坐标系项必须保持全零，不可修改。",
                    nameof(frame));
            }
        }

        private static void EnsurePayloadFrameIsZero(RobotPayloadFrame frame)
        {
            if (!IsZero(frame.M) || !IsZero(frame.Mx) || !IsZero(frame.My) || !IsZero(frame.Mz))
            {
                throw new ArgumentException(
                    "id=0 的负载坐标系项必须保持全零，不可修改。",
                    nameof(frame));
            }
        }

        private static void ValidateFullSlotList<T>(
            IReadOnlyList<T> frames,
            string paramName,
            Func<T, int> idSelector,
            Action<T> validateReservedZero)
        {
            Polyfills.ThrowIfNull(frames, paramName);
            if (frames.Count != MaxSlotId + 1)
            {
                throw new ArgumentException(
                    $"须提供 {MaxSlotId + 1} 项（id {MinSlotId}~{MaxSlotId}）。",
                    paramName);
            }

            var seen = new HashSet<int>();
            foreach (var frame in frames)
            {
                Polyfills.ThrowIfNull(frame);
                int id = idSelector(frame);
                if (id is < MinSlotId or > MaxSlotId)
                {
                    throw new ArgumentException($"列表中存在非法 id={id}。", paramName);
                }

                if (!seen.Add(id))
                {
                    throw new ArgumentException($"列表中 id={id} 重复。", paramName);
                }

                if (id == 0)
                {
                    validateReservedZero(frame);
                }
            }

            for (int i = MinSlotId; i <= MaxSlotId; i++)
            {
                if (!seen.Contains(i))
                {
                    throw new ArgumentException($"缺少 id={i} 的项。", paramName);
                }
            }
        }

        private static bool IsZero(double v) => Math.Abs(v) <= ZeroEpsilon;
    }

    internal static class RobotSettingsSerialization
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public static RobotParameters ParseFromDb(JsonElement db)
        {
            if (db.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("GetRobotParameter 响应 db 为空。");
            }

            var parameters = JsonSerializer.Deserialize<RobotParameters>(db.GetRawText(), JsonOptions);
            if (parameters == null)
            {
                throw new InvalidOperationException("无法反序列化 RobotParameters。");
            }

            return parameters;
        }

        public static List<RobotFrame> MergeToolFrame(
            IReadOnlyList<RobotFrame> current,
            int frameId,
            RobotFrame updated)
        {
            var merged = current.ToList();
            int index = merged.FindIndex(f => f.Id == frameId);
            if (index < 0)
            {
                throw new InvalidOperationException($"当前参数中不存在 Tool id={frameId}。");
            }

            merged[index] = updated;
            return merged;
        }

        public static List<RobotPayloadFrame> MergePayloadFrame(
            IReadOnlyList<RobotPayloadFrame> current,
            int frameId,
            RobotPayloadFrame updated)
        {
            var merged = current.ToList();
            int index = merged.FindIndex(f => f.Id == frameId);
            if (index < 0)
            {
                throw new InvalidOperationException($"当前参数中不存在 Payload id={frameId}。");
            }

            merged[index] = updated;
            return merged;
        }

        public static List<RobotFrame> MergeCoordinateFrame(
            IReadOnlyList<RobotFrame> current,
            int frameId,
            RobotFrame updated)
        {
            var merged = current.ToList();
            int index = merged.FindIndex(f => f.Id == frameId);
            if (index < 0)
            {
                throw new InvalidOperationException($"当前参数中不存在 Coordinate id={frameId}。");
            }

            merged[index] = updated;
            return merged;
        }

        public static List<RobotFrame> OrderFramesById(IReadOnlyList<RobotFrame> frames) =>
            frames.OrderBy(f => f.Id).ToList();

        public static List<RobotPayloadFrame> OrderPayloadFramesById(IReadOnlyList<RobotPayloadFrame> frames) =>
            frames.OrderBy(f => f.Id).ToList();

        public static object BuildDefaultPayloadIdDb(int payloadId) =>
            new Dictionary<string, int> { ["defaultPayloadId"] = payloadId };

        public static object BuildDefaultToolIdDb(int toolId) =>
            new Dictionary<string, int> { ["defaultToolId"] = toolId };

        public static object BuildDefaultCoordinateIdDb(int coordinateId) =>
            new Dictionary<string, int> { ["defaultCoordinateId"] = coordinateId };

        public static object BuildToolDb(IReadOnlyList<RobotFrame> frames) =>
            new Dictionary<string, List<RobotFrame>> { ["Tool"] = OrderFramesById(frames) };

        public static object BuildPayloadDb(IReadOnlyList<RobotPayloadFrame> frames) =>
            new Dictionary<string, List<RobotPayloadFrame>>
            {
                ["Payload"] = OrderPayloadFramesById(frames)
            };

        public static object BuildCoordinateDb(IReadOnlyList<RobotFrame> frames) =>
            new Dictionary<string, List<RobotFrame>> { ["Coordinate"] = OrderFramesById(frames) };
    }
}
