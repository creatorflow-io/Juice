namespace Juice.EF
{
    public interface IDataEventHandler
    {
        public Task HandleAsync(DataEvent dataEvent);
    }
}
