namespace Juice.EventBus.Policies
{
    public interface IRetryPolicyProvider<TPolicy>
    {
        /// <summary>
        /// Retrieves the appropriate retry policy for a given source and number of attempts.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="attempts"></param>
        /// <returns></returns>
        ValueTask<TPolicy?> GetRetryPolicyForSourceAsync(string? source, int attempts);
    }
}
