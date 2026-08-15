using System.Text;
using Polaris.Save.Format;

namespace Polaris.Save.Runtime
{
    /// <summary>
    /// 分区 ID 的语法规则。ID 一旦发布就不能改名，所以宁可在注册时把不规范的写法拦掉，
    /// 也不要让它进到存档里再变成永久的历史包袱。
    /// </summary>
    internal static class SavePartitionId
    {
        /// <summary>建议格式是 <c>BepInEx GUID/分区名</c>，例如 <c>com.example.my-mod/world</c>。</summary>
        internal static void Validate(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new PolarisSaveException("分区 ID 不能为空。");
            }

            if (Encoding.UTF8.GetByteCount(id) > SaveFormatLimits.MaxIdBytes)
            {
                throw new PolarisSaveException(
                    $"分区 ID \"{id}\" 的 UTF-8 长度超过 {SaveFormatLimits.MaxIdBytes} 字节。");
            }

            if (id[0] == '/' || id[id.Length - 1] == '/')
            {
                throw new PolarisSaveException($"分区 ID \"{id}\" 不能以 '/' 开头或结尾。");
            }

            char previous = '\0';
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '.' || c == '-' || c == '_' || c == '/';
                if (!ok)
                {
                    throw new PolarisSaveException(
                        $"分区 ID \"{id}\" 含有非法字符 '{c}'；只允许 ASCII 字母、数字和 . - _ / 。");
                }

                if (c == '/' && previous == '/')
                {
                    throw new PolarisSaveException($"分区 ID \"{id}\" 里不能出现连续的 '/'。");
                }

                previous = c;
            }
        }
    }
}
