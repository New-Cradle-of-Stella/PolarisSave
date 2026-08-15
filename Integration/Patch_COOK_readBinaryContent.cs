using System;
using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;

namespace Polaris.Save.Integration
{
    /// <summary>
    /// 原版"存档二进制 → 内存"的唯一入口。原版读到它认识的最后一个字段就返回，并不检查是否
    /// 正好到 EOF，所以尾部容器对它完全透明；这里在它成功之后再去读容器。
    /// </summary>
    /// <remarks>
    /// 优先级取 <see cref="Priority.First"/>，保证在 Core 发布 <c>SaveLoaded</c> 之前，
    /// 模组数据已经就位——订阅方在回调里读 <c>SaveHandle.Current</c> 才拿得到本局的值。
    /// <c>readBinaryContent</c> 是 private，按方法名挂钩。
    /// </remarks>
    [HarmonyPatch(typeof(COOK), "readBinaryContent",
        new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase) })]
    internal static class Patch_COOK_readBinaryContent
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        static void Postfix(bool __result, ByteArray Ba)
        {
            // 原版读档失败时不碰尾部数据：initGameScene 紧接着会落回 newGame，由那边统一重置。
            if (!__result || Ba == null)
            {
                return;
            }

            try
            {
                SaveIntegration.Load(Ba);
            }
            catch (Exception ex)
            {
                // 走到这里说明是 PolarisSave 自己的 bug——内核已经把所有可预期的损坏都变成了
                // 隔离 + 只读恢复，不会往外抛。原版世界照常继续，问题只留在 PolarisSave 这一侧。
                SaveIntegration.Report(ex, "reading the PolarisSave container from the vanilla save");
            }
        }
    }
}
