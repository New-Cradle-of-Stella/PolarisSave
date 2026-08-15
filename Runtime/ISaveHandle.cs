namespace Polaris.Save.Runtime
{
    /// <summary><see cref="SaveHandle{T}"/> 的非泛型面，让注册表能不关心 T 地驱动全部分区。</summary>
    internal interface ISaveHandle
    {
        string Id { get; }

        ushort Version { get; }

        bool WasLoaded { get; }

        /// <summary>换成一个全新实例，丢掉上一局的状态。</summary>
        void ResetToDefault();

        byte[] WritePayload();

        void ReadPayload(byte[] payload, ushort storedVersion);

        void RunAfterLoad(ushort storedVersion);
    }
}
