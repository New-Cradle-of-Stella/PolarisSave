using Polaris.Components;

namespace Polaris.Save
{
    /// <summary>模组存档能力的组件入口。</summary>
    public sealed class PolarisSaveComponent : PolarisComponent
    {
        public override string Id => "PolarisSave";
        public override int Order => 700;
    }
}
