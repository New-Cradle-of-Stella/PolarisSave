using System;
using System.Globalization;

namespace Polaris.Save.Serialization
{
    /// <summary>
    /// V1 支持的全部基本值与 JSON 表示之间的转换。规则是"只认精确形状"：
    /// 存的是数字就必须按数字读回，字符串到数字之类的宽松转换一律报错，逼模组作者显式迁移。
    /// </summary>
    internal static class JsonScalars
    {
        const NumberStyles IntegerStyles = NumberStyles.AllowLeadingSign;
        const NumberStyles RealStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // ── 编码

        internal static JsonValue Encode(bool value) => JsonValue.NewBool(value);

        internal static JsonValue Encode(sbyte value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(byte value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(short value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(ushort value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(int value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(uint value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(long value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(ulong value) => JsonValue.NewNumber(value.ToString(Invariant));

        internal static JsonValue Encode(decimal value) => JsonValue.NewNumber(value.ToString(Invariant));

        /// <summary>G9 能让 float 精确往返；NaN/无穷不是合法 JSON 数字，退化成约定好的字符串。</summary>
        internal static JsonValue Encode(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? JsonValue.NewString(NonFiniteText(value))
                : JsonValue.NewNumber(value.ToString("G9", Invariant));

        /// <summary>G17 能让 double 精确往返。</summary>
        internal static JsonValue Encode(double value) =>
            double.IsNaN(value) || double.IsInfinity(value)
                ? JsonValue.NewString(NonFiniteText(value))
                : JsonValue.NewNumber(value.ToString("G17", Invariant));

        /// <summary>char 编码成 UTF-16 码元数字而不是单字符字符串：落单的代理项也能原样往返。</summary>
        internal static JsonValue Encode(char value) => JsonValue.NewNumber(((int)value).ToString(Invariant));

        internal static JsonValue Encode(string value) => JsonValue.NewString(value);

        internal static JsonValue Encode(byte[] value) =>
            value == null ? JsonValue.NewNull() : JsonValue.NewString(Convert.ToBase64String(value));

        internal static JsonValue Encode(Guid value) => JsonValue.NewString(value.ToString("D", Invariant));

        /// <summary>往返格式 "o" 连 <see cref="DateTimeKind"/> 一起保住，本地时间不会在读回后变成 Unspecified。</summary>
        internal static JsonValue Encode(DateTime value) => JsonValue.NewString(value.ToString("o", Invariant));

        internal static JsonValue Encode(SaveVector2 value)
        {
            JsonValue node = JsonValue.NewObject();
            node.Set("x", Encode(value.X));
            node.Set("y", Encode(value.Y));
            return node;
        }

        // ── 解码

        internal static bool DecodeBoolean(JsonValue node, string context)
        {
            if (node.Kind != JsonKind.Bool)
            {
                throw Mismatch(context, "bool", node);
            }

            return node.BoolValue;
        }

        internal static sbyte DecodeSByte(JsonValue node, string context) =>
            sbyte.TryParse(NumberText(node, context, "sbyte"), IntegerStyles, Invariant, out sbyte value)
                ? value
                : throw Range(context, "sbyte", node);

        internal static byte DecodeByte(JsonValue node, string context) =>
            byte.TryParse(NumberText(node, context, "byte"), IntegerStyles, Invariant, out byte value)
                ? value
                : throw Range(context, "byte", node);

        internal static short DecodeInt16(JsonValue node, string context) =>
            short.TryParse(NumberText(node, context, "short"), IntegerStyles, Invariant, out short value)
                ? value
                : throw Range(context, "short", node);

        internal static ushort DecodeUInt16(JsonValue node, string context) =>
            ushort.TryParse(NumberText(node, context, "ushort"), IntegerStyles, Invariant, out ushort value)
                ? value
                : throw Range(context, "ushort", node);

        internal static int DecodeInt32(JsonValue node, string context) =>
            int.TryParse(NumberText(node, context, "int"), IntegerStyles, Invariant, out int value)
                ? value
                : throw Range(context, "int", node);

        internal static uint DecodeUInt32(JsonValue node, string context) =>
            uint.TryParse(NumberText(node, context, "uint"), IntegerStyles, Invariant, out uint value)
                ? value
                : throw Range(context, "uint", node);

        internal static long DecodeInt64(JsonValue node, string context) =>
            long.TryParse(NumberText(node, context, "long"), IntegerStyles, Invariant, out long value)
                ? value
                : throw Range(context, "long", node);

        internal static ulong DecodeUInt64(JsonValue node, string context) =>
            ulong.TryParse(NumberText(node, context, "ulong"), IntegerStyles, Invariant, out ulong value)
                ? value
                : throw Range(context, "ulong", node);

        internal static decimal DecodeDecimal(JsonValue node, string context) =>
            decimal.TryParse(NumberText(node, context, "decimal"), RealStyles, Invariant, out decimal value)
                ? value
                : throw Range(context, "decimal", node);

        internal static float DecodeSingle(JsonValue node, string context)
        {
            if (node.Kind == JsonKind.String)
            {
                return (float)NonFiniteValue(node.Text, context, "float");
            }

            return float.TryParse(NumberText(node, context, "float"), RealStyles, Invariant, out float value)
                ? value
                : throw Range(context, "float", node);
        }

        internal static double DecodeDouble(JsonValue node, string context)
        {
            if (node.Kind == JsonKind.String)
            {
                return NonFiniteValue(node.Text, context, "double");
            }

            return double.TryParse(NumberText(node, context, "double"), RealStyles, Invariant, out double value)
                ? value
                : throw Range(context, "double", node);
        }

        internal static char DecodeChar(JsonValue node, string context) =>
            ushort.TryParse(NumberText(node, context, "char"), IntegerStyles, Invariant, out ushort value)
                ? (char)value
                : throw Range(context, "char", node);

        internal static string DecodeString(JsonValue node, string context)
        {
            if (node.Kind == JsonKind.Null)
            {
                return null;
            }

            if (node.Kind != JsonKind.String)
            {
                throw Mismatch(context, "string", node);
            }

            return node.Text;
        }

        internal static byte[] DecodeBytes(JsonValue node, string context)
        {
            if (node.Kind == JsonKind.Null)
            {
                return null;
            }

            if (node.Kind != JsonKind.String)
            {
                throw Mismatch(context, "byte[]", node);
            }

            try
            {
                return Convert.FromBase64String(node.Text);
            }
            catch (FormatException ex)
            {
                throw new PolarisSaveException($"{context} 的 byte[] 不是合法 Base64。", ex);
            }
        }

        internal static Guid DecodeGuid(JsonValue node, string context)
        {
            if (node.Kind != JsonKind.String || !Guid.TryParseExact(node.Text, "D", out Guid value))
            {
                throw Mismatch(context, "Guid", node);
            }

            return value;
        }

        internal static DateTime DecodeDateTime(JsonValue node, string context)
        {
            if (node.Kind != JsonKind.String
                || !DateTime.TryParseExact(node.Text, "o", Invariant, DateTimeStyles.RoundtripKind, out DateTime value))
            {
                throw Mismatch(context, "DateTime", node);
            }

            return value;
        }

        internal static SaveVector2 DecodeVector2(JsonValue node, string context)
        {
            if (node.Kind != JsonKind.Object
                || !node.TryGet("x", out JsonValue x)
                || !node.TryGet("y", out JsonValue y))
            {
                throw Mismatch(context, "SaveVector2", node);
            }

            return new SaveVector2(DecodeSingle(x, context + ".x"), DecodeSingle(y, context + ".y"));
        }

        // ── 辅助

        static string NumberText(JsonValue node, string context, string expected)
        {
            if (node.Kind != JsonKind.Number)
            {
                throw Mismatch(context, expected, node);
            }

            return node.Text;
        }

        static string NonFiniteText(double value)
        {
            if (double.IsNaN(value))
            {
                return "NaN";
            }

            return double.IsPositiveInfinity(value) ? "Infinity" : "-Infinity";
        }

        static double NonFiniteValue(string text, string context, string expected)
        {
            switch (text)
            {
                case "NaN": return double.NaN;
                case "Infinity": return double.PositiveInfinity;
                case "-Infinity": return double.NegativeInfinity;
                default:
                    throw new PolarisSaveException(
                        $"{context} 期望 {expected}，但存的是字符串 \"{text}\"。字段类型变化必须显式迁移。");
            }
        }

        static PolarisSaveException Mismatch(string context, string expected, JsonValue node) =>
            new PolarisSaveException($"{context} 期望 {expected}，但存的是 {node.Kind}。字段类型变化必须显式迁移。");

        static PolarisSaveException Range(string context, string expected, JsonValue node) =>
            new PolarisSaveException($"{context} 的值 {node.Text} 无法表示成 {expected}。");
    }
}
