using System;
using System.Collections.Generic;

namespace Polaris.Save.Serialization
{
    internal enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Object,
        Array,
    }

    /// <summary>
    /// PolarisSave 自己的 JSON 文档模型，刻意不用通用 JSON 库的 JObject/JToken。
    /// 这个模型只能表达 null/bool/number/string/object/array，没有任何"按存档内容构造 CLR 类型"的路径，也就不存在需要关掉的 TypeNameHandling。
    /// </summary>
    internal sealed class JsonValue
    {
        List<string> keys;
        List<JsonValue> values;
        Dictionary<string, int> index;

        JsonValue(JsonKind kind) => Kind = kind;

        internal JsonKind Kind { get; }

        internal bool BoolValue { get; private set; }

        /// <summary>number 的原始文本或 string 的内容。number 保留原文，避免二次格式化丢精度。</summary>
        internal string Text { get; private set; }

        internal int Count => values?.Count ?? 0;

        internal static JsonValue NewNull() => new JsonValue(JsonKind.Null);

        internal static JsonValue NewBool(bool value) => new JsonValue(JsonKind.Bool) { BoolValue = value };

        internal static JsonValue NewNumber(string rawText) => new JsonValue(JsonKind.Number) { Text = rawText };

        internal static JsonValue NewString(string value) =>
            value == null ? NewNull() : new JsonValue(JsonKind.String) { Text = value };

        internal static JsonValue NewObject() =>
            new JsonValue(JsonKind.Object)
            {
                keys = new List<string>(),
                values = new List<JsonValue>(),
                index = new Dictionary<string, int>(StringComparer.Ordinal),
            };

        internal static JsonValue NewArray() =>
            new JsonValue(JsonKind.Array) { values = new List<JsonValue>() };

        internal IReadOnlyList<string> Keys => (IReadOnlyList<string>)keys ?? Array.Empty<string>();

        internal JsonValue this[int i] => values[i];

        internal void Add(JsonValue item)
        {
            Require(JsonKind.Array);
            values.Add(item ?? NewNull());
        }

        internal void Set(string key, JsonValue value)
        {
            Require(JsonKind.Object);
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (index.TryGetValue(key, out int at))
            {
                values[at] = value ?? NewNull();
                return;
            }

            index[key] = keys.Count;
            keys.Add(key);
            values.Add(value ?? NewNull());
        }

        internal bool TryGet(string key, out JsonValue value)
        {
            Require(JsonKind.Object);
            if (key != null && index.TryGetValue(key, out int at))
            {
                value = values[at];
                return true;
            }

            value = null;
            return false;
        }

        internal bool ContainsKey(string key)
        {
            Require(JsonKind.Object);
            return key != null && index.ContainsKey(key);
        }

        void Require(JsonKind kind)
        {
            if (Kind != kind)
            {
                throw new InvalidOperationException($"JSON 节点是 {Kind}，不是 {kind}。");
            }
        }
    }
}
