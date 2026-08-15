using System;
using HarmonyLib;
using nel;
using Polaris.Save.Runtime;

namespace Polaris.Save.Integration
{
    /// <summary>
    /// 新游戏初始化的唯一入口，读档失败时原版也会落到这里。全部分区在此换成新实例，
    /// 上一局的数据、保留字节和只读恢复状态一并清掉。
    /// </summary>
    /// <remarks>
    /// 清掉恢复状态是有意的：恢复状态保护的是"磁盘上还能救的那一局数据"，开了新游戏就不再
    /// 存在这样的数据；玩家此时若主动存到那个坏档位上，那是一次明确的选择。
    /// 优先级取 <see cref="Priority.First"/>，保证在 Core 发布 <c>NewGameStarted</c> 之前重置完成。
    /// </remarks>
    [HarmonyPatch(typeof(COOK), nameof(COOK.newGame), new[] { typeof(NelM2DBase), typeof(bool) })]
    internal static class Patch_COOK_newGame
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        static void Postfix()
        {
            try
            {
                SaveRuntime.Instance.ResetForNewGame();
            }
            catch (Exception ex)
            {
                SaveIntegration.Report(ex, "resetting PolarisSave partitions for a new game");
            }
        }
    }
}
