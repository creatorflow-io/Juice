namespace Juice.Workflows.Models
{
    public interface IFlow : IDisposable
    {

        Task<bool> PreSelectCheckAsync(WorkflowContext context, NodeContext source, NodeContext dest, FlowContext flow);

    }

}
