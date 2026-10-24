namespace Juice.EventBus.NATS
{
    public class JetStreamConnectionOptions
    {
        /// <summary>
        /// Server URL. Comma-separated seed URLs are allowed for a cluster. Default is <c>nats://localhost:4222</c>.
        /// </summary>
        public string Url { get; set; } = "nats://localhost:4222";
        /// <summary>
        /// Client name shown in server monitoring. Default is <c>"{entry-assembly}-{connection name}"</c>.
        /// </summary>
        public string? Name { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Token { get; set; }
        /// <summary>
        /// NKey/JWT <c>.creds</c> file path.
        /// </summary>
        public string? CredsFile { get; set; }
        public string? NKeySeedFile { get; set; }
        public JetStreamTlsOptions Tls { get; set; } = new();
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ReconnectWaitMin { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ReconnectWaitMax { get; set; } = TimeSpan.FromSeconds(10);
        /// <summary>
        /// Maximum number of reconnect attempts. Default is -1 (unlimited).
        /// </summary>
        public int MaxReconnectRetry { get; set; } = -1;

        internal void Validate(string connectionName)
        {
            void Fail(string reason)
                => throw new InvalidOperationException($"JetStream connection '{connectionName}' is invalid: {reason}");

            if (string.IsNullOrWhiteSpace(Url))
            {
                Fail("Url is required.");
            }
            if (string.IsNullOrEmpty(UserName) != string.IsNullOrEmpty(Password))
            {
                Fail("UserName and Password must be set together.");
            }
            var authModes = new[] { !string.IsNullOrEmpty(UserName), !string.IsNullOrEmpty(Token),
                !string.IsNullOrEmpty(CredsFile), !string.IsNullOrEmpty(NKeySeedFile) }.Count(x => x);
            if (authModes > 1)
            {
                Fail("only one of UserName/Password, Token, CredsFile or NKeySeedFile can be set.");
            }
            if (string.IsNullOrEmpty(Tls?.CertFile) != string.IsNullOrEmpty(Tls?.KeyFile))
            {
                Fail("Tls.CertFile and Tls.KeyFile must be set together.");
            }
            if (ReconnectWaitMin > ReconnectWaitMax)
            {
                Fail("ReconnectWaitMin must not be greater than ReconnectWaitMax.");
            }
            if (ConnectTimeout <= TimeSpan.Zero)
            {
                Fail("ConnectTimeout must be greater than zero.");
            }
            if (MaxReconnectRetry < -1)
            {
                Fail("MaxReconnectRetry must be -1 (unlimited) or greater.");
            }
        }
    }

    public class JetStreamTlsOptions
    {
        public JetStreamTlsMode Mode { get; set; } = JetStreamTlsMode.Auto;
        public string? CaFile { get; set; }
        public string? CertFile { get; set; }
        public string? KeyFile { get; set; }
        /// <summary>
        /// Skip server certificate verification. For development only.
        /// </summary>
        public bool InsecureSkipVerify { get; set; }
    }

    public enum JetStreamTlsMode
    {
        Auto,
        Prefer,
        Require,
        Implicit,
        Disable
    }
}
