namespace Juice.Workflows.Nodes.Activities
{
    public class BusinessRuleTask : Activity
    {
        public BusinessRuleTask(IServiceProvider serviceProvider,
            IStringLocalizer<BusinessRuleTask> stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

        public override LocalizedString DisplayText => Localizer["Business Rule Task"];

    }
}
