namespace Juice.BgService.Management
{
    public interface IServiceRepository
    {
        event EventHandler<EventArgs> OnChanged;
        Task<IEnumerable<IServiceModel>> GetServicesModelAsync(CancellationToken token);

    }
}
