# Contract: Public API — `Juice.EventBus.NATS`

All entry points hang off the existing `EventBusBuilder`, mirroring `AddRabbitMQ`. Types not listed here are `internal`.

## Registration

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class JetStreamServiceCollectionExtensions
{
    public static EventBusBuilder AddJetStream(this EventBusBuilder builder,
        Action<JetStreamEventBusBuilder> configure);
}

public sealed class JetStreamEventBusBuilder
{
    public MessagingBuilder Messaging { get; }
    public EventBusBuilder EventBus { get; }

    // Connections — keyed singleton IJetStreamConnection by name (FR-007, FR-019, FR-020)
    public JetStreamEventBusBuilder AddConnection(string name, Action<JetStreamConnectionOptions> configure);
    public JetStreamEventBusBuilder AddConnection(string name, IConfigurationSection configuration);

    // Producer — keyed singleton ITransportPublisher; throws on duplicate key (FR-001, FR-002)
    public JetStreamEventBusBuilder AddProducer(string key, string connectionName,
        Action<JetStreamProducerBuilder>? configure = default);

    // Consumer — hosted service; throws on duplicate (connection, stream, durable) (FR-008..FR-015)
    public JetStreamEventBusBuilder AddConsumer(string key, string stream, string durableName,
        string connectionName, Action<JetStreamConsumerBuilder>? configure = default);

    // Topology — verify (default) or create streams/consumers (FR-021)
    public JetStreamEventBusBuilder AddInfrastructureTopology(string connectionName,
        Action<JetStreamInfrastructureBuilder> configure);
}
```

## Producer builder

```csharp
public sealed class JetStreamProducerBuilder
{
    public JetStreamProducerBuilder DefaultDestination(string destination);
    public JetStreamProducerBuilder MaxRetryAttempts(ushort attempts);
    public JetStreamProducerBuilder PublishTimeout(TimeSpan timeout);
}
```

## Consumer builder

```csharp
public sealed class JetStreamConsumerBuilder : IConsumerBuilder
{
    public JetStreamConsumerBuilder Destination(string destination);          // subject prefix; required
    public JetStreamConsumerBuilder MaxConcurrency(int value);                // default 10
    public JetStreamConsumerBuilder AckWait(TimeSpan value);                  // default 30s
    public JetStreamConsumerBuilder MaxDeliver(int value);                    // default -1
    public JetStreamConsumerBuilder ParkingPrefix(string prefix);             // default "parking.{destination}"
    public JetStreamConsumerBuilder OnNotHandled(NotHandledBehavior behavior);// default Terminate
    public JetStreamConsumerBuilder Provisioning(JetStreamProvisioning mode); // default VerifyOnly

    public JetStreamConsumerBuilder Subscribe<TEvent, THandler>(string? route = default)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>;
}
```

`route` accepts RabbitMQ topic syntax (`*`, trailing `#`) — translated per [wire-format.md](./wire-format.md#filter-subjects).

## Topology builder

```csharp
public sealed class JetStreamInfrastructureBuilder
{
    public JetStreamInfrastructureBuilder Provisioning(JetStreamProvisioning mode);
    public JetStreamInfrastructureBuilder DeclareStream(string destination, Action<StreamDefinition>? configure = default);
    public JetStreamInfrastructureBuilder DeclareParkingStream(string destination, Action<StreamDefinition>? configure = default);
}

public enum JetStreamProvisioning { VerifyOnly, CreateIfMissing }
public enum NotHandledBehavior { Terminate, Park }
public sealed class StreamDefinition { /* see data-model.md §4 */ }
public class JetStreamConnectionOptions { /* see data-model.md §1 */ }
```

## Host / health

```csharp
public static class JetStreamHostExtensions
{
    public static Task InitJetStreamInfrastructureAsync(this IHost app);
    public static Task InitJetStreamInfrastructureAsync(this IServiceProvider sp);
}

public static class JetStreamHealthCheckExtensions
{
    public static IHealthChecksBuilder AddJetStreamHealthCheck(this IHealthChecksBuilder builder,
        string connectionName, string? name = null, HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null, TimeSpan? timeout = null);   // default name "nats_{connectionName}", tags ["nats","messaging"]
}
```

## Behavioral guarantees

| # | Guarantee | FR |
|---|---|---|
| 1 | `PublishAsync` completes only after a stream ack; duplicate ack counts as success | FR-003, FR-004 |
| 2 | `PublishAsync` throws `InvalidOperationException` naming subject + destination when no stream captures it | Edge |
| 3 | Resolved via `GetRequiredKeyedService<ITransportPublisher>(key)`; used by outbox delivery (`DeliveryHostedService<TContext>`) | FR-001, FR-002 |
| 4 | Consumer acks only after `Success`/`Duplicated`; `MessageContext` initialized before and cleared after dispatch | FR-010..FR-012 |
| 5 | Retry/parking driven by `ConsumeRetryPolicyOptions` (`AddConsumerRetryPolicies`) with `Source` = consumer destination | FR-016, FR-017 |
| 6 | Existing packages' public API unchanged | FR-026 |
