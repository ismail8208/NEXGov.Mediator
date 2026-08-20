using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;
using NEXGov.Mediator.UnitTests.ExceptionOrderingScenario.Commands;
using NEXGov.Mediator.UnitTests.ExceptionOrderingScenario.Other;

namespace NEXGov.Mediator.UnitTests;

// MED-015 end-to-end tests: real RequestExceptionProcessorBehavior/
// RequestExceptionActionProcessorBehavior.Handle calls (the actual
// production dispatch path, exactly like the pre-existing MED-009 tests in
// RequestExceptionHandlerBehaviorTests.cs/RequestExceptionActionBehaviorTests.cs),
// proving handler-proximity ordering now drives execution order instead of
// raw DI registration order.
public class ExceptionHandlerOrderingTests
{
    private static RequestExceptionProcessorBehavior<OrderingPing, OrderingPong> CreateHandlerBehavior(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new RequestExceptionProcessorBehavior<OrderingPing, OrderingPong>(services.BuildServiceProvider());
    }

    private static RequestExceptionActionProcessorBehavior<OrderingPing, OrderingPong> CreateActionBehavior(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new RequestExceptionActionProcessorBehavior<OrderingPing, OrderingPong>(services.BuildServiceProvider());
    }

    private static RequestHandlerDelegate<OrderingPong> Throws(Exception exception) => _ => throw exception;

    // --- Item 13: proves DI order is overridden by proximity priority ---

    [Fact]
    public async Task NearHandler_RegisteredAfterFarHandler_StillExecutesFirst()
    {
        var log = new List<string>();
        var far = new FarExceptionHandler(log);
        var near = new NearExceptionHandler(log);

        // Deliberately reversed: Far registered before Near.
        var behavior = CreateHandlerBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>>(far);
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>>(near);
        });

        var response = await behavior.Handle(new OrderingPing("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(["Near"], log);
        Assert.Equal("handled-by-near", response.Message);
    }

    [Fact]
    public async Task FarHandler_StillRuns_WhenHigherPriorityNearHandlerDoesNotHandle()
    {
        // High-priority (near) handler runs first but does not mark
        // handled -> the walk continues to the next prioritized (far)
        // handler, which does.
        var log = new List<string>();
        var nonHandlingNear = new NonHandlingNearExceptionHandler(log);
        var far = new FarExceptionHandler(log);

        var behavior = CreateHandlerBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>>(far);
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>>(nonHandlingNear);
        });

        var response = await behavior.Handle(new OrderingPing("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(["NonHandlingNear", "Far"], log);
        Assert.Equal("handled-by-far", response.Message);
    }

    // --- Item 14: same proof for actions ---

    [Fact]
    public async Task Actions_RegisteredInReversedPriorityOrder_ExecuteInPriorityOrder()
    {
        var log = new List<string>();
        var far = new FarExceptionAction(log);
        var near = new NearExceptionAction(log);

        var behavior = CreateActionBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionAction<OrderingPing, CustomValidationException>>(far);
            s.AddSingleton<IRequestExceptionAction<OrderingPing, CustomValidationException>>(near);
        });

        await Assert.ThrowsAsync<CustomValidationException>(
            () => behavior.Handle(new OrderingPing("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        // All applicable actions still execute (established action
        // semantics unchanged) but now in priority order, not DI order.
        Assert.Equal(["Near", "Far"], log);
    }

    // --- Item 2: exception-type specificity remains the primary ordering dimension ---

    [Fact]
    public async Task ExactExceptionType_InFarNamespace_StillOutranksBaseExceptionType_InNearNamespace()
    {
        // A highly-proximate BASE-exception-type handler must never jump
        // ahead of a less-proximate EXACT-exception-type handler: exception
        // hierarchy walking (ExceptionTypeHierarchy, unchanged by MED-015)
        // tries the whole CustomValidationException group before ever
        // considering the Exception group, regardless of any handler's
        // namespace proximity.
        var log = new List<string>();
        var farExact = new FarExactExceptionTypeHandler(log);
        var nearBase = new NearBaseExceptionTypeHandler(log);

        var behavior = CreateHandlerBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>>(farExact);
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, Exception>>(nearBase);
        });

        var response = await behavior.Handle(new OrderingPing("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(["FarExactType"], log);
        Assert.Equal("handled-by-far-exact-type", response.Message);
    }

    // --- Item 7: duplicate handler/action types across exception-type levels ---

    [Fact]
    public async Task HandlerType_RegisteredAtTwoExceptionLevels_HasNoCrossLevelDedup_RunsAtBothLevelsWhenUnhandled()
    {
        // Verified against current source: unlike actions, exception
        // HANDLERS have no cross-level dedup. A handler type registered
        // for both the exact thrown type and a base type further up the
        // hierarchy runs at both levels if it never marks the exception
        // handled (the walk only stops early on state.Handled, not on
        // "already saw this concrete type").
        var log = new List<string>();
        var dual = new DualLevelExceptionHandler(log);

        var behavior = CreateHandlerBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>>(dual);
            s.AddSingleton<IRequestExceptionHandler<OrderingPing, OrderingPong, Exception>>(dual);
        });

        await Assert.ThrowsAsync<CustomValidationException>(
            () => behavior.Handle(new OrderingPing("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal(["DualLevel", "DualLevel"], log);
        Assert.Equal(2, dual.CallCount);
    }

    [Fact]
    public async Task ActionType_RegisteredAtTwoExceptionLevels_DedupesAcrossLevels_RunsOnlyOnce()
    {
        // Verified against current source: actions DO dedupe by concrete
        // type across exception-hierarchy levels — a duplicate action type
        // must execute no more times than current MediatR would for one
        // thrown exception (exactly once, at the most specific applicable
        // level).
        var log = new List<string>();
        var dual = new DualLevelExceptionAction(log);

        var behavior = CreateActionBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionAction<OrderingPing, CustomValidationException>>(dual);
            s.AddSingleton<IRequestExceptionAction<OrderingPing, Exception>>(dual);
        });

        await Assert.ThrowsAsync<CustomValidationException>(
            () => behavior.Handle(new OrderingPing("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal(["DualLevel"], log);
        Assert.Equal(1, dual.CallCount);
    }
}
