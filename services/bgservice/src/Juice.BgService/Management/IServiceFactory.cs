namespace Juice.BgService.Management
{
    public interface IServiceFactory
    {
        IManagedService? CreateService(Type type);
    }
}
