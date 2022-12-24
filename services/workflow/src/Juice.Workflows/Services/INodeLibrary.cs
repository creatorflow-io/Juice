namespace Juice.Workflows.Services
{

    public interface INodeLibrary
    {
        /// <summary>
        /// Resolve new instance of activity from <see cref="IServiceProvider"/> by name
        /// <para>NOTE: all activities must be registering with Transient scope</para>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        INode CreateInstance(string name, IServiceProvider serviceProvider);

        void TryRegister(Type type);

        IEnumerable<Type> GetAllTypes();
    }

    public static class NodeLibraryExtensions
    {
        public static void TryRegister<T>(this INodeLibrary library)
            => library.TryRegister(typeof(T));
    }
}
