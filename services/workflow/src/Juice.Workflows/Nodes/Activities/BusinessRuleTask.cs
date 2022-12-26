namespace Juice.Workflows.Nodes.Activities
{
    public class BusinessRuleTask : Activity
    {
        public BusinessRuleTask(IServiceProvider serviceProvider,
            IStringLocalizerFactory stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Business Rule Task"];

    }
}
