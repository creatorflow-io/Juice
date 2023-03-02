namespace Juice.Workflows.Extensions
{
    public static class DefinitionRepositoryExtensions
    {
        public static async Task<OperationResult> SaveWorkflowContextAsync(this IDefinitionRepository definitionRepo,
            WorkflowContext context, string id, string name, bool @override, CancellationToken token)
        {
            try
            {
                var existing = await definitionRepo.ExistAsync(id, token);
                if (!existing || @override)
                {
                    var definition = existing ?
                            await definitionRepo.GetAsync(id, token)
                            : new WorkflowDefinition(id, name);
                    definition.SetData(context.Processes,
                        context.Nodes.Values.Select(n => new NodeData(n.Record, n.Node.GetType().Name,
                            context.Processes.Any(p => n.IsStartOf(p.Id)), n.Properties)
                        ),
                        context.Flows.Select(n => new FlowData(n.Record, n.Flow.GetType().Name)));
                    var createResult = existing ?
                        await definitionRepo.UpdateAsync(definition, token)
                        : await definitionRepo.CreateAsync(definition, token);
                    return createResult;
                }
                else
                {
                    return OperationResult.Success;
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(ex);
            }
        }
    }
}
