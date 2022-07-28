namespace Juice.MediatR.IdentifiedCommands.EF
{
    public interface IRequestManager
    {
        Task<bool> CreateRequestForCommandAsync<T>(Guid id);
        Task CompleteRequestAsync(Guid id, bool success);
    }
}
