using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MediatR.Internal;

public sealed class Mediator : IMediator
{
    // --- IMediator implementation ---
    private readonly IServiceProvider _provider;
    private readonly ConcurrentDictionary<Type, object> _cache = new();
    public Mediator(IServiceProvider provider)
    {
        _provider = provider;
    }

    #region Request
    public ValueTask Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var invoker = (Func<IRequest, CancellationToken, ValueTask>)_cache.GetOrAdd(
            request.GetType(),
            static (type, state) => state.BuildInvoker(type),
            this);

        return invoker(request, cancellationToken);
    }

    private Func<IRequest, CancellationToken, ValueTask> BuildInvoker(Type requestType)
    {
        // Resolve handler
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);
        var handlers = _provider.GetServices(handlerType).ToArray();
        if (handlers.Length == 0)
        {
            throw new InvalidOperationException($"No handler registered for {requestType.Name}");
        }
        if (handlers.Length > 1)
        {
            throw new InvalidOperationException($"Multiple handlers registered for {requestType.Name}");
        }
        var handler = handlers[0]!;

        // Resolve behaviors
        var behaviorType = typeof(IPipelineBehavior<>).MakeGenericType(requestType);
        var behaviors = (IEnumerable<object>)_provider.GetServices(behaviorType);

        var method = GetType()
            .GetMethod(nameof(InvokeRequestPipelineAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(requestType);

        // build delegate: (IRequest req, CancellationToken ct) => InvokeRequestPipelineAsync<TRequest>(handler, behaviors, (TRequest)req, ct)
        return (req, ct) => (ValueTask)method.Invoke(null, new object[] { handler, behaviors, req, ct })!;
    }

    private static async ValueTask InvokeRequestPipelineAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        IPipelineBehavior<TRequest>[] behaviors,
        TRequest request,
        CancellationToken ct)
    where TRequest : IRequest
    {
        // Build chain
        behaviors = [.. behaviors.DistinctBy(b => b.GetType())];
        Array.Sort(behaviors, (a, b) => a.Order.CompareTo(b.Order));
        ValueTask Next(int index)
        {
            if (index < behaviors.Length)
            {
                return behaviors[index].Handle(request, new RequestHandlerDelegate<TRequest>((r, c) => Next(index + 1)), ct);
            }
            return handler.Handle(request, ct);
        }

        await Next(0).ConfigureAwait(false);
    }
    #endregion

    #region Request/Response
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var invoker = (Func<IRequest<TResponse>, CancellationToken, ValueTask<TResponse>>)_cache.GetOrAdd(
            request.GetType(),
            static (type, state) => state.BuildInvoker<TResponse>(type),
            this);

        return invoker(request, cancellationToken);
    }

    private Func<IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> BuildInvoker<TResponse>(Type requestType)
    {
        // Resolve handler
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handlers = _provider.GetServices(handlerType).ToArray();
        if (handlers.Length == 0)
        {
            throw new InvalidOperationException($"No handler registered for {requestType.Name}");
        }
        if (handlers.Length > 1)
        {
            throw new InvalidOperationException($"Multiple handlers registered for {requestType.Name}");
        }
        var handler = handlers[0]!;

        // Resolve behaviors
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = (IEnumerable<object>)_provider.GetServices(behaviorType);

        var method = GetType()
        .GetMethod(nameof(InvokeRequestResponsePipelineAsync), BindingFlags.NonPublic | BindingFlags.Static)!
        .MakeGenericMethod(requestType, typeof(TResponse));

        // build delegate: (IRequest<TResponse> req, CancellationToken ct) => InvokeRequestResponsePipelineAsync<TRequest,TResponse>(handler, behaviors, (TRequest)req, ct)
        return (req, ct) => (ValueTask<TResponse>)method.Invoke(null, new object[] { handler, behaviors, req, ct })!;
    }

    private static async ValueTask<TResponse> InvokeRequestResponsePipelineAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        TRequest request,
        CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        // Build chain
        behaviors = [.. behaviors.DistinctBy(b => b.GetType())];
        Array.Sort(behaviors, (a, b) => a.Order.CompareTo(b.Order));
        ValueTask<TResponse> Next(int index)
        {
            if (index < behaviors.Length)
            {
                return behaviors[index].Handle(request, new RequestHandlerDelegate<TRequest, TResponse>((r, c) => Next(index + 1)), ct);
            }
            return handler.Handle(request, ct);
        }

        return await Next(0).ConfigureAwait(false);
    }
    #endregion

    #region Stream

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        var invoker = (Func<IStreamRequest<TResponse>, CancellationToken, IAsyncEnumerable<TResponse>>)_cache.GetOrAdd(
            request.GetType(),
            static (type, state) => state.BuildStreamInvoker<TResponse>(type),
            this);
        return invoker(request, cancellationToken);
    }

    private Func<IStreamRequest<TResponse>, CancellationToken, IAsyncEnumerable<TResponse>> BuildStreamInvoker<TResponse>(Type requestType)
    {
        // Resolve handler
        var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handlers = _provider.GetServices(handlerType).ToArray();
        if (handlers.Length == 0)
        {
            throw new InvalidOperationException($"No handler registered for {requestType.Name}");
        }
        if (handlers.Length > 1)
        {
            throw new InvalidOperationException($"Multiple handlers registered for {requestType.Name}");
        }
        var handler = handlers[0]!;
        // Resolve behaviors
        var behaviorType = typeof(IStreamPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = (IEnumerable<object>)_provider.GetServices(behaviorType);
        var method = GetType()
        .GetMethod(nameof(InvokeStreamRequestHandlerAsync), BindingFlags.NonPublic | BindingFlags.Static)!
        .MakeGenericMethod(requestType, typeof(TResponse));
        // build delegate: (IStreamRequest<TResponse> req, CancellationToken ct) => InvokeStreamRequestHandlerAsync<TRequest,TResponse>(handler, (TRequest)req, ct)
        return (req, ct) => (IAsyncEnumerable<TResponse>)method.Invoke(null, new object[] { handler, behaviors, req, ct })!;
    }

    private static IAsyncEnumerable<TResponse> InvokeStreamRequestHandlerAsync<TRequest, TResponse>(
        IStreamRequestHandler<TRequest, TResponse> handler,
        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors,
        TRequest request,
        CancellationToken ct)
        where TRequest : IStreamRequest<TResponse>
    {
        // Build chain
        behaviors = [.. behaviors.DistinctBy(b => b.GetType())];
        Array.Sort(behaviors, (a, b) => a.Order.CompareTo(b.Order));
        IAsyncEnumerable<TResponse> Next(int index)
        {
            if (index < behaviors.Length)
            {
                return behaviors[index].Handle(request, new StreamHandlerDelegate<TRequest, TResponse>((r, c) => Next(index + 1)), ct);
            }
            return handler.Handle(request, ct);
        }
        return Next(0);

    }

    #endregion

    #region Notification
    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        if (notification is null) { throw new ArgumentNullException(nameof(notification)); }

        var invoker = (Func<TNotification, CancellationToken, ValueTask>)_cache.GetOrAdd(
            notification.GetType(),
            static (type, state) => state.BuildNotificationInvoker<TNotification>(type),
            this);
        return invoker(notification, cancellationToken);
    }

    private Func<TNotification, CancellationToken, ValueTask> BuildNotificationInvoker<TNotification>(Type notificationType)
        where TNotification : INotification
    {
        // Resolve handlers
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var handlers = (IEnumerable<object>)_provider.GetServices(handlerType);
        // Resolve behaviors
        var behaviorType = typeof(INotificationPipelineBehavior<>).MakeGenericType(notificationType);
        var behaviors = (IEnumerable<object>)_provider.GetServices(behaviorType);

        var method = GetType()
            .GetMethod(nameof(InvokeNotificationPipelineAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(notificationType);
        // build delegate: (TNotification n, CancellationToken ct) => InvokeNotificationPipelineAsync<TNotification>(handlers, behaviors, n, ct)
        return (n, ct) => (ValueTask)method.Invoke(null, new object[] { handlers, behaviors, n, ct })!;
    }

    private static async ValueTask InvokeNotificationPipelineAsync<TNotification>(
    INotificationHandler<TNotification>[] handlers,
    INotificationPipelineBehavior<TNotification>[] behaviors,
    TNotification notification,
    CancellationToken ct)
    where TNotification : INotification
    {
        // Final chain: execute handlers one by one
        async ValueTask RunHandlers()
        {
            await Task.WhenAll(
                handlers.Select(h => h.Handle(notification, ct).AsTask())
            ).ConfigureAwait(false);
        }
        // Build chain
        behaviors = [.. behaviors.DistinctBy(b => b.GetType())];
        Array.Sort(behaviors, (a, b) => a.Order.CompareTo(b.Order));
        // Wrap with behaviors
        ValueTask Next(int index)
        {
            if (index < behaviors.Length)
            {
                return behaviors[index].Handle(notification, new NotificationHandlerDelegate<TNotification>((n, c) => Next(index + 1)), ct);
            }

            return RunHandlers();
        }

        await Next(0).ConfigureAwait(false);
    }
    #endregion
}
