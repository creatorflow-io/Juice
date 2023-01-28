using Juice.MediatR;
using Juice.Timers.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.Timers
{
    internal class Timer : ITimer
    {
        public Guid Id { get; private set; }
        public bool IsCompleted { get; private set; }

        protected static int globalCounter = 0;

        protected CancellationTokenSource? _shutdown;
        protected Task? _backgroundTask;

        protected TimerRequest? _request;

        protected IServiceScopeFactory _scopeFactory;

        protected TimerOptions _options;

        public Timer(IServiceScopeFactory scopeFactory, IOptionsSnapshot<TimerOptions> options)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            Interlocked.Increment(ref globalCounter);
        }

        public Task StartAsync(TimerRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.Id == Guid.Empty)
            {
                throw new ArgumentException("Request Id is required and could not be empty");
            }
            Id = request.Id;
            if (request.IsCompleted)
            {
                IsCompleted = true;
                return Task.CompletedTask;
            }

            if (request.AbsoluteExpired > DateTimeOffset.Now.Add(_options.MaxWaitTime))
            {
                return Task.CompletedTask;
            }


            _shutdown = new CancellationTokenSource();
            _request = request;
            _backgroundTask = request.IsExpired ? Task.Run(DispatchImmediatelyAsync) : Task.Run(WaitAndDispatchAsync);
            return Task.CompletedTask;
        }

        public async Task CancelAsync()
        {
            _shutdown?.Cancel();
            if (_backgroundTask != null)
            {
                await Task.WhenAny(_backgroundTask, Task.Delay(500));
            }
        }

        protected virtual async Task DispatchImmediatelyAsync()
        {
            if (_request != null)
            {
                try
                {
                    await DispatchAsync(_request);
                }
                catch (Exception)
                {
                }
            }
        }

        protected virtual async Task WaitAndDispatchAsync()
        {
            if (_request != null)
            {
                try
                {
                    await Task.Delay(_request.AbsoluteExpired - DateTimeOffset.Now, _shutdown.Token);
                    await DispatchAsync(_request);
                }
                catch (Exception)
                {
                }
            }
        }

        private async Task DispatchAsync(TimerRequest request)
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var rs = await mediator.Send(new IdentifiedCommand<CompleteTimerCommand>(new CompleteTimerCommand(request.Id), request.Id));
            if (rs == null)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Timer>>();
                logger.LogError("Cannot complete timer. No command hander found.");
            }
            else if (!rs.Succeeded)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Timer>>();
                logger.LogError("Cannot complete timer. Id: {Id} {Message}", request.Id, rs.Message ?? "");
            }

            IsCompleted = true;
        }

        #region IDisposable Support


        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    try
                    {
                        if (_shutdown != null)
                        {
                            _shutdown.Dispose();
                        }
                        if (_backgroundTask != null)
                        {
                            _backgroundTask.Dispose();
                        }
                        if (_request != null)
                        {
                            _request = null;
                        }
                    }
                    catch { }
                }
                Interlocked.Decrement(ref globalCounter);
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~Timer()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
