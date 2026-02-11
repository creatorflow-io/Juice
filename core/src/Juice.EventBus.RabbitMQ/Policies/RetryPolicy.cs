
namespace Juice.EventBus.RabbitMQ.Policies
{

    /// ======================= DOMAIN: CONTENT =======================
    ///
    ///                      ┌──────────────────────────────┐
    ///                      │     content.main.exchange    │  (direct)
    ///                      └─────────────┬────────────────┘
    ///                                    │ routingKey = EventName
    ///                                    v
    ///                      ┌──────────────────────────────┐
    ///                      │       content.main.queue     │
    ///                      └─────────────┬────────────────┘
    ///                                    │
    ///                                    │ Retryable failure
    ///                                    v
    /// ======================= RETRY ================================
    ///
    ///                      ┌──────────────────────────────┐
    ///                      │     content.retry.exchange   │  (direct / topic)
    ///                      └─────────────┬────────────────┘
    ///                                    │
    ///           ┌────────────────────────┼────────────────────────┐
    ///           │                        │                        │
    ///       retry.10s                retry.1m                 retry.5m
    ///           │                        │                        │
    ///           v                        v                        v
    /// ┌──────────────────-┐   ┌──────────────────┐   ┌──────────────────┐
    /// │ content.retry.10s │   │ content.retry.1m │   │ content.retry.5m │
    /// │ TTL = 10s         │   │ TTL = 1m         │   │ TTL = 5m         │
    /// │ DLX = content     │   │ DLX = content    │   │ DLX = content    │
    /// └──────────────────-┘   └──────────────────┘   └──────────────────┘
    ///                                    │
    ///                                    │ TTL expired
    ///                                    v
    ///                      ┌──────────────────────────────┐
    ///                      │     content.main.exchange    │
    ///                      └──────────────────────────────┘
    ///
    /// ======================= PARKING ===============================
    ///
    ///  retry-count >= max
    ///         │
    ///         v
    /// ┌──────────────────────────────┐
    /// │  content.parking.exchange    │
    /// └─────────────┬────────────────┘
    ///               v
    /// ┌──────────────────────────────┐
    /// │    content.parking.queue     │
    /// │   (no consumer, manual ops)  │
    /// └──────────────────────────────┘
    /// 
    internal record RetryPolicy
    {
        public string Exchange { get; init; } = default!;
        public string RoutingKey { get; init; } = default!;
        public bool IsMaxRetryReached { get; init; }
        public bool IsParkingEnabled { get; init; }
    }
}
