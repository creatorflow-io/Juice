using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;

namespace MediatR.Benchmarks;
[MemoryDiagnoser]
public class MediatorBench
{
    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
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
}
internal class RequestHandler : IRequestHandler<Request>
{
    public Task Handle(Request request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<MediatorBench>();
    }
}
