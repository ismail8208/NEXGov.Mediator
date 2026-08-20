using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.IntegrationTests.Ordering;
using NEXGov.Mediator.IntegrationTests.Ordering.Feature;
using NEXGov.Mediator.IntegrationTests.Ordering.Feature.Commands;
using NEXGov.Mediator.IntegrationTests.Ordering.Other;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// MED-015 mandatory integration tests: real AddMediatR + Send, proving
// assembly proximity, namespace proximity, and derived-handler
// prioritization end to end — not by calling HandlerPriorityOrderer
// directly.
//
// Manual registrations below use the (serviceType, implementationType)
// Type-based overload, not the instance overload: ServiceRegistrar's
// exception-behavior auto-wiring check (unchanged by MED-015) inspects
// each ServiceDescriptor's ImplementationType, which is null for an
// instance-based registration — so an instance-registered handler/action
// would never be detected and RequestExceptionProcessorBehavior<,>/
// RequestExceptionActionProcessorBehavior<,> would never get wired into
// the pipeline. DI constructs each instance via the already-registered
// shared `log` singleton.
public class ExceptionOrderingIntegrationTests
{
    // --- Item 15: assembly proximity, using two real assemblies ---

    [Fact]
    public async Task SameAssemblyExceptionHandler_Wins_WhenBothAssembliesHaveAHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        // Deliberately reversed: foreign (Sample) assembly registered
        // before the same-assembly (IntegrationTests) handler.
        services.AddSingleton(
            typeof(IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>),
            typeof(NEXGov.Mediator.Sample.OtherAssemblyExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>));
        services.AddSingleton(
            typeof(IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>),
            typeof(ExactNamespaceOrderExceptionHandler));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(typeof(DiTestMarker).Assembly, typeof(NEXGov.Mediator.Sample.Greet).Assembly);
            cfg.TypeEvaluator = type => type == typeof(ThrowingCreateOrderHandler);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new CreateOrder("widget"));

        Assert.Equal(["Exact"], log);
        Assert.Equal("handled-by-exact", response.Message);
    }

    // --- Item 16: namespace proximity, mandatory consumer-style scenario ---

    [Fact]
    public async Task NamespaceProximity_Handlers_ExecuteInPriorityOrder_RegardlessOfRegistrationOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        // Deliberately registered in REVERSE priority order: unrelated,
        // grandparent, parent, exact.
        services.AddSingleton(typeof(IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>), typeof(UnrelatedNamespaceOrderExceptionHandler));
        services.AddSingleton(typeof(IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>), typeof(GrandparentNamespaceOrderExceptionHandler));
        services.AddSingleton(typeof(IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>), typeof(ParentNamespaceOrderExceptionHandler));
        services.AddSingleton(typeof(IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>), typeof(ExactNamespaceOrderExceptionHandler));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.TypeEvaluator = type => type == typeof(ThrowingCreateOrderHandler);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new CreateOrder("widget"));

        // Stops at the first (highest-priority) handler that handles it.
        Assert.Equal("handled-by-exact", response.Message);
        Assert.Equal(["Exact"], log);
    }

    [Fact]
    public async Task NamespaceProximity_Actions_ExecuteInPriorityOrder_RegardlessOfRegistrationOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        // Deliberately registered in REVERSE priority order: unrelated,
        // parent, exact. No handler is registered, so the exception
        // propagates and every action still runs (established semantics),
        // now in priority order.
        services.AddSingleton(typeof(IRequestExceptionAction<CreateOrder, InvalidOperationException>), typeof(UnrelatedNamespaceOrderExceptionAction));
        services.AddSingleton(typeof(IRequestExceptionAction<CreateOrder, InvalidOperationException>), typeof(ParentNamespaceOrderExceptionAction));
        services.AddSingleton(typeof(IRequestExceptionAction<CreateOrder, InvalidOperationException>), typeof(ExactNamespaceOrderExceptionAction));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.TypeEvaluator = type => type == typeof(ThrowingCreateOrderHandler);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new CreateOrder("widget")));

        Assert.Equal(["Exact", "Parent", "Unrelated"], log);
    }

    // --- Item 17: derived handler discovered by MED-012 scanning, prioritized correctly ---

    [Fact]
    public async Task DerivedHandler_DiscoveredByInheritedScanning_ExecutesCorrectly()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.TypeEvaluator = type => type == typeof(ThrowingCreateOrderHandler) || type == typeof(DerivedOrderExceptionHandler);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new CreateOrder("widget"));

        Assert.Equal("handled-by-derived", response.Message);
    }

    // --- Item 18: Unit-based void exception handler ordering regression ---

    [Fact]
    public async Task VoidExceptionHandlers_DifferentProximity_HigherPriorityOneHandles()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        // Reversed: far registered before near.
        services.AddSingleton(typeof(IRequestExceptionHandler<ThrowingDeleteWidget, Unit, InvalidOperationException>), typeof(FarVoidExceptionHandler));
        services.AddSingleton(typeof(IRequestExceptionHandler<ThrowingDeleteWidget, Unit, InvalidOperationException>), typeof(DeleteWidgetExceptionHandler));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.TypeEvaluator = type => type == typeof(ThrowingDeleteWidgetHandler);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ThrowingDeleteWidget(1)); // completes as a plain Task, no Unit observable

        Assert.Equal(["ExceptionHandler"], log); // DeleteWidgetExceptionHandler (near, same namespace as the request) wins
    }
}
