using System;
using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.Save.Runtime;

namespace Polaris.Save.Integration
{
    /// <summary>
    /// 落盘入口。PolarisSave 序列化失败、或处于只读恢复状态时，在这里把整次写入拦下来。
    /// </summary>
    /// <remarks>
    /// 返回非 null 字符串就是原版约定的"保存失败"信号：<c>COOK.autoSave</c> 和
    /// <c>COOK.createNewSave</c> 都会把它并进 <c>save_failure_announce</c> 并走失败分支。
    /// 拦在这里而不是更早，是因为 <c>saveBinary</c> 才是"写临时文件 → 删旧档 → 改名"的地方；
    /// 不进这个方法，磁盘上的旧存档就一个字节都不会动。
    /// </remarks>
    [HarmonyPatch(typeof(SVD), nameof(SVD.saveBinary), new[] { typeof(SVD.sFile), typeof(ByteArray) })]
    internal static class Patch_SVD_saveBinary
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        static bool Prefix(ByteArray Ba, ref string __result)
        {
            string reason;
            try
            {
                reason = SaveGate.Consume(Ba);
                if (reason == null && SaveRuntime.Instance.IsReadOnlyRecovery)
                {
                    reason = "PolarisSave 处于只读恢复状态：本局读到的模组存档数据有损坏，"
                        + "现在保存会用默认值覆盖掉仍可人工恢复的内容。";
                }
            }
            catch (Exception ex)
            {
                // 失败门自己出问题时按"拦下"处理：宁可这次存不上，也不能拿不确定的状态覆盖旧存档。
                SaveIntegration.Report(ex, "evaluating the PolarisSave save gate");
                reason = $"PolarisSave 无法确认本次保存是否安全：{ex.Message}";
            }

            if (reason == null)
            {
                return true;
            }

            SaveIntegration.Logger.LogWarning($"[PolarisSave] 已阻止本次存档写入：{reason}");
            __result = reason;
            return false;
        }
    }
}
