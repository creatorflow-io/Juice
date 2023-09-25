namespace Juice.BgService.Management
{
    public interface IServiceFactory
    {
        //IManagedService? CreateService(Type type);
        IManagedService? CreateService(string typeAssemblyQualifiedName);
        bool IsServiceExists(string typeAssemblyQualifiedName);
    }
}
