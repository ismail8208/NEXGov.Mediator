using NEXGov.Mediator.Pipeline;

// MED-015 fixtures for end-to-end exception handler/action priority tests.
// The request lives in .Commands; a "near" handler/action lives in the
// exact same namespace, a "far" one lives in an unrelated sibling
// namespace — giving HandlerPriorityOrderer genuinely different proximity
// to reorder by by, unlike the "same-priority tie" fixtures used elsewhere.

namespace NEXGov.Mediator.UnitTests.ExceptionOrderingScenario.Commands
{
    internal sealed record OrderingPing(string Message) : IRequest<OrderingPong>;

    internal sealed record OrderingPong(string Message);

    internal sealed class NearExceptionHandler : IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>
    {
        private readonly List<string> _log;

        public NearExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(OrderingPing request, CustomValidationException exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            _log.Add("Near");
            state.SetHandled(new OrderingPong("handled-by-near"));
            return Task.CompletedTask;
        }
    }

    // Higher priority than FarExceptionHandler but deliberately never
    // marks the exception handled, so the walk must continue to the next
    // prioritized handler.
    internal sealed class NonHandlingNearExceptionHandler : IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>
    {
        private readonly List<string> _log;

        public NonHandlingNearExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(OrderingPing request, CustomValidationException exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            _log.Add("NonHandlingNear");
            return Task.CompletedTask;
        }
    }

    internal sealed class NearExceptionAction : IRequestExceptionAction<OrderingPing, CustomValidationException>
    {
        private readonly List<string> _log;

        public NearExceptionAction(List<string> log)
        {
            _log = log;
        }

        public Task Execute(OrderingPing request, CustomValidationException exception, CancellationToken cancellationToken)
        {
            _log.Add("Near");
            return Task.CompletedTask;
        }
    }

    // Exact-exception-type handler placed in a FAR namespace, to prove
    // exception-type specificity still outranks handler proximity (item 2).
    internal sealed class NearBaseExceptionTypeHandler : IRequestExceptionHandler<OrderingPing, OrderingPong, Exception>
    {
        private readonly List<string> _log;

        public NearBaseExceptionTypeHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(OrderingPing request, Exception exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            _log.Add("NearBaseType");
            state.SetHandled(new OrderingPong("handled-by-near-base-type"));
            return Task.CompletedTask;
        }
    }

    // Item 7: a single concrete type implementing exception handlers/actions
    // for TWO different exception-type levels in the same hierarchy walk
    // (CustomValidationException, the exact thrown type, and Exception, a
    // base type further up). Used to prove verified target dedup
    // semantics: handlers have no cross-level dedup (may run at both
    // levels if not handled at the more specific one); actions dedupe by
    // concrete type across levels (run once, at the most specific level).
    internal sealed class DualLevelExceptionHandler :
        IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>,
        IRequestExceptionHandler<OrderingPing, OrderingPong, Exception>
    {
        private readonly List<string> _log;

        public DualLevelExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public int CallCount { get; private set; }

        public Task Handle(OrderingPing request, CustomValidationException exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            CallCount++;
            _log.Add("DualLevel");
            return Task.CompletedTask; // deliberately does not call SetHandled
        }

        public Task Handle(OrderingPing request, Exception exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            CallCount++;
            _log.Add("DualLevel");
            return Task.CompletedTask; // deliberately does not call SetHandled
        }
    }

    internal sealed class DualLevelExceptionAction :
        IRequestExceptionAction<OrderingPing, CustomValidationException>,
        IRequestExceptionAction<OrderingPing, Exception>
    {
        private readonly List<string> _log;

        public DualLevelExceptionAction(List<string> log)
        {
            _log = log;
        }

        public int CallCount { get; private set; }

        public Task Execute(OrderingPing request, CustomValidationException exception, CancellationToken cancellationToken)
        {
            CallCount++;
            _log.Add("DualLevel");
            return Task.CompletedTask;
        }

        public Task Execute(OrderingPing request, Exception exception, CancellationToken cancellationToken)
        {
            CallCount++;
            _log.Add("DualLevel");
            return Task.CompletedTask;
        }
    }
}

namespace NEXGov.Mediator.UnitTests.ExceptionOrderingScenario.Other
{
    using NEXGov.Mediator.UnitTests.ExceptionOrderingScenario.Commands;

    internal sealed class FarExceptionHandler : IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>
    {
        private readonly List<string> _log;

        public FarExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(OrderingPing request, CustomValidationException exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            _log.Add("Far");
            state.SetHandled(new OrderingPong("handled-by-far"));
            return Task.CompletedTask;
        }
    }

    internal sealed class FarExceptionAction : IRequestExceptionAction<OrderingPing, CustomValidationException>
    {
        private readonly List<string> _log;

        public FarExceptionAction(List<string> log)
        {
            _log = log;
        }

        public Task Execute(OrderingPing request, CustomValidationException exception, CancellationToken cancellationToken)
        {
            _log.Add("Far");
            return Task.CompletedTask;
        }
    }

    // Exact exception type, far namespace — used to prove exact-type-in-a-far-namespace
    // still beats base-type-in-a-near-namespace (item 2).
    internal sealed class FarExactExceptionTypeHandler : IRequestExceptionHandler<OrderingPing, OrderingPong, CustomValidationException>
    {
        private readonly List<string> _log;

        public FarExactExceptionTypeHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(OrderingPing request, CustomValidationException exception, RequestExceptionHandlerState<OrderingPong> state, CancellationToken cancellationToken)
        {
            _log.Add("FarExactType");
            state.SetHandled(new OrderingPong("handled-by-far-exact-type"));
            return Task.CompletedTask;
        }
    }
}
