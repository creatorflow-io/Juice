namespace Juice.BgService.Management
{
    public interface IServiceStore
    {
        event EventHandler<EventArgs> OnChanged;
        Task<IEnumerable<IServiceModel>> GetServicesModelAsync(CancellationToken token);

    }
}
