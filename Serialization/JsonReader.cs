using System;
using System.Globalization;
using System.Text;
using Polaris.Save.Format;

namespace Polaris.Save.Serialization
{
    /// <summary>
    /// 严格的 JSON 解析器。宁可报错也不宽松接受：payload 是存档数据，一个被默默"修正"的畸形文档
    /// 比一个被隔离的坏分区危险得多。
    /// </summary>
    internal static class JsonReader
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static JsonValue ReadUtf8(byte[] payload)
        {
            string text;
            try
            {
                text = StrictUtf8.GetString(payload);
            }
            catch (ArgumentException)
            {
                throw new PolarisSaveException("分区 payload 不是合法 UTF-8。");
            }

            return Read(text);
        }

        internal static JsonValue Read(string text)
        {
            int position = 0;
            JsonValue value = ParseValue(text, ref position, 1);
            SkipWhitespace(text, ref position);
            if (position != text.Length)
            {
                throw Fail(position, "文档末尾存在多余内容。");
            }

            return value;
        }

        static JsonValue ParseValue(string text, ref int position, int depth)
        {
            if (depth > SaveFormatLimits.MaxDepth)
            {
                throw Fail(position, $"对象嵌套超过 {SaveFormatLimits.MaxDepth} 层。");
            }

            SkipWhitespace(text, ref position);
            if (position >= text.Length)
            {
                throw Fail(position, "文档意外结束。");
            }

            char c = text[position];
            switch (c)
            {
                case '{':
                    return ParseObject(text, ref position, depth);
                case '[':
                    return ParseArray(text, ref position, depth);
                case '"':
                    return JsonValue.NewString(ParseString(text, ref position));
                case 't':
                    Expect(text, ref position, "true");
                    return JsonValue.NewBool(true);
                case 'f':
                    Expect(text, ref position, "false");
                    return JsonValue.NewBool(false);
                case 'n':
                    Expect(text, ref position, "null");
                    return JsonValue.NewNull();
                default:
                    return JsonValue.NewNumber(ParseNumber(text, ref position));
            }
        }

        static JsonValue ParseObject(string text, ref int position, int depth)
        {
            JsonValue result = JsonValue.NewObject();
            position++;
            SkipWhitespace(text, ref position);
            if (Peek(text, position) == '}')
            {
                position++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(text, ref position);
                if (Peek(text, position) != '"')
                {
                    throw Fail(position, "对象成员名必须是字符串。");
                }

                string key = ParseString(text, ref position);
                SkipWhitespace(text, ref position);
                if (Peek(text, position) != ':')
                {
                    throw Fail(position, "对象成员名后缺少冒号。");
                }

                position++;
                JsonValue value = ParseValue(text, ref position, depth + 1);

                // 重复 key 只可能来自畸形或被篡改的 payload，静默取其一会让读到的数据取决于实现细节。
                if (result.ContainsKey(key))
                {
                    throw Fail(position, $"对象里出现重复成员名：{key}。");
                }

                result.Set(key, value);

                SkipWhitespace(text, ref position);
                char next = Peek(text, position);
                if (next == ',')
                {
                    position++;
                    continue;
                }

                if (next == '}')
                {
                    position++;
                    return result;
                }

                throw Fail(position, "对象成员之间缺少逗号或右花括号。");
            }
        }

        static JsonValue ParseArray(string text, ref int position, int depth)
        {
            JsonValue result = JsonValue.NewArray();
            position++;
            SkipWhitespace(text, ref position);
            if (Peek(text, position) == ']')
            {
                position++;
                return result;
            }

            while (true)
            {
                result.Add(ParseValue(text, ref position, depth + 1));
                SkipWhitespace(text, ref position);
                char next = Peek(text, position);
                if (next == ',')
                {
                    position++;
                    continue;
                }

                if (next == ']')
                {
                    position++;
                    return result;
                }

                throw Fail(position, "数组元素之间缺少逗号或右方括号。");
            }
        }

        static string ParseString(string text, ref int position)
        {
            position++;
            var builder = new StringBuilder();
            while (true)
            {
                if (position >= text.Length)
                {
                    throw Fail(position, "字符串没有结束引号。");
                }

                char c = text[position++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    if (c < 0x20)
                    {
                        throw Fail(position, "字符串里出现未转义的控制字符。");
                    }

                    builder.Append(c);
                    continue;
                }

                if (position >= text.Length)
                {
                    throw Fail(position, "转义序列没有结束。");
                }

                char escape = text[position++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length
                            || !ushort.TryParse(
                                text.Substring(position, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out ushort code))
                        {
                            throw Fail(position, "\\u 转义后不是四位十六进制。");
                        }

                        builder.Append((char)code);
                        position += 4;
                        break;
                    default:
                        throw Fail(position, $"不认识的转义字符 \\{escape}。");
                }
            }
        }

        static string ParseNumber(string text, ref int position)
        {
            int start = position;
            if (Peek(text, position) == '-')
            {
                position++;
            }

            int digits = SkipDigits(text, ref position);
            if (digits == 0)
            {
                throw Fail(position, "数字缺少整数部分。");
            }

            // JSON 不允许前导零（-0 与 0 本身除外）。
            if (digits > 1 && text[position - digits] == '0')
            {
                throw Fail(position, "数字存在前导零。");
            }

            if (Peek(text, position) == '.')
            {
                position++;
                if (SkipDigits(text, ref position) == 0)
                {
                    throw Fail(position, "小数点后缺少数字。");
                }
            }

            char exponent = Peek(text, position);
            if (exponent == 'e' || exponent == 'E')
            {
                position++;
                char sign = Peek(text, position);
                if (sign == '+' || sign == '-')
                {
                    position++;
                }

                if (SkipDigits(text, ref position) == 0)
                {
                    throw Fail(position, "指数部分缺少数字。");
                }
            }

            return text.Substring(start, position - start);
        }

        static int SkipDigits(string text, ref int position)
        {
            int start = position;
            while (position < text.Length && text[position] >= '0' && text[position] <= '9')
            {
                position++;
            }

            return position - start;
        }

        static void SkipWhitespace(string text, ref int position)
        {
            while (position < text.Length)
            {
                char c = text[position];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                {
                    return;
                }

                position++;
            }
        }

        static void Expect(string text, ref int position, string literal)
        {
            if (position + literal.Length > text.Length
                || string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
            {
                throw Fail(position, $"期望字面量 {literal}。");
            }

            position += literal.Length;
        }

        static char Peek(string text, int position) => position < text.Length ? text[position] : '\0';

        static PolarisSaveException Fail(int position, string message) =>
            new PolarisSaveException($"分区 payload 的 JSON 在第 {position} 个字符处无效：{message}");
    }
}
