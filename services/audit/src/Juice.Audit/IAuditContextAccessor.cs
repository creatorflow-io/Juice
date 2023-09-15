namespace Juice.Audit
{
    public interface IAuditContextAccessor : IDisposable
    {
        AuditContext? AuditContext { get; }
        void Init(string action, string? user);
    }
}
