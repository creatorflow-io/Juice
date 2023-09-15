namespace Juice.Audit
{
    public interface IAuditService
    {
        Task<IOperationResult> CommitAuditInformationAsync(CancellationToken token);
    }
}
