using System;
using Polaris.Save.Runtime;
using Polaris.Save.Serialization;

namespace Polaris.Save
{
    /// <summary>
    /// 一个已注册分区的长期句柄。模组把它存成 <c>static readonly</c>，全程通过
    /// <see cref="Current"/> 访问自己的存档数据。
    /// </summary>
    public sealed class SaveHandle<T> : ISaveHandle
        where T : class, IPolarisSaveData
    {
        readonly Func<T> factory;

        internal SaveHandle(string id, ushort version, Func<T> factory)
        {
            Id = id;
            Version = version;
            this.factory = factory;
            Reset();
        }

        /// <summary>注册时给定的全局唯一分区 ID。</summary>
        public string Id { get; }

        /// <summary>当前模组代码所用的 schema 版本，写进存档供以后迁移用。</summary>
        public ushort Version { get; }

        /// <summary>
        /// 当前这一局的数据实例，永不为 <c>null</c>。新游戏和每次读档都会换成新实例，
        /// 所以缓存这个引用是不安全的——每次都从句柄取。
        /// </summary>
        public T Current { get; private set; }

        /// <summary>本局的数据是否真的来自存档；老存档或全新游戏为 <c>false</c>。</summary>
        public bool WasLoaded { get; private set; }

        // 换实例的时机由 PolarisSave 的生命周期决定，不开放给模组自行触发。
        void ISaveHandle.ResetToDefault() => Reset();

        void Reset()
        {
            Current = factory()
                ?? throw new PolarisSaveException($"分区 {Id} 的工厂返回了 null。");
            WasLoaded = false;
        }

        byte[] ISaveHandle.WritePayload() =>
            JsonWriter.WriteToUtf8(SaveArchive.WriteRoot(Current, Id, Version));

        void ISaveHandle.ReadPayload(byte[] payload, ushort storedVersion)
        {
            SaveArchive.ReadRoot(Current, JsonReader.ReadUtf8(payload), Id, storedVersion);
            WasLoaded = true;
        }

        void ISaveHandle.RunAfterLoad(ushort storedVersion) =>
            SaveArchive.RunAfterLoad(Current, Id, storedVersion);
    }
}
