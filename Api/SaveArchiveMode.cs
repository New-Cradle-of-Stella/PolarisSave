namespace Polaris.Save
{
    /// <summary>一次 <see cref="IPolarisSaveData.Serialize"/> 调用处在哪个方向上。</summary>
    public enum SaveArchiveMode
    {
        /// <summary>把当前内存状态写进存档。</summary>
        Writing,

        /// <summary>把存档内容读进内存。</summary>
        Reading,

        /// <summary>
        /// 全部分区读完之后的迁移回合。此时 <c>Member</c>/<c>ValueList</c>/<c>ValueMap</c> 都是空操作
        /// （否则会拿 fallback 把刚读进来的值盖掉），只有 <c>Child</c>/<c>ChildList</c> 会继续下钻，
        /// 让嵌套对象也有机会迁移。
        /// </summary>
        AfterLoad,
    }
}
