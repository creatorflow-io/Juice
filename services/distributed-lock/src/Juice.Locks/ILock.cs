namespace Juice.Locks
{
    public interface ILock : IDisposable
    {
        public string Key { get; }
        public string Value { get; }
    }

}
