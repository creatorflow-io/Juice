# Contract: Transport Conformance Scenarios (SC-006)

Each row is implemented once per transport. RabbitMQ coverage lives in `core/test/Juice.EventBus.Tests`; JetStream coverage in `core/test/Juice.EventBus.NATS.Tests/Integration`. All rows are `IgnoreOnCIFact` + `[InitializeMessageContext]`.

| # | Scenario | Expected | Spec ref | JetStream test |
|---|---|---|---|---|
| 1 | Outbox event routed by policy to transport key | Message stored/received once; delivery row `Published` | US1-AS1 | `JetStreamOutboxEndToEndTest.OutboxEventShouldBePublishedToJetStreamAsync` |
| 2 | Broker down during delivery, then up | Delivery `Failed` → retried → `Published` | US1-AS2 | `JetStreamOutboxEndToEndTest.DeliveryShouldRecoverWhenStreamBecomesAvailableAsync`, `JetStreamPublishTest.PublishWhenServerUnreachableShouldThrowAsync` |
| 3 | Same delivery published twice | Single stored message (JetStream) / single handled (RabbitMQ via idempotency) | US1-AS3, US2-AS4 | `JetStreamPublishTest.PublishSameMessageIdTwiceShouldStoreOnceAsync`, `JetStreamConsumeTest.DuplicatePublishWithIdempotencyShouldHandleOnceAsync`, `JetStreamOutboxEndToEndTest.OutboxDeliveryShouldSurviveFaultsAsync` |
| 4 | Fan-out to RabbitMQ + JetStream | Both receive; failure of one doesn't affect the other | US1-AS4 | `JetStreamOutboxEndToEndTest.PolicyRoutingToTwoPublishersShouldDeliverIndependentlyAsync` (second publisher is a failing fake) |
| 5 | Standard headers round-trip | correlation, causation, source, tenant, message id/type/name equal at consumer | US1-AS5, FR-006 | `JetStreamPublishTest.PublishShouldStoreMessageWithHeadersAsync`, `JetStreamConsumeTest.MessageContextShouldPropagateCorrelationAsync` |
| 6 | Consume with tenant header | Handler sees tenant context | US2-AS2 | `JetStreamConsumeTest.HandlerShouldRunInTenantContextAsync` |
| 7 | MessageContext propagation | correlation = header, causation = incoming message id | US2-AS3 | `JetStreamConsumeTest.MessageContextShouldPropagateCorrelationAsync` |
| 8 | Wildcard route (`kernel.*`, `kernel.#`) | Matching dispatched, non-matching not | US2-AS5 | `JetStreamConsumeTest.WildcardRouteShouldMatchOnlyMatchingSubjectsAsync` (`kernel.*`); `#` translation in `JetStreamBuilderTest.FilterSubjectsShouldFollowRoutes` |
| 9 | Handler fails N times then succeeds | Succeeds on attempt N+1 with tier delays (± 20 %) | US3-AS1/2 | `JetStreamRetryParkingTest.HandlerFailingTwiceShouldSucceedOnThirdAttemptAsync` |
| 10 | Handler always fails, parking on | Parked after max attempts with failure headers | US3-AS3, SC-004 | `JetStreamRetryParkingTest.AlwaysFailingHandlerShouldBeParkedAsync` |
| 11 | Handler always fails, parking off | Terminated + error log | US3-AS4 | `JetStreamRetryParkingTest.ParkingDisabledShouldTerminateAsync` |
| 12 | One failing message + healthy traffic | Healthy messages unaffected | US3-AS5 | `JetStreamRetryParkingTest.FailingMessageShouldNotBlockOthersAsync` |
| 13 | Two instances, same durable / queue | Each message handled once overall | FR-013 | `JetStreamConsumeTest.TwoInstancesSameDurableShouldShareWorkAsync` |
| 14 | Backlog published while consumer stopped | All handled after restart (10k < 30 s) | SC-005 | `JetStreamOutboxEndToEndTest.BacklogOf10kShouldDrainWithin30sAsync`, `JetStreamConsumeTest.BacklogShouldBeProcessedAfterRestartAsync` |

Rows 1–12 already exist (fully or partly) for RabbitMQ; rows not yet covered there are **not** in scope for this feature — parity is measured on the rows RabbitMQ implements plus JetStream-specific rows 3, 10–14.
