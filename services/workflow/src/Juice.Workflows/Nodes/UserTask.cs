
namespace Juice.Workflows.Nodes
{
    public class UserTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["User Task"];


        public UserTask(ILoggerFactory logger, IStringLocalizer<UserTask> stringLocalizer)
            : base(logger, stringLocalizer)
        {
        }

    }
}
