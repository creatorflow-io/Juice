using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;

namespace Juice.EventBus.NATS.Infrastructure
{
    /// <summary>
    /// Verifies (default) or creates the declared streams. Existing streams are never updated.
    /// </summary>
    public sealed class JetStreamInfrastructureInitializer
    {
        private readonly IJetStreamConnection _connection;
        private readonly IReadOnlyList<StreamDefinition> _streams;
        private readonly JetStreamProvisioning _provisioning;
        private readonly ILogger<JetStreamInfrastructureInitializer> _logger;

        internal JetStreamInfrastructureInitializer(
            IJetStreamConnection connection,
            IReadOnlyList<StreamDefinition> streams,
            JetStreamProvisioning provisioning,
            ILogger<JetStreamInfrastructureInitializer> logger)
        {
            _connection = connection;
            _streams = streams;
            _provisioning = provisioning;
            _logger = logger;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await _connection.EnsureConnectedAsync(ct).ConfigureAwait(false);
            var js = _connection.JetStream;
            var missing = new List<string>();

            foreach (var stream in _streams)
            {
                try
                {
                    await js.GetStreamAsync(stream.Name, cancellationToken: ct);
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Stream {Stream} exists; left unchanged.", stream.Name);
                    }
                }
                catch (NatsJSApiException ex) when (ex.Error.Code == 404)
                {
                    if (_provisioning == JetStreamProvisioning.CreateIfMissing)
                    {
                        await js.CreateStreamAsync(stream.ToStreamConfig(), ct);
                        _logger.LogInformation("Created stream {Stream} for subjects {Subjects}", stream.Name, string.Join(", ", stream.Subjects));
                    }
                    else
                    {
                        missing.Add(stream.Name);
                    }
                }
            }

            if (missing.Count > 0)
            {
                _logger.LogError("Missing JetStream streams (Provisioning=VerifyOnly): {Streams}", string.Join(", ", missing));
                throw new InvalidOperationException(
                    $"Missing JetStream streams (Provisioning=VerifyOnly): {string.Join(", ", missing)}. Create them or use JetStreamProvisioning.CreateIfMissing.");
            }
        }
    }
}
