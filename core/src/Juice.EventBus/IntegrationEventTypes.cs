namespace Juice.EventBus
{
    public class IntegrationEventTypes
    {
        private HashSet<Type> _types = new HashSet<Type>();
        public List<Type> EventTypes => _types.ToList();
        public void Register<T>()
        {
            _types.Add(typeof(T));
        }
        public void Register(Type type)
        {
            _types.Add(type);
        }
    }
}
