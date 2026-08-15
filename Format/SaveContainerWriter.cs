using System;
using System.Collections.Generic;
using System.Text;

namespace Polaris.Save.Format
{
    /// <summary>把分区列表编码成追加到原版存档末尾的尾部容器。任何超限都在这里变成异常，绝不写出坏容器。</summary>
    internal static class SaveContainerWriter
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static byte[] Write(IList<SavePartitionRecord> partitions)
        {
            if (partitions == null)
            {
                throw new ArgumentNullException(nameof(partitions));
            }

            if (partitions.Count > SaveFormatLimits.MaxPartitions)
            {
                throw new PolarisSaveException(
                    $"分区数量 {partitions.Count} 超过上限 {SaveFormatLimits.MaxPartitions}。");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var buffer = new List<byte>(SaveFormatLimits.HeaderSize + SaveFormatLimits.FooterSize);

            BigEndian.WriteAscii(buffer, SaveFormatLimits.Magic);
            BigEndian.WriteUInt16(buffer, SaveFormatLimits.FormatVersion);
            BigEndian.WriteUInt16(buffer, SaveFormatLimits.HeaderSize);
            BigEndian.WriteUInt32(buffer, (uint)partitions.Count);

            foreach (SavePartitionRecord partition in partitions)
            {
                AppendPartition(buffer, partition, seen);
            }

            // containerLength 覆盖 magic 到 endMagic 的全部字节，读取端靠它从 EOF 反推容器起点。
            long containerLength = buffer.Count + SaveFormatLimits.FooterSize;
            if (containerLength > SaveFormatLimits.MaxContainerLength)
            {
                throw new PolarisSaveException(
                    $"容器长度 {containerLength} 字节超过上限 {SaveFormatLimits.MaxContainerLength} 字节。");
            }

            BigEndian.WriteUInt32(buffer, (uint)containerLength);

            // CRC 覆盖 header + 全部分区 + containerLength 字段本身，footer 剩下的 12 字节不参与。
            byte[] container = new byte[containerLength];
            buffer.CopyTo(container, 0);
            uint crc = Crc32.Compute(container, 0, buffer.Count);

            BigEndian.WriteUInt32(container, buffer.Count, crc);
            for (int i = 0; i < SaveFormatLimits.EndMagic.Length; i++)
            {
                container[buffer.Count + 4 + i] = (byte)SaveFormatLimits.EndMagic[i];
            }

            return container;
        }

        static void AppendPartition(List<byte> buffer, SavePartitionRecord partition, HashSet<string> seen)
        {
            if (partition == null)
            {
                throw new PolarisSaveException("分区记录为 null。");
            }

            if (!seen.Add(partition.Id))
            {
                throw new PolarisSaveException($"分区 ID 重复：{partition.Id}。");
            }

            byte[] id = Utf8.GetBytes(partition.Id ?? string.Empty);
            if (id.Length == 0 || id.Length > SaveFormatLimits.MaxIdBytes)
            {
                throw new PolarisSaveException(
                    $"分区 ID 的 UTF-8 长度必须在 1 到 {SaveFormatLimits.MaxIdBytes} 字节之间：{partition.Id}。");
            }

            byte[] payload = partition.Payload ?? Array.Empty<byte>();
            if (payload.Length > SaveFormatLimits.MaxPartitionPayload)
            {
                throw new PolarisSaveException(
                    $"分区 {partition.Id} 的 payload {payload.Length} 字节超过上限 {SaveFormatLimits.MaxPartitionPayload} 字节。");
            }

            BigEndian.WriteUInt16(buffer, (ushort)id.Length);
            buffer.AddRange(id);
            BigEndian.WriteUInt16(buffer, partition.SchemaVersion);
            BigEndian.WriteUInt16(buffer, partition.Flags);
            BigEndian.WriteUInt32(buffer, (uint)payload.Length);
            BigEndian.WriteUInt32(buffer, Crc32.Compute(payload));
            buffer.AddRange(payload);
        }
    }
}
