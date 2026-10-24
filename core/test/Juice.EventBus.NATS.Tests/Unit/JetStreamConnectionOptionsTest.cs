using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamConnectionOptionsTest
    {
        public static IEnumerable<object[]> InvalidOptions()
        {
            yield return new object[] { new JetStreamConnectionOptions { Url = "" }, "Url" };
            yield return new object[] { new JetStreamConnectionOptions { UserName = "u" }, "Password" };
            yield return new object[] { new JetStreamConnectionOptions { Password = "p" }, "Password" };
            yield return new object[] { new JetStreamConnectionOptions { UserName = "u", Password = "p", Token = "t" }, "only one" };
            yield return new object[] { new JetStreamConnectionOptions { Token = "t", CredsFile = "a.creds" }, "only one" };
            yield return new object[] { new JetStreamConnectionOptions { CredsFile = "a.creds", NKeySeedFile = "a.nk" }, "only one" };
            yield return new object[] { new JetStreamConnectionOptions { Tls = new() { CertFile = "c.pem" } }, "KeyFile" };
            yield return new object[] { new JetStreamConnectionOptions { Tls = new() { KeyFile = "k.pem" } }, "KeyFile" };
            yield return new object[] { new JetStreamConnectionOptions { ReconnectWaitMin = TimeSpan.FromSeconds(20) }, "ReconnectWaitMin" };
            yield return new object[] { new JetStreamConnectionOptions { ConnectTimeout = TimeSpan.Zero }, "ConnectTimeout" };
            yield return new object[] { new JetStreamConnectionOptions { MaxReconnectRetry = -2 }, "MaxReconnectRetry" };
        }

        [Theory]
        [MemberData(nameof(InvalidOptions))]
        public void ValidateShouldThrowNamingConnection(JetStreamConnectionOptions options, string expected)
        {
            var act = () => options.Validate("orders");
            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*'orders'*{expected}*");
        }

        [Fact]
        public void DefaultsShouldBeValid()
        {
            var act = () => new JetStreamConnectionOptions().Validate("nats");
            act.Should().NotThrow();
        }

        [Fact]
        public void OptionsShouldBindFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Nats:Url"] = "nats://nats-1:4222,nats://nats-2:4222",
                    ["Nats:Name"] = "orders-api",
                    ["Nats:CredsFile"] = "/run/secrets/orders.creds",
                    ["Nats:Tls:Mode"] = "Require",
                    ["Nats:Tls:CaFile"] = "/etc/ssl/nats-ca.pem",
                    ["Nats:ConnectTimeout"] = "00:00:03",
                    ["Nats:ReconnectWaitMin"] = "00:00:01",
                    ["Nats:ReconnectWaitMax"] = "00:00:20",
                    ["Nats:MaxReconnectRetry"] = "5"
                })
                .Build();

            var options = new JetStreamConnectionOptions();
            configuration.GetSection("Nats").Bind(options);

            options.Url.Should().Be("nats://nats-1:4222,nats://nats-2:4222");
            options.Name.Should().Be("orders-api");
            options.CredsFile.Should().Be("/run/secrets/orders.creds");
            options.Tls.Mode.Should().Be(JetStreamTlsMode.Require);
            options.Tls.CaFile.Should().Be("/etc/ssl/nats-ca.pem");
            options.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(3));
            options.ReconnectWaitMin.Should().Be(TimeSpan.FromSeconds(1));
            options.ReconnectWaitMax.Should().Be(TimeSpan.FromSeconds(20));
            options.MaxReconnectRetry.Should().Be(5);
        }

        [Fact]
        public void BuildNatsOptsShouldMapUserPassword()
        {
            var opts = DefaultJetStreamConnection.BuildNatsOpts("nats",
                new JetStreamConnectionOptions { Url = "nats://h:4222", UserName = "u", Password = "p" },
                new TestLogger());

            opts.Url.Should().Be("nats://h:4222");
            opts.AuthOpts.Username.Should().Be("u");
            opts.AuthOpts.Password.Should().Be("p");
            opts.AuthOpts.Token.Should().BeNull();
            opts.Name.Should().EndWith("-nats");
        }

        [Fact]
        public void BuildNatsOptsShouldMapTokenCredsAndNKey()
        {
            DefaultJetStreamConnection.BuildNatsOpts("n", new JetStreamConnectionOptions { Token = "t" }, new TestLogger())
                .AuthOpts.Token.Should().Be("t");
            DefaultJetStreamConnection.BuildNatsOpts("n", new JetStreamConnectionOptions { CredsFile = "a.creds" }, new TestLogger())
                .AuthOpts.CredsFile.Should().Be("a.creds");
            DefaultJetStreamConnection.BuildNatsOpts("n", new JetStreamConnectionOptions { NKeySeedFile = "a.nk" }, new TestLogger())
                .AuthOpts.NKeyFile.Should().Be("a.nk");
        }

        [Fact]
        public void BuildNatsOptsShouldMapTlsAndReconnect()
        {
            var opts = DefaultJetStreamConnection.BuildNatsOpts("n", new JetStreamConnectionOptions
            {
                Name = "client",
                Tls = new() { Mode = JetStreamTlsMode.Require, CaFile = "ca.pem", CertFile = "c.pem", KeyFile = "k.pem" },
                ConnectTimeout = TimeSpan.FromSeconds(4),
                ReconnectWaitMin = TimeSpan.FromSeconds(1),
                ReconnectWaitMax = TimeSpan.FromSeconds(9),
                MaxReconnectRetry = 7
            }, new TestLogger());

            opts.Name.Should().Be("client");
            opts.TlsOpts.Mode.Should().Be(TlsMode.Require);
            opts.TlsOpts.CaFile.Should().Be("ca.pem");
            opts.TlsOpts.CertFile.Should().Be("c.pem");
            opts.TlsOpts.KeyFile.Should().Be("k.pem");
            opts.TlsOpts.InsecureSkipVerify.Should().BeFalse();
            opts.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(4));
            opts.ReconnectWaitMin.Should().Be(TimeSpan.FromSeconds(1));
            opts.ReconnectWaitMax.Should().Be(TimeSpan.FromSeconds(9));
            opts.MaxReconnectRetry.Should().Be(7);
        }

        [Fact]
        public void BuildNatsOptsShouldWarnWhenInsecure()
        {
            var logger = new TestLogger();
            var opts = DefaultJetStreamConnection.BuildNatsOpts("n",
                new JetStreamConnectionOptions { Tls = new() { InsecureSkipVerify = true } }, logger);

            opts.TlsOpts.InsecureSkipVerify.Should().BeTrue();
            logger.Warnings.Should().ContainSingle(w => w.Contains("InsecureSkipVerify"));
        }

        [Fact]
        public void ConnectionShouldNotConnectUntilUsed()
        {
            var connection = new DefaultJetStreamConnection("n",
                new JetStreamConnectionOptions { Url = "nats://127.0.0.1:1" }, new TestLogger());

            connection.State.Should().Be(NatsConnectionState.Closed);
            connection.JetStream.Should().NotBeNull();
            connection.State.Should().Be(NatsConnectionState.Closed);
        }

        internal sealed class TestLogger : ILogger
        {
            public List<string> Warnings { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}
