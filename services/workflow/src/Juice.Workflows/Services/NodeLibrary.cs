using System.Collections.Concurrent;
using Juice.Workflows.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Workflows.Services
{
    internal class NodeLibrary : INodeLibrary
    {
        /// <summary>
        /// Init static node types to register type from service builder.
        /// It must be static because the instance of singleton service in service build time
        /// by called services.BuildServiceProvider().GetService<T>()
        /// is not same the instance after application started-up.
        /// </summary>
        private static ConcurrentDictionary<string, Type> _nodeTyps = new ConcurrentDictionary<string, Type>();

        public void TryRegister(Type type)
        {
            if (!type.IsAssignableTo(typeof(INode)))
            {
                throw new ArgumentException($"Type {type.FullName} is not inherit from INode");
            }
            var key = type.Name;
            if (!_nodeTyps.ContainsKey(key))
            {
                _nodeTyps[key] = type;
            }
        }

        public INode CreateInstance(string name, IServiceProvider serviceProvider)
        {
            if (!_nodeTyps.ContainsKey(name))
            {
                throw new Exception($"Service {name} was not registered to library.");
            }
            return serviceProvider.GetRequiredService(_nodeTyps[name]) is INode node ? node
                : serviceProvider.GetRequiredService<DummyNode>();

        }

        public IEnumerable<Type> GetAllTypes()
        {
            return _nodeTyps.Values;
        }
    }
}
