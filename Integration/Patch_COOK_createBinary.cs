using System;
using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.Save.Runtime;

namespace Polaris.Save.Integration
{
    /// <summary>
    /// 手动保存和自动保存共同的序列化入口。Prefix 冻结注册，Postfix 把 PolarisSave 容器
    /// 追加在原版数据之后。
    /// </summary>
    /// <remarks>
    /// 优先级取 <see cref="Priority.First"/>：Core 在同一个方法上也挂了 Postfix 去发布
    /// <c>SaveSerialized</c>，必须等 PolarisSave 追加完成、字节数已是最终值之后再发。
    /// 全部补丁都由 Core 用同一个 Harmony id 应用，所以只能靠显式优先级排序，不能用
    /// <c>HarmonyBefore</c> 按 owner 区分。
    /// </remarks>
    [HarmonyPatch(typeof(COOK), nameof(COOK.createBinary),
        new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    internal static class Patch_COOK_createBinary
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        static void Prefix()
        {
            // 保存一旦开始，分区名单就不能再变了，否则同一局里两次存档的分区集合会不一致。
            SaveRuntime.Instance.Freeze();
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        static void Postfix(ByteArray __result)
        {
            if (__result == null)
            {
                return;
            }

            try
            {
                SaveIntegration.Append(__result);
                SaveGate.Clear(__result);
            }
            catch (Exception ex)
            {
                // 追加失败时绝不能让这份半成品落盘：污染它，SVD.saveBinary 会认出来并拒绝写入。
                SaveGate.Poison(__result, $"PolarisSave 序列化失败，已阻止覆盖存档：{ex.Message}");
                SaveIntegration.Report(ex, "appending the PolarisSave container to the vanilla save");
            }
        }
    }
}
