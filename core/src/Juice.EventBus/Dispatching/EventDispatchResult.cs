
namespace Juice.EventBus.Dispatching
{
    public enum EventDispatchResult
    {
        Failure,
        Success,
        NotHandled,
        Duplicated
    }
}
