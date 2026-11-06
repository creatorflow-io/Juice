# JUICE
`Juice is a simple and lightweigh code base that help developers to build microservices system.`

## Structure

### Core projects
Define the abstractions and default implementation for the API, event bus, audit...
that you need to build a micro, auditable and multi-tenants service.

#### Juice.*

Define base entity, common, utils, helper, extensions... class

#### Juice.EventBus.*

Implement event bus with related feature.

##### Transports → NATS JetStream

`Juice.EventBus.NATS` publishes outbox events to NATS JetStream and consumes them with durable pull consumers.
The outbox, publishing policies and integration event handlers are unchanged; only the transport registration differs.

```xml
<PackageReference Include="Juice.EventBus.NATS" Version="10.5.*" />
```

```csharp
// publisher
services.AddEventBus().AddJetStream(js => js
    .AddConnection("nats", config.GetSection("Juice:EventBus:Connections:Nats"))
    .AddProducer("nats", "nats")                       // publisher key used by PublishingPolicies
    .AddInfrastructureTopology("nats", t => t
        .DeclareStream("content")                      // stream CONTENT, subjects content.>
        .DeclareParkingStream("content")));            // stream CONTENT_PARKING, subjects parking.content.>
await app.InitJetStreamInfrastructureAsync();          // verifies streams (default) or creates them

// consumer
services.AddEventBus()
    .AddConsumerRetryPolicies(config.GetSection("ConsumeRetryPolicies"))
    .AddJetStream(js => js
        .AddConnection("nats", config.GetSection("Juice:EventBus:Connections:Nats"))
        .AddConsumer("content-worker", stream: "CONTENT", durableName: "content-worker", connectionName: "nats", c => c
            .Destination("content")
            .Subscribe<ContentPublishedIntegrationEvent, ContentPublishedHandler>()));
```

See the [quickstart](specs/012-nats-jetstream/quickstart.md) (including moving from RabbitMQ) and the
[configuration schema](specs/012-nats-jetstream/contracts/configuration-schema.md).

#### Juice.Extensions.*

Implement logging, configuration, options extenstion.

#### Juice.MediatR

Implement MediatR pattern with request, request handler, pipeline behavior.

``` ini
Juice.MediatR
MediatorBench.SendAsync: DefaultJob
Runtime = .NET 8.0.19 (8.0.19, 8.0.1925.36514), X64 RyuJIT x86-64-v3; GC = Concurrent Workstation
Mean = 3.513 us, StdErr = 0.018 us (0.51%), N = 24, StdDev = 0.087 us
Min = 3.369 us, Q1 = 3.454 us, Median = 3.521 us, Q3 = 3.578 us, Max = 3.655 us
IQR = 0.124 us, LowerFence = 3.269 us, UpperFence = 3.764 us
ConfidenceInterval = [3.446 us; 3.580 us] (CI 99.9%), Margin = 0.067 us (1.92% of Mean)
Skewness = -0.11, Kurtosis = 1.89, MValue = 2
-------------------- Histogram --------------------
[3.342 us ; 3.425 us) | @@@@
[3.425 us ; 3.514 us) | @@@@@@
[3.514 us ; 3.594 us) | @@@@@@@@@@
[3.594 us ; 3.674 us) | @@@@
---------------------------------------------------

// * Summary *

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26100.6584/24H2/2024Update/HudsonValley)
12th Gen Intel Core i9-12900 2.40GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 9.0.304
  [Host]     : .NET 8.0.19 (8.0.19, 8.0.1925.36514), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.19 (8.0.19, 8.0.1925.36514), X64 RyuJIT x86-64-v3


| Method    | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------- |---------:|----------:|----------:|-------:|-------:|----------:|
| SendAsync | 3.513 us | 0.0673 us | 0.0875 us | 0.7782 | 0.1831 |  11.96 KB |
```

#### Juice.Messaging.Outbox

Saves integration events in the same transaction as the data, then delivers them to the broker.

Delivery before and after 10.11.0, sending to RabbitMQ with a batch of 100 unless noted:

| | 10.10.1 | 10.11.0 |
|---|---|---|
| One instance, no other traffic | 133 messages/s | about 1,400-1,500 messages/s |
| Default settings (10 every 5 s) | about 2 messages/s, calculated | 719 messages/s |
| Delivery at 64 orders/s | 26 messages/s, backlog growing | 63.9 messages/s, keeps up |
| Commit to broker at 64 orders/s, p95 | kept growing | 243 ms |
| Client request at 64 orders/s, p95 | 57 ms | 29 ms |
| Disk flush at 64 orders/s | 11.9 ms | 2.1 ms |
| Claims lost between two instances | about half, at 64 orders/s | none, with no other traffic |

Measured on one development machine, with the services, SQL Server and RabbitMQ sharing its disk, so these compare the two
versions and are not production figures. Each order runs through three services in separate processes.

At 128 orders/s, 10.11.0 delivers 127.8 messages/s with a p95 of 400 ms from commit to broker. The consumer of those messages
falls behind at that rate, so the limit there is no longer delivery. Two instances with no other traffic reach 2,516 messages/s,
1.59× one instance on this shared machine, which isn't a scaling figure.

Messages of different transactions may now overtake each other: see the [changelog](CHANGELOG.md) for the order rules.

#### Juice.MultiTenant.*

Integrated with [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant)

### Services
Define the base services like background service, storage... to build a file management system.

## Road map

Version 6.0

- [x] Entity framework
    - [x] Full auditable with data event handler
    - [x] Dynamic expandable with JSON property
    - [x] Id string generator
    - [x] Dynamic schema migration
    - [x] Unit of work pattern
- [x] Microservices pattern
    - [x] RabbitMQ event bus
    - [x] NATS JetStream event bus (10.5)
    - [x] IntegrationEventLog with EF store
    - [x] MediatR Identified command
- [x] Extensions
    - [x] Options mutable
- [x] MultiTenant as microservice
    - [x] [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant) integration
    - [x] Tenant gRPC store
    - [x] Tenant options
    - [x] Tenant JSON configuration provider
    - [x] Tenant EF configuration provider
    - [x] Tenant gRPC configuration provider
    - [x] EF DbContext base supports multi-tenant, audit, dynamic entity, unit of work
- [x] Background service
    - [x] File watcher (watch folder) service
    - [x] Scheduled service
    - [x] Service managable via API
    - [x] Custom file logging provider: write to custom folder by service and custom file by job.
- [x] File storage
    - [x] Transfer large file size by multiple request
    - [x] Resumable with asset store (currently InMemory store)
    - [x] Write file to FTP, UNC path
    - [x] Extended file's metadata
    - [ ] EF store 
- [x] Workflow
    - [x] [Bpmn](https://bpmn.io/toolkit/bpmn-js/) compatible
    - [x] Yaml support
    - [x] Sub-workflow
    - [ ] Workload/ lightweigh workflow engine
    - [x] gRPC/ Web API
- [ ] App templates
    - [ ] MVC target
    - [ ] API target
    - [ ] SPA target
    - [ ] Microservice target

## License

[MIT](LICENSE)
