namespace Juice.BgService
{
    public interface IServiceModel
    {
        Guid? Id { get; }
        string Name { get; }
        public Dictionary<string, object?> Options { get; }
        public string AssemblyQualifiedName { get; }
    }
}
