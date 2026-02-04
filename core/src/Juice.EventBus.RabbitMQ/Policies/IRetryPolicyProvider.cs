namespace Juice.EventBus.RabbitMQ.Policies
{
    internal interface IRetryPolicyProvider
    {
        /// <summary>
        /// Retrieves the appropriate retry policy for a given source and number of attempts.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="attempts"></param>
        /// <returns></returns>
        ValueTask<RetryPolicy?> GetRetryPolicyForSourceAsync(string? source, int attempts);
    }
}
