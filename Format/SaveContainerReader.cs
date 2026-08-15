using System;
using System.Collections.Generic;
using System.Text;

namespace Polaris.Save.Format
{
    /// <summary>
    /// 从存档字节流的 EOF 反向定位并解析尾部容器。这一层只做 framing 和 CRC，不碰 payload 语义；
    /// 它的判断结果决定了上层进不进只读恢复状态，所以宁可判"坏"也不猜。
    /// </summary>
    internal static class SaveContainerReader
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <param name="data">存档全部字节；允许比 <paramref name="length"/> 长（原版 ByteArray 的底层数组常有富余）。</param>
        /// <param name="length">存档的有效字节数。</param>
        internal static SaveContainerReadResult Read(byte[] data, int length)
        {
            if (data == null || length < SaveFormatLimits.FooterSize || length > data.Length)
            {
                return SaveContainerReadResult.Absent();
            }

            // 没有结束魔数就当作"这个存档根本没有容器"，不是损坏。
            if (!BigEndian.MatchesAscii(data, length, length - SaveFormatLimits.EndMagic.Length, SaveFormatLimits.EndMagic))
            {
                return SaveContainerReadResult.Absent();
            }

            int footerStart = length - SaveFormatLimits.FooterSize;
            if (!BigEndian.TryReadUInt32(data, length, footerStart, out uint declaredLength)
                || !BigEndian.TryReadUInt32(data, length, footerStart + 4, out uint declaredCrc))
            {
                return SaveContainerReadResult.Corrupt("无法读出容器 footer。");
            }

            int minimum = SaveFormatLimits.HeaderSize + SaveFormatLimits.FooterSize;
            if (declaredLength < minimum
                || declaredLength > SaveFormatLimits.MaxContainerLength
                || declaredLength > (uint)length)
            {
                return SaveContainerReadResult.Corrupt($"容器长度字段 {declaredLength} 不合法。");
            }

            int start = length - (int)declaredLength;
            if (!BigEndian.MatchesAscii(data, length, start, SaveFormatLimits.Magic))
            {
                return SaveContainerReadResult.Corrupt("按 footer 推算出的容器起点没有起始魔数，存档尾部很可能被截断。");
            }

            if (!BigEndian.TryReadUInt16(data, length, start + 8, out ushort formatVersion)
                || !BigEndian.TryReadUInt16(data, length, start + 10, out ushort headerSize)
                || !BigEndian.TryReadUInt32(data, length, start + 12, out uint partitionCount))
            {
                return SaveContainerReadResult.Corrupt("无法读出容器 header。");
            }

            if (formatVersion != SaveFormatLimits.FormatVersion)
            {
                return SaveContainerReadResult.Unsupported(
                    $"容器格式版本 {formatVersion} 高于本版本支持的 {SaveFormatLimits.FormatVersion}。");
            }

            if (headerSize != SaveFormatLimits.HeaderSize)
            {
                return SaveContainerReadResult.Corrupt($"容器 headerSize 字段 {headerSize} 不合法。");
            }

            if (partitionCount > SaveFormatLimits.MaxPartitions)
            {
                return SaveContainerReadResult.Corrupt($"分区数量 {partitionCount} 超过上限 {SaveFormatLimits.MaxPartitions}。");
            }

            int partitionsEnd = footerStart;
            List<SavePartitionRecord> partitions;
            try
            {
                partitions = ReadPartitions(data, start + SaveFormatLimits.HeaderSize, partitionsEnd, (int)partitionCount);
            }
            catch (PolarisSaveException ex)
            {
                return SaveContainerReadResult.Corrupt(ex.Message);
            }

            // CRC 覆盖 header + 全部分区 + containerLength 字段。
            uint actualCrc = Crc32.Compute(data, start, (int)declaredLength - 12);
            bool containerCrcOk = actualCrc == declaredCrc;

            bool anyDamaged = false;
            foreach (SavePartitionRecord partition in partitions)
            {
                if (partition.PayloadDamaged)
                {
                    anyDamaged = true;
                }
            }

            if (!containerCrcOk && !anyDamaged)
            {
                // 每个 payload 都自洽，说明坏在 framing/元数据字节上——整块都不能信。
                return SaveContainerReadResult.Corrupt("容器 CRC 校验失败，且没有任何单个分区能定位损坏。");
            }

            string message = containerCrcOk
                ? null
                : "容器 CRC 校验失败，损坏已定位到具体分区。";

            return SaveContainerReadResult.Parsed(partitions, start, anyDamaged, message);
        }

        static List<SavePartitionRecord> ReadPartitions(byte[] data, int offset, int end, int count)
        {
            var partitions = new List<SavePartitionRecord>(count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < count; i++)
            {
                if (!BigEndian.TryReadUInt16(data, end, offset, out ushort idLength))
                {
                    throw new PolarisSaveException($"第 {i} 个分区的 idLength 越界。");
                }

                offset += 2;
                if (idLength == 0 || idLength > SaveFormatLimits.MaxIdBytes || offset + idLength > end)
                {
                    throw new PolarisSaveException($"第 {i} 个分区的 idLength 字段 {idLength} 不合法。");
                }

                string id;
                try
                {
                    id = StrictUtf8.GetString(data, offset, idLength);
                }
                catch (ArgumentException)
                {
                    throw new PolarisSaveException($"第 {i} 个分区的 ID 不是合法 UTF-8。");
                }

                offset += idLength;

                if (!BigEndian.TryReadUInt16(data, end, offset, out ushort schemaVersion)
                    || !BigEndian.TryReadUInt16(data, end, offset + 2, out ushort flags)
                    || !BigEndian.TryReadUInt32(data, end, offset + 4, out uint payloadLength)
                    || !BigEndian.TryReadUInt32(data, end, offset + 8, out uint payloadCrc))
                {
                    throw new PolarisSaveException($"分区 {id} 的头部越界。");
                }

                offset += 12;

                // 先校验长度再分配，绝不按一个可能被改坏的 u32 去申请内存。
                if (payloadLength > SaveFormatLimits.MaxPartitionPayload || offset + payloadLength > end)
                {
                    throw new PolarisSaveException($"分区 {id} 的 payloadLength 字段 {payloadLength} 不合法。");
                }

                if (!seen.Add(id))
                {
                    throw new PolarisSaveException($"容器里出现重复分区 ID：{id}。");
                }

                var payload = new byte[payloadLength];
                Array.Copy(data, offset, payload, 0, (int)payloadLength);
                offset += (int)payloadLength;

                partitions.Add(new SavePartitionRecord(id, schemaVersion, flags, payload)
                {
                    PayloadDamaged = Crc32.Compute(payload) != payloadCrc,
                });
            }

            if (offset != end)
            {
                throw new PolarisSaveException($"分区数据没有正好填满容器：剩余 {end - offset} 字节。");
            }

            return partitions;
        }
    }
}
