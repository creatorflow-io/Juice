namespace Juice.Workflows.Tests.Nodes
{
    internal class TestCatchEvent : IntermediateCatchEvent
    {
        public TestCatchEvent(IStringLocalizerFactory stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Test catch"];
    }
}
