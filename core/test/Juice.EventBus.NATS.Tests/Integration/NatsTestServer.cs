using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Juice.EventBus.NATS.Tests.Integration
{
    /// <summary>
    /// Raw NATS helpers so publish/consume tests can prepare streams without the US4 provisioning code.
    /// Requires <c>docker run -p 4222:4222 nats:2.10 -js</c>.
    /// </summary>
    internal static class NatsTestServer
    {
        public const string Url = "nats://localhost:4222";

        public record StoredMessage(string Subject, byte[] Data, IDictionary<string, object?> Headers);

        public static string UniqueDestination(string prefix)
            => prefix + "." + Guid.NewGuid().ToString("N")[..8];

        public static async Task<NatsConnection> ConnectAsync()
        {
            var connection = new NatsConnection(new NatsOpts { Url = Url });
            await connection.ConnectAsync();
            return connection;
        }

        public static async Task EnsureStreamAsync(string name, params string[] subjects)
        {
            await using var connection = await ConnectAsync();
            var js = new NatsJSContext(connection);
            await js.CreateOrUpdateStreamAsync(new StreamConfig(name, subjects)
            {
                DuplicateWindow = TimeSpan.FromMinutes(2)
            });
        }

        public static async Task DeleteStreamAsync(string name)
        {
            await using var connection = await ConnectAsync();
            var js = new NatsJSContext(connection);
            try
            {
                await js.DeleteStreamAsync(name);
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
            }
        }

        public static async Task<StreamInfo?> GetStreamAsync(string name)
        {
            await using var connection = await ConnectAsync();
            var js = new NatsJSContext(connection);
            try
            {
                var stream = await js.GetStreamAsync(name);
                return stream.Info;
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                return null;
            }
        }

        public static async Task<ConsumerInfo?> GetConsumerAsync(string stream, string durable)
        {
            await using var connection = await ConnectAsync();
            var js = new NatsJSContext(connection);
            try
            {
                var consumer = await js.GetConsumerAsync(stream, durable);
                return consumer.Info;
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                return null;
            }
        }

        public static async Task<List<StoredMessage>> ReadAllAsync(string stream)
        {
            await using var connection = await ConnectAsync();
            var js = new NatsJSContext(connection);
            var consumer = await js.CreateOrderedConsumerAsync(stream);
            var info = (await js.GetStreamAsync(stream)).Info;
            var result = new List<StoredMessage>();
            if (info.State.Messages == 0)
            {
                return result;
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: cts.Token))
            {
                result.Add(new StoredMessage(msg.Subject, msg.Data ?? Array.Empty<byte>(), JetStreamHeaders.Decode(msg.Headers)));
                if (result.Count >= info.State.Messages)
                {
                    break;
                }
            }
            return result;
        }

        public static async Task PublishRawAsync(string subject, byte[] data, IDictionary<string, object?> headers, string? msgId = default)
        {
            await using var connection = await ConnectAsync();
            var js = new NatsJSContext(connection);
            var ack = await js.PublishAsync(subject, data, headers: JetStreamHeaders.Encode(headers),
                opts: new NatsJSPubOpts { MsgId = msgId });
            ack.EnsureSuccess();
        }

        public static string? DockerContainer => TestSettings.DockerContainer;

        public static IEnumerable<string> DistinctHeader(this IEnumerable<StoredMessage> messages, string header)
            => messages.Select(m => m.Headers.TryGetValue(header, out var v) ? v?.ToString() ?? "" : "").Distinct();
    }
}
