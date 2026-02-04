namespace Juice.XUnit
{
    public static class Waiter
    {
        public static async Task WaitAsync(Func<bool> condition, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
        {
            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tokenSource.CancelAfter(timeout?? TimeSpan.FromSeconds(3));
            while (true)
            {
                if (condition())
                {
                    return;
                }
                if (tokenSource.Token.IsCancellationRequested)
                {
                    return;
                }
                await Task.Delay(100, cancellationToken);
            }
        }
    }
}
