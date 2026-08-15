using System.Collections.Generic;
using System.Text;
using Polaris.Save.Format;

namespace Polaris.Save.Serialization
{
    /// <summary>把 <see cref="JsonValue"/> 写成紧凑的 UTF-8 JSON。输出是确定性的：同样的文档永远得到同样的字节。</summary>
    internal static class JsonWriter
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static byte[] WriteToUtf8(JsonValue value)
        {
            var builder = new StringBuilder(256);
            Write(builder, value, 1);
            return Utf8.GetBytes(builder.ToString());
        }

        internal static string WriteToString(JsonValue value)
        {
            var builder = new StringBuilder(256);
            Write(builder, value, 1);
            return builder.ToString();
        }

        static void Write(StringBuilder builder, JsonValue value, int depth)
        {
            if (depth > SaveFormatLimits.MaxDepth)
            {
                throw new PolarisSaveException($"对象嵌套超过 {SaveFormatLimits.MaxDepth} 层。");
            }

            switch (value.Kind)
            {
                case JsonKind.Null:
                    builder.Append("null");
                    break;
                case JsonKind.Bool:
                    builder.Append(value.BoolValue ? "true" : "false");
                    break;
                case JsonKind.Number:
                    builder.Append(value.Text);
                    break;
                case JsonKind.String:
                    WriteString(builder, value.Text);
                    break;
                case JsonKind.Array:
                    builder.Append('[');
                    for (int i = 0; i < value.Count; i++)
                    {
                        if (i != 0)
                        {
                            builder.Append(',');
                        }

                        Write(builder, value[i], depth + 1);
                    }

                    builder.Append(']');
                    break;
                case JsonKind.Object:
                    builder.Append('{');
                    IReadOnlyList<string> keys = value.Keys;
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (i != 0)
                        {
                            builder.Append(',');
                        }

                        WriteString(builder, keys[i]);
                        builder.Append(':');
                        Write(builder, value[i], depth + 1);
                    }

                    builder.Append('}');
                    break;
            }
        }

        static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        // 控制字符和落单的代理项都转义：后者直接编码成 UTF-8 会被替换成 U+FFFD，静默改数据。
                        if (c < 0x20 || char.IsSurrogate(c))
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
