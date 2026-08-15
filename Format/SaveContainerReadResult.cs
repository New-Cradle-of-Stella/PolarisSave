using System.Collections.Generic;

namespace Polaris.Save.Format
{
    internal enum SaveContainerStatus
    {
        /// <summary>存档末尾没有容器——老存档或从未装过 PolarisSave 的存档。</summary>
        Absent,

        /// <summary>framing 与全部 CRC 都通过。</summary>
        Ok,

        /// <summary>framing 完好，但个别分区 payload 的 CRC 不符：只隔离这些分区，其余照常读。</summary>
        PartiallyCorrupt,

        /// <summary>framing 或元数据本身坏了，整个容器不可解释。</summary>
        Corrupt,

        /// <summary>容器由更高版本的 PolarisSave 写出，本版本不认识它的 framing。</summary>
        UnsupportedFormat,
    }

    internal sealed class SaveContainerReadResult
    {
        SaveContainerReadResult(SaveContainerStatus status, string message)
        {
            Status = status;
            Message = message;
            Partitions = new List<SavePartitionRecord>();
        }

        internal SaveContainerStatus Status { get; }

        internal string Message { get; }

        internal List<SavePartitionRecord> Partitions { get; }

        /// <summary>容器在存档字节流中的起点，用于和原版读取结束位置对账。</summary>
        internal int ContainerStart { get; private set; } = -1;

        internal static SaveContainerReadResult Absent() =>
            new SaveContainerReadResult(SaveContainerStatus.Absent, null);

        internal static SaveContainerReadResult Corrupt(string message) =>
            new SaveContainerReadResult(SaveContainerStatus.Corrupt, message);

        internal static SaveContainerReadResult Unsupported(string message) =>
            new SaveContainerReadResult(SaveContainerStatus.UnsupportedFormat, message);

        internal static SaveContainerReadResult Parsed(
            List<SavePartitionRecord> partitions,
            int containerStart,
            bool anyDamaged,
            string message)
        {
            var result = new SaveContainerReadResult(
                anyDamaged ? SaveContainerStatus.PartiallyCorrupt : SaveContainerStatus.Ok,
                message)
            {
                ContainerStart = containerStart,
            };

            result.Partitions.AddRange(partitions);
            return result;
        }
    }
}
