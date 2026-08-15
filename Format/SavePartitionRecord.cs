namespace Polaris.Save.Format
{
    /// <summary>容器里的一个分区：framing 元数据 + 未解释的 payload 字节。</summary>
    internal sealed class SavePartitionRecord
    {
        internal SavePartitionRecord(string id, ushort schemaVersion, ushort flags, byte[] payload)
        {
            Id = id;
            SchemaVersion = schemaVersion;
            Flags = flags;
            Payload = payload;
        }

        internal string Id { get; }

        internal ushort SchemaVersion { get; }

        internal ushort Flags { get; }

        internal byte[] Payload { get; }

        /// <summary>payload CRC 与记录不符。此时 <see cref="Payload"/> 仍是原始字节，只能原样保留、不得解析。</summary>
        internal bool PayloadDamaged { get; set; }
    }
}
