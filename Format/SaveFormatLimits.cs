namespace Polaris.Save.Format
{
    /// <summary>
    /// 容器格式的硬上限。读取时<b>必须</b>在分配内存之前拿这些值校验长度字段，
    /// 否则一个被改坏的 u32 就能让我们替攻击者申请几个 GB。
    /// </summary>
    internal static class SaveFormatLimits
    {
        /// <summary>容器起始魔数。</summary>
        internal const string Magic = "POLARSAV";

        /// <summary>容器结束魔数，用来从 EOF 反向定位容器。</summary>
        internal const string EndMagic = "PLSVEND!";

        /// <summary>当前写出的容器格式版本。</summary>
        internal const ushort FormatVersion = 1;

        /// <summary>magic(8) + formatVersion(2) + headerSize(2) + partitionCount(4)。</summary>
        internal const int HeaderSize = 16;

        /// <summary>containerLength(4) + containerCrc32(4) + endMagic(8)。</summary>
        internal const int FooterSize = 16;

        /// <summary>一个分区除 id 和 payload 之外的固定开销。</summary>
        internal const int PartitionOverhead = 2 + 2 + 2 + 4 + 4;

        internal const int MaxPartitions = 1024;

        internal const int MaxPartitionPayload = 4 * 1024 * 1024;

        internal const int MaxContainerLength = 16 * 1024 * 1024;

        /// <summary>分区 ID 与字段 key 的长度上限，按 UTF-8 字节计。</summary>
        internal const int MaxIdBytes = 128;

        /// <summary>对象/数组的最大嵌套层数，读写两侧都要拦。</summary>
        internal const int MaxDepth = 64;
    }
}
