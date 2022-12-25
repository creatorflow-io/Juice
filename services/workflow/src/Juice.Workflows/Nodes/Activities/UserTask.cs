
namespace Juice.Workflows.Nodes.Activities
{
    public class UserTask : Activity
    {
        public override LocalizedString DisplayText => Localizer["User Task"];


        public UserTask(IServiceProvider serviceProvider, IStringLocalizer<UserTask> stringLocalizer)
            : base(serviceProvider, stringLocalizer)
        {
        }

    }
}
