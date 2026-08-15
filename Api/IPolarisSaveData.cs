namespace Polaris.Save
{
    /// <summary>
    /// 一份可存档的模组数据。<b>同一个 <see cref="Serialize"/> 同时用于保存和读取</b>：
    /// 字段声明只写一遍，两个方向自动对齐，不会出现"保存写了三个字段、读取只读两个"这种错位。
    /// </summary>
    /// <remarks>
    /// <see cref="Serialize"/> 会被调用三次，靠 <see cref="SaveArchive.Mode"/> 区分：
    /// <list type="number">
    ///   <item>保存时以 <see cref="SaveArchiveMode.Writing"/> 调用一次。</item>
    ///   <item>读档时以 <see cref="SaveArchiveMode.Reading"/> 调用一次。</item>
    ///   <item>全部分区读完后，以 <see cref="SaveArchiveMode.AfterLoad"/> 再调用一次，用于迁移和补默认集合。</item>
    /// </list>
    /// 实现类必须能被无参构造（或由注册时提供的工厂构造）出一个"全新存档"状态。
    /// </remarks>
    public interface IPolarisSaveData
    {
        void Serialize(SaveArchive archive);
    }
}
