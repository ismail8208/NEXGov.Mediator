using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator;
using NEXGov.Mediator.Sample;

var services = new ServiceCollection();

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

var provider = services.BuildServiceProvider();

var mediator = provider.GetRequiredService<IMediator>();

var response = await mediator.Send(new Greet("world"));
Console.WriteLine(response.Message);

await mediator.Publish(new UserGreeted("world"));
