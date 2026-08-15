using System;
using System.Collections.Generic;

namespace Polaris.Save.Serialization
{
    /// <summary>
    /// <c>ValueList&lt;T&gt;</c> / <c>ValueMap&lt;T&gt;</c> 的类型分发。支持的类型是一张写死的白名单：
    /// 表里没有的类型直接报错，不存在"用反射把存档字节还原成任意 CLR 类型"的路径。
    /// </summary>
    internal static class SaveValueCodec
    {
        static readonly Dictionary<Type, object> Converters = BuildConverters();

        static readonly Dictionary<Type, object> EnumConverters = new Dictionary<Type, object>();

        internal static bool IsSupported(Type type) => TryGetConverter(type, out _);

        internal static JsonValue Encode<T>(T value, string context)
        {
            if (!TryGetTypedConverter(out ISaveValueConverter<T> converter))
            {
                throw Unsupported(typeof(T), context);
            }

            return converter.Encode(value);
        }

        internal static T Decode<T>(JsonValue node, string context)
        {
            if (!TryGetTypedConverter(out ISaveValueConverter<T> converter))
            {
                throw Unsupported(typeof(T), context);
            }

            return converter.Decode(node, context);
        }

        static bool TryGetTypedConverter<T>(out ISaveValueConverter<T> converter)
        {
            bool found = TryGetConverter(typeof(T), out object boxed);
            converter = found ? (ISaveValueConverter<T>)boxed : null;
            return found;
        }

        static bool TryGetConverter(Type type, out object converter)
        {
            if (Converters.TryGetValue(type, out converter))
            {
                return true;
            }

            if (!type.IsEnum)
            {
                return false;
            }

            lock (EnumConverters)
            {
                if (!EnumConverters.TryGetValue(type, out converter))
                {
                    converter = Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(type));
                    EnumConverters[type] = converter;
                }
            }

            return true;
        }

        static PolarisSaveException Unsupported(Type type, string context) =>
            new PolarisSaveException(
                $"{context} 的元素类型 {type.FullName} 不在 PolarisSave 支持的值类型里。"
                + "受支持的是 bool、整数、float/double/decimal、string、char、byte[]、Guid、DateTime、enum 和 SaveVector2；"
                + "自定义类型请实现 IPolarisSaveData 并用 ChildList。");

        static Dictionary<Type, object> BuildConverters() =>
            new Dictionary<Type, object>
            {
                [typeof(bool)] = new Converter<bool>(JsonScalars.Encode, JsonScalars.DecodeBoolean),
                [typeof(sbyte)] = new Converter<sbyte>(JsonScalars.Encode, JsonScalars.DecodeSByte),
                [typeof(byte)] = new Converter<byte>(JsonScalars.Encode, JsonScalars.DecodeByte),
                [typeof(short)] = new Converter<short>(JsonScalars.Encode, JsonScalars.DecodeInt16),
                [typeof(ushort)] = new Converter<ushort>(JsonScalars.Encode, JsonScalars.DecodeUInt16),
                [typeof(int)] = new Converter<int>(JsonScalars.Encode, JsonScalars.DecodeInt32),
                [typeof(uint)] = new Converter<uint>(JsonScalars.Encode, JsonScalars.DecodeUInt32),
                [typeof(long)] = new Converter<long>(JsonScalars.Encode, JsonScalars.DecodeInt64),
                [typeof(ulong)] = new Converter<ulong>(JsonScalars.Encode, JsonScalars.DecodeUInt64),
                [typeof(float)] = new Converter<float>(JsonScalars.Encode, JsonScalars.DecodeSingle),
                [typeof(double)] = new Converter<double>(JsonScalars.Encode, JsonScalars.DecodeDouble),
                [typeof(decimal)] = new Converter<decimal>(JsonScalars.Encode, JsonScalars.DecodeDecimal),
                [typeof(char)] = new Converter<char>(JsonScalars.Encode, JsonScalars.DecodeChar),
                [typeof(string)] = new Converter<string>(JsonScalars.Encode, JsonScalars.DecodeString),
                [typeof(byte[])] = new Converter<byte[]>(JsonScalars.Encode, JsonScalars.DecodeBytes),
                [typeof(Guid)] = new Converter<Guid>(JsonScalars.Encode, JsonScalars.DecodeGuid),
                [typeof(DateTime)] = new Converter<DateTime>(JsonScalars.Encode, JsonScalars.DecodeDateTime),
                [typeof(SaveVector2)] = new Converter<SaveVector2>(JsonScalars.Encode, JsonScalars.DecodeVector2),
            };

        interface ISaveValueConverter<T>
        {
            JsonValue Encode(T value);

            T Decode(JsonValue node, string context);
        }

        sealed class Converter<T> : ISaveValueConverter<T>
        {
            readonly Func<T, JsonValue> encode;
            readonly Func<JsonValue, string, T> decode;

            internal Converter(Func<T, JsonValue> encode, Func<JsonValue, string, T> decode)
            {
                this.encode = encode;
                this.decode = decode;
            }

            public JsonValue Encode(T value) => encode(value);

            public T Decode(JsonValue node, string context) => decode(node, context);
        }

        /// <summary>enum 按底层整数值存。名字改了不影响存档，值重排了才需要迁移。</summary>
        sealed class EnumConverter<T> : ISaveValueConverter<T>
            where T : struct, Enum
        {
            public JsonValue Encode(T value) => EnumCodec.Encode(value);

            public T Decode(JsonValue node, string context) => EnumCodec.Decode<T>(node, context);
        }
    }

    /// <summary>
    /// enum 与 JSON 数字之间的转换，按底层类型精确往返。名字改了不影响存档，值重排了才需要迁移。
    /// 解码走底层类型的解析器而不是 <c>Enum.ToObject(long)</c>——后者会把超范围的值静默截断。
    /// </summary>
    internal static class EnumCodec
    {
        internal static JsonValue Encode<T>(T value)
            where T : struct, Enum
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            return Enum.GetUnderlyingType(typeof(T)) == typeof(ulong)
                ? JsonScalars.Encode(Convert.ToUInt64(value, invariant))
                : JsonScalars.Encode(Convert.ToInt64(value, invariant));
        }

        internal static T Decode<T>(JsonValue node, string context)
            where T : struct, Enum
        {
            Type underlying = Enum.GetUnderlyingType(typeof(T));
            object raw;
            if (underlying == typeof(sbyte)) { raw = JsonScalars.DecodeSByte(node, context); }
            else if (underlying == typeof(byte)) { raw = JsonScalars.DecodeByte(node, context); }
            else if (underlying == typeof(short)) { raw = JsonScalars.DecodeInt16(node, context); }
            else if (underlying == typeof(ushort)) { raw = JsonScalars.DecodeUInt16(node, context); }
            else if (underlying == typeof(int)) { raw = JsonScalars.DecodeInt32(node, context); }
            else if (underlying == typeof(uint)) { raw = JsonScalars.DecodeUInt32(node, context); }
            else if (underlying == typeof(long)) { raw = JsonScalars.DecodeInt64(node, context); }
            else if (underlying == typeof(ulong)) { raw = JsonScalars.DecodeUInt64(node, context); }
            else { throw new PolarisSaveException($"{context} 的 enum 底层类型 {underlying.FullName} 不受支持。"); }

            return (T)Enum.ToObject(typeof(T), raw);
        }
    }
}
