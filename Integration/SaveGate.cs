using System.Runtime.CompilerServices;
using PixelLiner.PixelLinerLib;

namespace Polaris.Save.Integration
{
    /// <summary>
    /// 保存失败门。<c>COOK.createBinary</c> 里 PolarisSave 追加失败时，这里记下"具体是哪个
    /// ByteArray 被污染了"，随后 <c>SVD.saveBinary</c> 认出同一个实例就拒绝落盘，旧存档保持不动。
    /// </summary>
    /// <remarks>
    /// 用实例身份而不是一个布尔标志：<c>createBinary</c> 未必每次都紧跟着 <c>saveBinary</c>，
    /// 一个粘着的全局标志会把后面某次本来正常的保存也一起拦掉。
    /// </remarks>
    internal static class SaveGate
    {
        static readonly ConditionalWeakTable<ByteArray, string> Poisoned = new ConditionalWeakTable<ByteArray, string>();

        internal static void Poison(ByteArray serialized, string reason)
        {
            if (serialized == null)
            {
                return;
            }

            Poisoned.Remove(serialized);
            Poisoned.Add(serialized, reason);
        }

        /// <summary>取出并清掉污染标记；没有被污染则返回 <c>null</c>。</summary>
        internal static string Consume(ByteArray serialized)
        {
            if (serialized == null || !Poisoned.TryGetValue(serialized, out string reason))
            {
                return null;
            }

            Poisoned.Remove(serialized);
            return reason;
        }

        internal static void Clear(ByteArray serialized)
        {
            if (serialized != null)
            {
                Poisoned.Remove(serialized);
            }
        }
    }
}
