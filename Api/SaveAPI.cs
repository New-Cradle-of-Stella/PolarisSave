using System;
using System.Collections.Generic;
using Polaris.Save.Runtime;

namespace Polaris.Save
{
    /// <summary>
    /// PolarisSave 的公开入口：模组用一个稳定字符串 ID 注册若干分区，PolarisSave 把它们打成一个
    /// 容器追加在原版 <c>.aicsave</c> 数据末尾。原版读档只读它认识的那一段就返回，因此尾部容器
    /// 对原版完全透明。
    /// </summary>
    /// <example>
    /// <code>
    /// public static class MyModSave
    /// {
    ///     public static readonly SaveHandle&lt;MyWorldData&gt; World =
    ///         SaveAPI.Register&lt;MyWorldData&gt;("com.example.my-mod/world", version: 2);
    /// }
    ///
    /// MyModSave.World.Current.Counter++;
    /// </code>
    /// </example>
    public static class SaveAPI
    {
        /// <summary>注册一个分区。<typeparamref name="T"/> 的无参构造出来的状态就是"全新存档"的默认值。</summary>
        /// <param name="id">全局唯一的稳定 ID，建议用 <c>BepInEx GUID/分区名</c>，发布后不能改名。</param>
        /// <param name="version">当前代码所用的 schema 版本，从 1 开始。</param>
        /// <exception cref="PolarisSaveException">ID 非法、ID 重复，或注册已冻结。</exception>
        public static SaveHandle<T> Register<T>(string id, ushort version = 1)
            where T : class, IPolarisSaveData, new()
            => SaveRuntime.Instance.Register(id, version, () => new T());

        /// <summary>注册一个分区，并指定用来创建实例的工厂。</summary>
        public static SaveHandle<T> Register<T>(string id, ushort version, Func<T> factory)
            where T : class, IPolarisSaveData
            => SaveRuntime.Instance.Register(id, version, factory);

        /// <summary>注册是否已冻结。第一次新游戏、读档或保存开始时冻结。</summary>
        public static bool IsRegistrationFrozen => SaveRuntime.Instance.IsFrozen;

        /// <summary>
        /// 是否处于只读恢复状态。为 <c>true</c> 时 PolarisSave 会拦下普通保存，
        /// 以免用默认值覆盖磁盘上仍可人工恢复的模组数据。
        /// </summary>
        public static bool IsReadOnlyRecovery => SaveRuntime.Instance.IsReadOnlyRecovery;

        /// <summary>本次读档发现的全部问题，按发现顺序排列。</summary>
        public static IReadOnlyList<SaveIssue> Issues => SaveRuntime.Instance.Issues;

        /// <summary>
        /// 明确丢弃某个分区的存档数据：清掉它的问题记录与保留字节，并把它重置成默认值。
        /// 这是一步不可逆的破坏性操作，只应由用户在诊断界面上显式触发——PolarisSave 自己绝不自动丢数据。
        /// </summary>
        /// <returns>是否真的改变了状态。</returns>
        public static bool DiscardPartition(string id) => SaveRuntime.Instance.DiscardPartition(id);

        /// <summary>
        /// 明确丢弃整个尾部容器的全部模组数据，回到"这局没有模组存档"的状态。同样只应由用户显式触发。
        /// </summary>
        /// <returns>是否真的改变了状态。</returns>
        public static bool DiscardAllModData() => SaveRuntime.Instance.DiscardAllModData();
    }
}
