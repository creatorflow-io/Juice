using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Juice.MultiTenant.Grpc;
using Juice.XUnit;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MultiTenant.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.MultiTenant.Tests")]
    public class GrpcTest
    {
        ITestOutputHelper _output;
        public GrpcTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        [IgnoreOnCIFact(DisplayName = "Find tenant with gRPC")]
        public async Task GRPCFindTenantAsync()
        {
            var timer = new Stopwatch();
            timer.Start();

            var channel =
                //CreateChannel();
                GrpcChannel.ForAddress(new Uri("https://localhost:7045"));

            var client = new TenantStore.TenantStoreClient(channel);

            _output.WriteLine("Init client take {0} milliseconds",
                timer.ElapsedMilliseconds);

            for (var i = 0; i < 10; i++)
            {
                timer.Reset();
                timer.Start();
                var reply = await client.TryGetByIdentifierAsync(
                new TenantIdenfier { Identifier = "acme" });

                _output.WriteLine("Request take {0} milliseconds",
                    timer.ElapsedMilliseconds);
                timer.Stop();
            }

            //Assert.NotNull(reply);
            //Assert.Equal("acme", reply.Identifier);
            //_output.WriteLine(reply.Name);
        }

        [IgnoreOnCIFact(DisplayName = "Find tenant with HttpClient")]
        public async Task HttpClientFindTenantAsync()
        {
            var timer = new Stopwatch();
            timer.Start();
            var client = new HttpClient();
            _output.WriteLine("Init client take {0} milliseconds",
                timer.ElapsedMilliseconds);


            for (var i = 0; i < 10; i++)
            {
                timer.Reset();
                timer.Start();
                var reply = await client.GetStringAsync(new Uri("https://localhost:7045/tenant"));
                _output.WriteLine("Request take {0} milliseconds",
                    timer.ElapsedMilliseconds);
                timer.Stop();
            }

            //Assert.NotNull(reply);
            //_output.WriteLine(reply);
        }

        public static readonly string SocketPath = Path.Combine(Path.GetTempPath(), "socket.tmp");

        public static GrpcChannel CreateChannel()
        {
            var udsEndPoint = new UnixDomainSocketEndPoint(SocketPath);
            var connectionFactory = new UnixDomainSocketConnectionFactory(udsEndPoint);
            var socketsHttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = connectionFactory.ConnectAsync
            };

            return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = socketsHttpHandler
            });
        }
    }
}
