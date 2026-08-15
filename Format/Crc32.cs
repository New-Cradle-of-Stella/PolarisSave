namespace Polaris.Save.Format
{
    /// <summary>
    /// IEEE 802.3 CRC-32（反射式，多项式 0xEDB88320，初值与终值均取反）。存档容器同时用它校验
    /// 单个分区 payload 和整个容器，用来把"截断/位翻转"和"根本没有容器"区分开。
    /// </summary>
    internal static class Crc32
    {
        const uint Polynomial = 0xEDB88320u;

        static readonly uint[] Table = BuildTable();

        internal static uint Compute(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            }

            return crc ^ 0xFFFFFFFFu;
        }

        internal static uint Compute(byte[] data) => Compute(data, 0, data.Length);

        static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    entry = (entry & 1) != 0 ? (entry >> 1) ^ Polynomial : entry >> 1;
                }

                table[i] = entry;
            }

            return table;
        }
    }
}
