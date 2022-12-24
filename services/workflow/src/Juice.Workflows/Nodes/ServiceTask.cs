namespace Juice.Workflows.Nodes
{
    public class ServiceTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["Service Task"];

        public ServiceTask(ILoggerFactory logger, IStringLocalizer<ServiceTask> stringLocalizer)
            : base(logger, stringLocalizer)
        {
        }

    }
}
