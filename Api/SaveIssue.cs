namespace Polaris.Save
{
    public enum SaveIssueKind
    {
        /// <summary>整个尾部容器的 framing 或元数据损坏，本次读档没有发布任何模组数据。</summary>
        ContainerCorrupt,

        /// <summary>容器由更高版本的 PolarisSave 写出，本版本读不了。</summary>
        UnsupportedFormat,

        /// <summary>单个分区的 payload CRC 不符，已隔离。</summary>
        PartitionCorrupt,

        /// <summary>分区的 schema 版本比当前模组新，已隔离且不解析。</summary>
        PartitionTooNew,

        /// <summary>分区 payload 解析失败（JSON 畸形、字段类型不符等），已隔离。</summary>
        PartitionReadFailed,

        /// <summary><see cref="SaveArchiveMode.AfterLoad"/> 迁移回合抛了异常。</summary>
        MigrationFailed,
    }

    /// <summary>一次读档中被发现的问题。</summary>
    public sealed class SaveIssue
    {
        internal SaveIssue(SaveIssueKind kind, string partitionId, string message, bool blocksSaving)
        {
            Kind = kind;
            PartitionId = partitionId;
            Message = message;
            BlocksSaving = blocksSaving;
        }

        public SaveIssueKind Kind { get; }

        /// <summary>出问题的分区 ID；问题出在整个容器上时为 <c>null</c>。</summary>
        public string PartitionId { get; }

        public string Message { get; }

        /// <summary>
        /// 这个问题是否说明"磁盘上还有人工可恢复的数据"。为 <c>true</c> 时本次会话拒绝普通保存，
        /// 免得拿默认值把还能救的字节盖掉。
        /// </summary>
        public bool BlocksSaving { get; }

        public override string ToString() =>
            PartitionId == null ? $"{Kind}: {Message}" : $"{Kind} [{PartitionId}]: {Message}";
    }
}
