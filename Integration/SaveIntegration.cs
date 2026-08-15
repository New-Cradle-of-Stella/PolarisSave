using System;
using System.Reflection;
using BepInEx.Logging;
using PixelLiner.PixelLinerLib;
using Polaris.Save.Runtime;

namespace Polaris.Save.Integration
{
    /// <summary>
    /// 补丁与存档内核之间的唯一桥梁。原版类型（<c>COOK</c>/<c>SVD</c>/<c>ByteArray</c>）只出现在
    /// Integration 层，内核那边连引用都没有。
    /// </summary>
    internal static class SaveIntegration
    {
        internal static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("PolarisSave");

        static readonly Assembly Self = typeof(SaveIntegration).Assembly;

        /// <summary>把 PolarisSave 容器追加到原版序列化结果的末尾。</summary>
        internal static void Append(ByteArray serialized)
        {
            byte[] container = SaveRuntime.Instance.BuildContainer();

            // 原版刚写完，position 本就在末尾；显式对齐一次，免得依赖别的补丁没动过它。
            serialized.position = serialized.Length;
            serialized.writeBytes(container);
        }

        /// <summary>读取原版存档末尾的 PolarisSave 容器。</summary>
        internal static void Load(ByteArray content)
        {
            // ByteArray 的底层数组通常比有效长度长，必须按 Length 截断，不能按 bytes.Length。
            ulong length = content.Length;
            if (length > int.MaxValue)
            {
                throw new PolarisSaveException($"存档长度 {length} 超出可处理范围。");
            }

            ulong vanillaEnd = content.position;
            int containerStart = SaveRuntime.Instance.Load(content.bytes, (int)length);

            if (containerStart >= 0 && (ulong)containerStart != vanillaEnd)
            {
                // 不算错误：可能是别的模组也往尾部追加了自己的数据。但足够反常，值得留一行日志。
                Logger.LogWarning(
                    $"原版读取结束于 {vanillaEnd}，PolarisSave 容器却起自 {containerStart}，中间有 {(long)containerStart - (long)vanillaEnd} 字节不属于任何一方。");
            }
        }

        internal static void Report(Exception exception, string context)
        {
            Logger.LogError($"[PolarisSave] {context}：{exception}");
            try
            {
                PolarisAPI.Errors.Report(exception, context, Self);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PolarisSave] 上报错误时又失败了：{ex.Message}");
            }
        }
    }
}
