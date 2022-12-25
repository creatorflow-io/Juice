namespace Juice.Workflows.Nodes.Activities
{
    public class ServiceTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["Service Task"];

        public ServiceTask(IServiceProvider serviceProvider, IStringLocalizer<ServiceTask> stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

    }
}
