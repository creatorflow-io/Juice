using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MediatR.Benchmarks;
[MemoryDiagnoser]
public class MediatorBench
{
    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddMediatR(builder =>
        {
            builder.RegisterServicesFromAssemblyContaining<MediatorBench>();
        });
        return services.BuildServiceProvider();
    }

    [Benchmark]
    public async Task SendAsync()
    {
        var provider = BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        await mediator.Send(new Request());
    }
}
internal class Request : IRequest
{
    public Guid MessageId => throw new NotImplementedException();

    public DateTimeOffset CreatedAt => throw new NotImplementedException();

    public string? TenantId => throw new NotImplementedException();
}
internal class RequestHandler : IRequestHandler<Request>
{
    public ValueTask Handle(Request request, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<MediatorBench>();
    }
}
