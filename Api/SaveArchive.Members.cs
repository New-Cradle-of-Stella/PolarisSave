using System;
using Polaris.Save.Serialization;

namespace Polaris.Save
{
    /// <summary>
    /// <c>Member</c> 的强类型入口。V1 支持的类型就是这里列出的这些：bool、全部整数类型、
    /// float/double/decimal、string、char、byte[]、Guid、DateTime、enum 和 <see cref="SaveVector2"/>。
    /// 任意对象、委托、<see cref="Type"/>、UnityEngine 对象都<b>不</b>支持——存档里不该藏着一条
    /// 能构造任意 CLR 类型的路径。自定义类型请实现 <see cref="IPolarisSaveData"/> 并用 <c>Child</c>。
    /// </summary>
    /// <remarks>读取时存档里缺这个 key 就写入 <c>fallback</c>；key 在但类型对不上则报错并隔离该分区。</remarks>
    public sealed partial class SaveArchive
    {
        public void Member(string key, ref bool value, bool fallback = false)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeBoolean(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref sbyte value, sbyte fallback = 0)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeSByte(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref byte value, byte fallback = 0)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeByte(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref short value, short fallback = 0)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeInt16(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref ushort value, ushort fallback = 0)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeUInt16(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref int value, int fallback = 0)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeInt32(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref uint value, uint fallback = 0)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeUInt32(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref long value, long fallback = 0L)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeInt64(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref ulong value, ulong fallback = 0UL)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeUInt64(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref float value, float fallback = 0f)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeSingle(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref double value, double fallback = 0d)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeDouble(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref decimal value, decimal fallback = 0m)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeDecimal(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref char value, char fallback = '\0')
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeChar(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref string value, string fallback = null)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeString(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref byte[] value, byte[] fallback = null)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeBytes(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref Guid value, Guid fallback = default)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeGuid(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref DateTime value, DateTime fallback = default)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeDateTime(stored, Describe(key)) : fallback;
        }

        public void Member(string key, ref SaveVector2 value, SaveVector2 fallback = default)
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, JsonScalars.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? JsonScalars.DecodeVector2(stored, Describe(key)) : fallback;
        }

        /// <summary>enum 按底层整数值存。重命名成员不影响存档，重排数值才需要显式迁移。</summary>
        public void Member<TEnum>(string key, ref TEnum value, TEnum fallback = default)
            where TEnum : struct, Enum
        {
            if (!BeginMember(key)) { return; }
            if (Mode == SaveArchiveMode.Writing) { node.Set(key, EnumCodec.Encode(value)); return; }
            value = node.TryGet(key, out JsonValue stored) ? EnumCodec.Decode<TEnum>(stored, Describe(key)) : fallback;
        }

        /// <summary>校验 key 并挡掉 <see cref="SaveArchiveMode.AfterLoad"/>：那一轮再写 fallback 会盖掉刚读到的值。</summary>
        bool BeginMember(string key)
        {
            ValidateKey(key);
            return Mode != SaveArchiveMode.AfterLoad;
        }
    }
}
