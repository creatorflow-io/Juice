using Juice.Modular;

namespace Juice.SimpleModule
{
    [CustomFeature(Required = true)]
    [Obsolete("This module is obsolete and will be removed in future versions. Please use the new Juice framework features instead.")]
    public class SimpleModule: ModuleStartup
    {

    }
}
