using System;
using System.Collections.Generic;

namespace Polaris.Save.Format
{
    /// <summary>容器 framing 的大端整数读写。读取端一律先查边界再取值，越界返回 false 而不是抛异常。</summary>
    internal static class BigEndian
    {
        internal static void WriteUInt16(List<byte> buffer, ushort value)
        {
            buffer.Add((byte)(value >> 8));
            buffer.Add((byte)value);
        }

        internal static void WriteUInt32(List<byte> buffer, uint value)
        {
            buffer.Add((byte)(value >> 24));
            buffer.Add((byte)(value >> 16));
            buffer.Add((byte)(value >> 8));
            buffer.Add((byte)value);
        }

        internal static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        internal static bool TryReadUInt16(byte[] data, int limit, int offset, out ushort value)
        {
            value = 0;
            if (offset < 0 || offset + 2 > limit)
            {
                return false;
            }

            value = (ushort)((data[offset] << 8) | data[offset + 1]);
            return true;
        }

        internal static bool TryReadUInt32(byte[] data, int limit, int offset, out uint value)
        {
            value = 0;
            if (offset < 0 || offset + 4 > limit)
            {
                return false;
            }

            value = ((uint)data[offset] << 24)
                | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8)
                | data[offset + 3];
            return true;
        }

        internal static bool MatchesAscii(byte[] data, int limit, int offset, string ascii)
        {
            if (offset < 0 || offset + ascii.Length > limit)
            {
                return false;
            }

            for (int i = 0; i < ascii.Length; i++)
            {
                if (data[offset + i] != (byte)ascii[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal static void WriteAscii(List<byte> buffer, string ascii)
        {
            foreach (char c in ascii)
            {
                if (c > 0x7F)
                {
                    throw new ArgumentOutOfRangeException(nameof(ascii), ascii, "魔数必须是纯 ASCII。");
                }

                buffer.Add((byte)c);
            }
        }
    }
}
