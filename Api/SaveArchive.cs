using System;
using System.Collections.Generic;
using System.Text;
using Polaris.Save.Format;
using Polaris.Save.Serialization;

namespace Polaris.Save
{
    /// <summary>
    /// 一次保存或读取操作的显式上下文。刻意<b>不做</b>全局静态状态：archive 由参数传进
    /// <see cref="IPolarisSaveData.Serialize"/>，谁在序列化、序列化到哪一层都写在签名里，
    /// 嵌套和异常都不会让"当前上下文"错位。
    /// </summary>
    public sealed partial class SaveArchive
    {
        readonly JsonValue node;
        readonly int depth;
        readonly string path;

        internal SaveArchive(SaveArchiveMode mode, string partitionId, ushort storedVersion, JsonValue node, int depth, string path)
        {
            Mode = mode;
            PartitionId = partitionId;
            StoredVersion = storedVersion;
            this.node = node;
            this.depth = depth;
            this.path = path;
        }

        /// <summary>本次调用的方向。</summary>
        public SaveArchiveMode Mode { get; }

        /// <summary>
        /// 存档里记录的分区 schema 版本。<see cref="SaveArchiveMode.Writing"/> 时等于当前注册版本；
        /// 没有读到分区时也等于当前注册版本，避免"全新存档"误触发版本迁移。
        /// </summary>
        public ushort StoredVersion { get; }

        /// <summary>当前分区的注册 ID，用于日志与错误信息。</summary>
        public string PartitionId { get; }

        /// <summary>嵌套一个实现了 <see cref="IPolarisSaveData"/> 的子对象。</summary>
        /// <remarks>
        /// 读取时：存档里没有这个 key 就<b>保持 <paramref name="value"/> 不变</b>（字段初始化器就是默认值）；
        /// 存的是 null 就置 null；存的是对象则在必要时新建实例再填充。
        /// </remarks>
        public void Child<T>(string key, ref T value)
            where T : class, IPolarisSaveData, new()
        {
            Child(key, ref value, () => new T());
        }

        /// <summary>嵌套一个子对象，并指定读取时用来新建实例的工厂。</summary>
        public void Child<T>(string key, ref T value, Func<T> factory)
            where T : class, IPolarisSaveData
        {
            ValidateKey(key);

            if (Mode == SaveArchiveMode.AfterLoad)
            {
                if (value != null)
                {
                    Descend(key, JsonValue.NewObject()).Run(value);
                }

                return;
            }

            if (Mode == SaveArchiveMode.Writing)
            {
                node.Set(key, value == null ? JsonValue.NewNull() : WriteChild(key, value));
                return;
            }

            if (!node.TryGet(key, out JsonValue child))
            {
                return;
            }

            if (child.Kind == JsonKind.Null)
            {
                value = null;
                return;
            }

            if (child.Kind != JsonKind.Object)
            {
                throw Mismatch(key, "object", child);
            }

            value ??= Create(factory, key);
            Descend(key, child).Run(value);
        }

        /// <summary>读写一组基本值。</summary>
        /// <remarks>读取时存档里没有这个 key 就保持 <paramref name="values"/> 不变；存的是 null 就置 null。</remarks>
        public void ValueList<T>(string key, ref List<T> values)
        {
            ValidateKey(key);

            if (Mode == SaveArchiveMode.AfterLoad)
            {
                return;
            }

            if (Mode == SaveArchiveMode.Writing)
            {
                if (values == null)
                {
                    node.Set(key, JsonValue.NewNull());
                    return;
                }

                JsonValue array = JsonValue.NewArray();
                for (int i = 0; i < values.Count; i++)
                {
                    array.Add(SaveValueCodec.Encode(values[i], $"{Describe(key)}[{i}]"));
                }

                node.Set(key, array);
                return;
            }

            if (!TryReadCollection(key, JsonKind.Array, out JsonValue stored, ref values))
            {
                return;
            }

            var result = new List<T>(stored.Count);
            for (int i = 0; i < stored.Count; i++)
            {
                result.Add(SaveValueCodec.Decode<T>(stored[i], $"{Describe(key)}[{i}]"));
            }

            values = result;
        }

        /// <summary>读写一组嵌套对象。列表元素允许是 null。</summary>
        public void ChildList<T>(string key, ref List<T> values)
            where T : class, IPolarisSaveData, new()
        {
            ChildList(key, ref values, () => new T());
        }

        /// <summary>读写一组嵌套对象，并指定读取时用来新建元素的工厂。</summary>
        public void ChildList<T>(string key, ref List<T> values, Func<T> factory)
            where T : class, IPolarisSaveData
        {
            ValidateKey(key);

            if (Mode == SaveArchiveMode.AfterLoad)
            {
                if (values != null)
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] != null)
                        {
                            Descend($"{key}[{i}]", JsonValue.NewObject()).Run(values[i]);
                        }
                    }
                }

                return;
            }

            if (Mode == SaveArchiveMode.Writing)
            {
                if (values == null)
                {
                    node.Set(key, JsonValue.NewNull());
                    return;
                }

                JsonValue array = JsonValue.NewArray();
                for (int i = 0; i < values.Count; i++)
                {
                    array.Add(values[i] == null ? JsonValue.NewNull() : WriteChild($"{key}[{i}]", values[i]));
                }

                node.Set(key, array);
                return;
            }

            if (!TryReadCollection(key, JsonKind.Array, out JsonValue stored, ref values))
            {
                return;
            }

            var result = new List<T>(stored.Count);
            for (int i = 0; i < stored.Count; i++)
            {
                JsonValue item = stored[i];
                if (item.Kind == JsonKind.Null)
                {
                    result.Add(null);
                    continue;
                }

                if (item.Kind != JsonKind.Object)
                {
                    throw Mismatch($"{key}[{i}]", "object", item);
                }

                T instance = Create(factory, key);
                Descend($"{key}[{i}]", item).Run(instance);
                result.Add(instance);
            }

            values = result;
        }

        /// <summary>读写一张字符串到基本值的表。</summary>
        public void ValueMap<T>(string key, ref Dictionary<string, T> values)
        {
            ValidateKey(key);

            if (Mode == SaveArchiveMode.AfterLoad)
            {
                return;
            }

            if (Mode == SaveArchiveMode.Writing)
            {
                if (values == null)
                {
                    node.Set(key, JsonValue.NewNull());
                    return;
                }

                JsonValue map = JsonValue.NewObject();

                // 字典本身无序；按序数排序保证同样的内存状态每次都写出同样的字节。
                var mapKeys = new List<string>(values.Keys);
                mapKeys.Sort(StringComparer.Ordinal);
                foreach (string mapKey in mapKeys)
                {
                    if (mapKey == null)
                    {
                        throw new PolarisSaveException($"{Describe(key)} 的表里有 null 键，无法写进 JSON 对象。");
                    }

                    map.Set(mapKey, SaveValueCodec.Encode(values[mapKey], $"{Describe(key)}[{mapKey}]"));
                }

                node.Set(key, map);
                return;
            }

            if (!TryReadCollection(key, JsonKind.Object, out JsonValue stored, ref values))
            {
                return;
            }

            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            IReadOnlyList<string> storedKeys = stored.Keys;
            for (int i = 0; i < storedKeys.Count; i++)
            {
                result[storedKeys[i]] = SaveValueCodec.Decode<T>(stored[i], $"{Describe(key)}[{storedKeys[i]}]");
            }

            values = result;
        }

        // ── 内部

        internal static JsonValue WriteRoot(IPolarisSaveData data, string partitionId, ushort version)
        {
            JsonValue root = JsonValue.NewObject();
            new SaveArchive(SaveArchiveMode.Writing, partitionId, version, root, 1, partitionId).Run(data);
            return root;
        }

        internal static void ReadRoot(IPolarisSaveData data, JsonValue root, string partitionId, ushort storedVersion)
        {
            if (root.Kind != JsonKind.Object)
            {
                throw new PolarisSaveException($"分区 {partitionId} 的 payload 顶层不是 JSON 对象。");
            }

            new SaveArchive(SaveArchiveMode.Reading, partitionId, storedVersion, root, 1, partitionId).Run(data);
        }

        internal static void RunAfterLoad(IPolarisSaveData data, string partitionId, ushort storedVersion)
        {
            new SaveArchive(SaveArchiveMode.AfterLoad, partitionId, storedVersion, JsonValue.NewObject(), 1, partitionId)
                .Run(data);
        }

        void Run(IPolarisSaveData data) => data.Serialize(this);

        JsonValue WriteChild(string key, IPolarisSaveData data)
        {
            JsonValue child = JsonValue.NewObject();
            Descend(key, child).Run(data);
            return child;
        }

        SaveArchive Descend(string key, JsonValue child)
        {
            if (depth + 1 > SaveFormatLimits.MaxDepth)
            {
                throw new PolarisSaveException(
                    $"{Describe(key)} 的对象嵌套超过 {SaveFormatLimits.MaxDepth} 层。");
            }

            return new SaveArchive(Mode, PartitionId, StoredVersion, child, depth + 1, Describe(key));
        }

        /// <summary>读取端集合的共同前半段：key 不存在保持原值，存的是 null 就置 null，形状不符报错。</summary>
        bool TryReadCollection<TCollection>(string key, JsonKind expected, out JsonValue stored, ref TCollection target)
            where TCollection : class
        {
            if (!node.TryGet(key, out stored))
            {
                return false;
            }

            if (stored.Kind == JsonKind.Null)
            {
                target = null;
                return false;
            }

            if (stored.Kind != expected)
            {
                throw Mismatch(key, expected == JsonKind.Array ? "array" : "object", stored);
            }

            return true;
        }

        static T Create<T>(Func<T> factory, string key)
            where T : class
        {
            T instance = factory != null ? factory() : null;
            return instance ?? throw new PolarisSaveException($"成员 {key} 的工厂返回了 null。");
        }

        void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new PolarisSaveException($"分区 {PartitionId} 里出现了空的字段 key。");
            }

            if (Encoding.UTF8.GetByteCount(key) > SaveFormatLimits.MaxIdBytes)
            {
                throw new PolarisSaveException(
                    $"字段 key \"{key}\" 的 UTF-8 长度超过 {SaveFormatLimits.MaxIdBytes} 字节。");
            }
        }

        string Describe(string key) => $"{path}.{key}";

        PolarisSaveException Mismatch(string key, string expected, JsonValue actual) =>
            new PolarisSaveException(
                $"{Describe(key)} 期望 {expected}，但存的是 {actual.Kind}。字段类型变化必须显式迁移。");
    }
}
