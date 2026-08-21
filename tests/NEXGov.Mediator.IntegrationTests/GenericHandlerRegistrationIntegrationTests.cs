using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-013 integration tests: RegisterGenericHandlers with a real DI
// container, and its composition with the advanced pipeline features from
// MED-009/010/011.
public class GenericHandlerRegistrationIntegrationTests
{
    // --- Mandatory acceptance (item 21): generic response request dispatch ---

    [Fact]
    public async Task GenericResponseRequest_NoManualRegistration_DispatchesToTheGeneratedHandler()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GenericDiQuery<GenericDiCustomer>(1));

        Assert.Equal(1, response.Id);
    }

    // --- Mandatory acceptance (item 22): generic void request dispatch ---

    [Fact]
    public async Task GenericVoidRequest_NoManualRegistration_DispatchesToTheGeneratedHandler()
    {
        GenericDiCommandHandler<GenericDiCustomer>.Handled.Clear();
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new GenericDiCommand<GenericDiCustomer>(2));

        Assert.Equal([2], GenericDiCommandHandler<GenericDiCustomer>.Handled);
    }

    // --- Item 23: multiple closing types, each actually dispatched ---

    [Fact]
    public async Task MultipleClosingTypes_EachDispatchesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var customer = await sender.Send(new GenericDiQuery<GenericDiCustomer>(1));
        var supplier = await sender.Send(new GenericDiQuery<GenericDiSupplier>(2));
        var partner = await sender.Send(new GenericDiQuery<GenericDiPartner>(3));

        Assert.Equal(1, customer.Id);
        Assert.Equal(2, supplier.Id);
        Assert.Equal(3, partner.Id);
    }

    // --- Item 24: disabled regression ---

    [Fact]
    public async Task RegisterGenericHandlers_Disabled_GenericRequestFailsViaMissingHandlerPath()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new GenericDiQuery<GenericDiCustomer>(1)));
    }

    // --- Item 30: scoped dependency inside a generated generic handler ---

    [Fact]
    public async Task GeneratedGenericHandler_ScopedDependency_SameWithinScope_DifferentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();

        static Guid ExtractId(string message) => Guid.Parse(message[(message.IndexOf(':') + 1)..]);

        string first;
        string second;
        using (var scope1 = provider.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            first = (await sender.Send(new GenericDiScopedQuery<GenericDiCustomer>("a"))).Message;
            second = (await sender.Send(new GenericDiScopedQuery<GenericDiCustomer>("b"))).Message;
        }

        string third;
        using (var scope2 = provider.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            third = (await sender.Send(new GenericDiScopedQuery<GenericDiCustomer>("c"))).Message;
        }

        Assert.Equal(ExtractId(first), ExtractId(second));
        Assert.NotEqual(ExtractId(first), ExtractId(third));
    }

    // --- Item 31: coexistence with AddOpenBehavior ---

    [Fact]
    public async Task GeneratedGenericHandler_ComposesWithAddOpenBehavior()
    {
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

        var response = await sender.Send(new GenericDiQuery<GenericDiCustomer>(9));

        Assert.Equal(9, response.Id);
        Assert.Equal(["Logging.Before", "Logging.After"], log);
    }

    // --- Item 32: coexistence with an open-generic pre-processor ---

    [Fact]
    public async Task GeneratedGenericHandler_ComposesWithAnOpenRequestPreProcessor()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.AddOpenRequestPreProcessor(typeof(GenericDiPreProcessor<>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GenericDiQuery<GenericDiCustomer>(5));

        Assert.Equal(5, response.Id);
        Assert.Equal(["GenericDiPre"], log);
    }

    // --- Item 33: coexistence with the exception pipeline ---

    [Fact]
    public async Task GeneratedGenericHandler_ExceptionsStillFlowThroughTheOrdinaryExceptionPipeline()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GenericDiThrowingQuery<GenericDiCustomer>(1));

        Assert.Equal(-1, response.Id);
    }
}
