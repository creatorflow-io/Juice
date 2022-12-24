namespace Juice.Workflows.Tests.Nodes
{
    internal class TestCatchEvent : IntermediateCatchEvent
    {
        public TestCatchEvent(IStringLocalizer<TestCatchEvent> stringLocalizer) : base(stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Test catch"];
    }
}
