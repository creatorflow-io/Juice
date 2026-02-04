using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ.Publishing
{
    internal delegate ValueTask AsyncEventHandler<in TEventArgs>(
        object sender,
        TEventArgs args);
    internal sealed class ChannelPool(int capacity, string destination,
        IRabbitMQPersistentConnection persistentConnection,
        ChannelManager manager,
        ILogger logger) : IAsyncDisposable
    {
        private readonly IRabbitMQPersistentConnection _persistentConnection = persistentConnection;

        // SemaphoreSlim limits total number of live channels.
        // Channel<T> is used only for pooling/reuse.
        // This prevents channel explosion under high concurrency.

        private readonly SemaphoreSlim _semaphore = new(capacity, capacity);
        private readonly Channel<IChannel> _channelPool =
            Channel.CreateBounded<IChannel>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        private readonly ILogger _logger = logger;


        public async ValueTask<IChannel> RentAsync(CancellationToken ct)
        {
            // Try to reuse existing channel
            await _semaphore.WaitAsync(ct);
            while (_channelPool.Reader.TryRead(out var channel))
            {
                if (channel.IsOpen)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Reusing existing RabbitMQ channel from pool.");
                    return channel;
                }
                // Dispose closed channel
                SafeDispose(channel);
            }

            // Create new channel
            var newChannel = await CreateProducerChannelAsync(ct);
            if (newChannel == null)
                throw new InvalidOperationException("Cannot create channel - connection unavailable");
            InitializeChannel(newChannel);
            return newChannel;

        }

        public async Task ReturnAsync(IChannel? channel)
        {
            try
            {
                // Add back to pool only if still open
                if (channel?.IsOpen == true)
                {
                    await _channelPool.Writer.WriteAsync(channel);
                }
                else
                {
                   SafeDispose(channel);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async ValueTask<IChannel?> CreateProducerChannelAsync(CancellationToken ct)
        {
            if (!_persistentConnection.IsConnected && !await _persistentConnection.TryConnectAsync(ct).ConfigureAwait(false))
            {
                return null;
            }
            _logger.LogInformation("Creating RabbitMQ producer channel. Connection: {Connection}", _persistentConnection.Name);
            return await _persistentConnection.CreateChannelAsync(ct);
        }

        private void InitializeChannel(IChannel channel)
        {
            // Any channel initialization logic can go here
            if (channel != null)
            {
                channel.CallbackExceptionAsync += (_, ea) =>
                {
                    _logger.LogWarning(ea.Exception, "RabbitMQ channel callback exception occurred. Disposing channel.");
                    _ = Task.Run(() => manager.RaiseChannelFailedAsync(
                        new ChannelFailureEventArgs(
                            exchange: destination,
                            replyCode: 0,
                            replyText: "CallbackException",
                            exception: ea.Exception))
                    );
                    try
                    {
                        channel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing RabbitMQ channel after callback exception");
                    }
                    return Task.CompletedTask;
                };

                channel.ChannelShutdownAsync += (sender, ea) =>
                {
                    _logger.LogWarning("RabbitMQ channel shutdown: ReplyCode={ReplyCode}, ReplyText={ReplyText}",
                        ea.ReplyCode, ea.ReplyText);

                    if (ea.Initiator == ShutdownInitiator.Peer)
                    {
                        _ = Task.Run(() =>
                           manager.RaiseChannelFailedAsync(
                            new ChannelFailureEventArgs(
                                exchange: destination,
                                replyCode: ea.ReplyCode,
                                replyText: ea.ReplyText,
                                exception: ea.Exception)));
                    }
                    try
                    {
                        channel.Dispose();
                    }
                    catch
                    {
                    }
                    return Task.CompletedTask;
                };

            }
        }

        private void SafeDispose(IChannel? channel)
        {
            try
            {
                channel?.Dispose();
            }
            catch
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            while (_channelPool.Reader.TryRead(out var channel))
            {
                try
                {
                    await channel.DisposeAsync();
                }
                catch
                {
                }
            }
        }

    }

    internal sealed class ChannelManager(int poolCapacity, IRabbitMQPersistentConnection persistentConnection
            , ILoggerFactory loggerFactory, string logCategory) : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ChannelPool> _pools = new();
        private readonly int _poolCapacity = poolCapacity;
        private readonly IRabbitMQPersistentConnection _persistentConnection = persistentConnection;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly string _logCategory = logCategory;
        private readonly ILogger _logger = loggerFactory.CreateLogger(logCategory);

        public event AsyncEventHandler<ChannelFailureEventArgs>? ChannelFailureAsync;

        public ValueTask<IChannel> RentAsync(string destination, CancellationToken ct)
        {
            var pool = _pools.GetOrAdd(destination,
                _ => new ChannelPool(_poolCapacity,
                    destination,
                    _persistentConnection,
                    this,
                    _loggerFactory.CreateLogger(_logCategory + $"[channel-pool][{destination}]")));
            return pool.RentAsync(ct);
        }

        public async Task ReturnAsync(string destination, IChannel? channel)
        {
            if (_pools.TryGetValue(destination, out var pool))
            {
                await pool.ReturnAsync(channel);
            }
            else
            {
                // No pool found, dispose channel
                try
                {
                    if (channel?.IsOpen ?? false)
                    {
                        await channel.CloseAsync();
                    }
                    channel?.Dispose();
                }
                catch { }
            }
        }

        public async ValueTask RaiseChannelFailedAsync(ChannelFailureEventArgs args)
        {
            var handlers = ChannelFailureAsync;
            if (handlers is null)
                return;

            var invocationList = handlers.GetInvocationList();

            foreach (var handler in invocationList)
            {
                try
                {
                    var asyncHandler = (AsyncEventHandler<ChannelFailureEventArgs>)handler;
                    await asyncHandler(this, args);
                }
                catch (Exception ex)
                {
                    // NEVER let async event crash infra
                    _logger.LogError(
                        ex,
                        "Error handling ChannelFailedAsync for exchange {Exchange}",
                        args.Exchange);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var pool in _pools.Values)
            {
                await pool.DisposeAsync();
            }
            _pools.Clear();
        }
    }
}
