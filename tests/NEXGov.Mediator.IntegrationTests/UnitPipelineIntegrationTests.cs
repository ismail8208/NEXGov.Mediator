using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-014 integration tests: closed void-targeting pipeline components
// with a real DI container, plus regression coverage for MED-013 generic
// void handlers composing with an open behavior closing against Unit.
public class UnitPipelineIntegrationTests
{
    [Fact]
    public async Task ClosedVoidBehavior_RegisteredViaAddBehavior_ExecutesAroundTheHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.AddBehavior<DeleteWidgetBehavior>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteWidget(1));

        Assert.Equal(["Behavior.Before", "Handler", "Behavior.After"], log);
    }

    [Fact]
    public async Task ClosedVoidPostProcessor_RegisteredViaAddRequestPostProcessor_Executes()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.AddRequestPostProcessor<DeleteWidgetPostProcessor>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteWidget(1));

        Assert.Equal(["Handler", "PostProcessor"], log);
    }

    [Fact]
    public async Task ClosedVoidExceptionHandler_DiscoveredByScanning_HandlesTheException()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ThrowingDeleteWidget(1)); // completes without throwing

        Assert.Equal(["ExceptionHandler"], log);
    }

    // --- Item 25: MED-013 generic void handler regression, composed with an open behavior ---

    [Fact]
    public async Task GenericVoidHandler_ComposesWithOpenBehavior_ClosingAgainstUnit()
    {
        // Closes against GenericDiPartner rather than GenericDiCustomer:
        // GenericDiCommandHandler<TEntity>.Handled is a static field
        // per closed generic type, and GenericHandlerRegistrationIntegrationTests
        // dispatches/clears/asserts GenericDiCommandHandler<GenericDiCustomer>.Handled
        // — xUnit runs different test classes concurrently by default, so
        // sharing that same closed generic type here (even without reading
        // its static list) would race against that test's own Add/Clear/
        // enumerate calls on the identical static field.
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new GenericDiCommand<GenericDiPartner>(1));

        Assert.Equal(["Logging.Before", "Logging.After"], log);

        var resolved = provider.GetService(typeof(IPipelineBehavior<GenericDiCommand<GenericDiPartner>, Unit>));
        Assert.IsType<LoggingBehavior<GenericDiCommand<GenericDiPartner>, Unit>>(resolved);
    }
}
