# Quickstart: NATS JetStream with Juice

Goal: publish an outbox event to JetStream and handle it in another service in under 15 minutes (SC-007).

## 1. Run a local JetStream server

```bash
docker run -d --name nats -p 4222:4222 -p 8222:8222 nats:2.10 -js
```

## 2. Reference the package

```xml
<PackageReference Include="Juice.EventBus.NATS" Version="10.5.*" />
```

## 3. Configure (appsettings.json)

```json
{
  "Juice": { "EventBus": { "Connections": { "Nats": { "Url": "nats://localhost:4222" } } } },
  "PublishingPolicies": {
    "Default": { "Publishers": [ { "Key": "nats", "Destination": "content" } ] }
  },
  "DeliveryPolicies": {
    "DefaultPolicy": { "Interval": "00:00:01", "InitialRetryDelay": "00:00:05", "RetryDelayMultiplier": 2.0, "MaxRetryAttempts": 5 }
  },
  "ConsumeRetryPolicies": {
    "Default": { "RetryTiers": [ "10s", "1m", "5m" ], "IsParkingEnabled": true }
  }
}
```

## 4. Publisher service

```csharp
services.AddMessaging()
    .AddPublishingPolicies(config.GetSection("PublishingPolicies"))
    .AddOutbox()                                   // ContentDbContext implements IOutboxContext
    .AddDelivery(delivery =>
    {
        delivery.AddDeliveryProcessor<ContentDbContext>("nats");
        delivery.AddDeliveryPolicies(config.GetSection("DeliveryPolicies"));
    });

services.AddEventBus()
    .AddJetStream(js =>
    {
        js.AddConnection("nats", config.GetSection("Juice:EventBus:Connections:Nats"))
          .AddProducer("nats", "nats")
          .AddInfrastructureTopology("nats", t => t
              .Provisioning(JetStreamProvisioning.CreateIfMissing)   // dev only; default is VerifyOnly
              .DeclareStream("content")
              .DeclareParkingStream("content"));
    });

services.AddHealthChecks().AddJetStreamHealthCheck("nats");

var app = builder.Build();
await app.InitJetStreamInfrastructureAsync();
```

Domain code is unchanged: commands that raise `ContentPublishedIntegrationEvent` through the outbox now land on subject `content.ContentPublishedIntegrationEvent` in stream `CONTENT`.

## 5. Consumer service

```csharp
services.AddEventBus()
    .AddConsumerRetryPolicies(config.GetSection("ConsumeRetryPolicies"))
    .AddJetStream(js =>
    {
        js.AddConnection("nats", config.GetSection("Juice:EventBus:Connections:Nats"))
          .AddConsumer(key: "content-worker", stream: "CONTENT", durableName: "content-worker",
                       connectionName: "nats", c => c
              .Destination("content")
              .MaxConcurrency(16)
              .Provisioning(JetStreamProvisioning.CreateIfMissing)
              .Subscribe<ContentPublishedIntegrationEvent, ContentPublishedHandler>());
    });
```

Scale out by running more instances with the same `durableName` — each message is handled by one instance. A different service uses its own `durableName` to get its own copy.

## 6. Verify

```bash
nats stream ls                         # CONTENT, CONTENT_PARKING
nats stream view CONTENT               # published messages + x-* headers
nats consumer info CONTENT content-worker
nats stream view CONTENT_PARKING       # failed messages with x-death-reason
```

## 7. Moving from RabbitMQ

1. Add the JetStream connection + producer alongside RabbitMQ.
2. Add a policy rule that publishes to **both** keys (`rabbitmq` and `nats`) for the domain being migrated.
3. Move consumers to `AddJetStream(...).AddConsumer(...)` — same `Subscribe<,>` calls and `ConsumeRetryPolicies`; set `Source` to the JetStream destination.
4. Remove the `rabbitmq` publisher from the rule once all consumers are migrated. Idempotency prevents double handling during overlap when both consumers live in the same service.

## Running the tests

One script starts everything the integration tests need (SQL Server, PostgreSQL, Redis, RabbitMQ, NATS with
JetStream, and a password-protected NATS server for the auth test) and writes the settings into each test
project's user secrets:

```powershell
./test/infra/setup-test-infra.ps1          # docker compose up + user secrets
dotnet test core/test/Juice.EventBus.NATS.Tests -p:TestTfmsInParallel=false
./test/infra/setup-test-infra.ps1 -Down    # stop (add -RemoveVolumes to wipe data)
```

```bash
dotnet test core/test/Juice.EventBus.NATS.Tests --filter "FullyQualifiedName~Unit"   # CI-safe, no server
```

Settings read by the NATS tests (user secrets, or the environment variable in brackets):

| Setting | Used for |
|---|---|
| `ConnectionStrings:SqlServerConnection` (`NATS_TESTS_SQLSERVER`) | Outbox end-to-end tests; a throw-away database per test. Falls back to `(localdb)\MSSQLLocalDB` |
| `Nats:DockerContainer` (`NATS_DOCKER_CONTAINER`) | Server-restart steps (`docker restart <name>`) in the reconnect and fault-injection tests |
| `Nats:Auth:Url/UserName/Password` (`NATS_AUTH_URL/USER/PASSWORD`) | The user/password authentication test |

Without Docker, `nats-server -js` (a single binary from the nats-server GitHub releases) works for everything except
the restart steps.

> **Docker Desktop (Windows/macOS):** a single NATS connection that both publishes and consumes stalls about 40 ms
> per publish in Docker Desktop's port-forwarding proxy (setting TCP `NoDelay` on the client does not help). For
> local development against a containerized server, give consumers their own connection
> (`AddConnection("nats-consumer", ...)` and pass it to `AddConsumer`). Native or Linux-hosted servers are not affected.
