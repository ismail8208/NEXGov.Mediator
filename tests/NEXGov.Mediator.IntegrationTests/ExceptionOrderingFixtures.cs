using NEXGov.Mediator.Pipeline;

// MED-015 integration fixtures. CreateOrder lives in .Ordering.Feature.Commands;
// handlers/actions are spread across that exact namespace, its parent, its
// grandparent, and an unrelated sibling namespace, so AddMediatR/Send
// integration tests can exercise real namespace-proximity ordering
// end-to-end (not just the direct HandlerPriorityOrderer unit tests).

namespace NEXGov.Mediator.IntegrationTests.Ordering.Feature.Commands
{
    public sealed record CreateOrder(string Message) : IRequest<OrderResult>;

    public sealed record OrderResult(string Message);

    public sealed class ThrowingCreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
    {
        public Task<OrderResult> Handle(CreateOrder request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"boom:{request.Message}");
    }

    public sealed class ExactNamespaceOrderExceptionHandler : IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>
    {
        private readonly List<string> _log;

        public ExactNamespaceOrderExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(CreateOrder request, InvalidOperationException exception, RequestExceptionHandlerState<OrderResult> state, CancellationToken cancellationToken)
        {
            _log.Add("Exact");
            state.SetHandled(new OrderResult("handled-by-exact"));
            return Task.CompletedTask;
        }
    }

    public sealed class ExactNamespaceOrderExceptionAction : IRequestExceptionAction<CreateOrder, InvalidOperationException>
    {
        private readonly List<string> _log;

        public ExactNamespaceOrderExceptionAction(List<string> log)
        {
            _log = log;
        }

        public Task Execute(CreateOrder request, InvalidOperationException exception, CancellationToken cancellationToken)
        {
            _log.Add("Exact");
            return Task.CompletedTask;
        }
    }

    // Item 17: base/derived handler discovered by MED-012 inherited scanning.
    public abstract class OrderExceptionHandlerBase : IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>
    {
        public abstract Task Handle(CreateOrder request, InvalidOperationException exception, RequestExceptionHandlerState<OrderResult> state, CancellationToken cancellationToken);
    }

    public sealed class DerivedOrderExceptionHandler : OrderExceptionHandlerBase
    {
        public override Task Handle(CreateOrder request, InvalidOperationException exception, RequestExceptionHandlerState<OrderResult> state, CancellationToken cancellationToken)
        {
            state.SetHandled(new OrderResult("handled-by-derived"));
            return Task.CompletedTask;
        }
    }
}

namespace NEXGov.Mediator.IntegrationTests.Ordering.Feature
{
    using NEXGov.Mediator.IntegrationTests.Ordering.Feature.Commands;

    public sealed class ParentNamespaceOrderExceptionHandler : IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>
    {
        private readonly List<string> _log;

        public ParentNamespaceOrderExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(CreateOrder request, InvalidOperationException exception, RequestExceptionHandlerState<OrderResult> state, CancellationToken cancellationToken)
        {
            _log.Add("Parent");
            state.SetHandled(new OrderResult("handled-by-parent"));
            return Task.CompletedTask;
        }
    }

    public sealed class ParentNamespaceOrderExceptionAction : IRequestExceptionAction<CreateOrder, InvalidOperationException>
    {
        private readonly List<string> _log;

        public ParentNamespaceOrderExceptionAction(List<string> log)
        {
            _log = log;
        }

        public Task Execute(CreateOrder request, InvalidOperationException exception, CancellationToken cancellationToken)
        {
            _log.Add("Parent");
            return Task.CompletedTask;
        }
    }
}

namespace NEXGov.Mediator.IntegrationTests.Ordering
{
    using NEXGov.Mediator.IntegrationTests.Ordering.Feature.Commands;

    public sealed class GrandparentNamespaceOrderExceptionHandler : IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>
    {
        private readonly List<string> _log;

        public GrandparentNamespaceOrderExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(CreateOrder request, InvalidOperationException exception, RequestExceptionHandlerState<OrderResult> state, CancellationToken cancellationToken)
        {
            _log.Add("Grandparent");
            state.SetHandled(new OrderResult("handled-by-grandparent"));
            return Task.CompletedTask;
        }
    }
}

namespace NEXGov.Mediator.IntegrationTests.Ordering.Other
{
    using NEXGov.Mediator.IntegrationTests.Ordering.Feature.Commands;

    public sealed class UnrelatedNamespaceOrderExceptionHandler : IRequestExceptionHandler<CreateOrder, OrderResult, InvalidOperationException>
    {
        private readonly List<string> _log;

        public UnrelatedNamespaceOrderExceptionHandler(List<string> log)
        {
            _log = log;
        }

        public Task Handle(CreateOrder request, InvalidOperationException exception, RequestExceptionHandlerState<OrderResult> state, CancellationToken cancellationToken)
        {
            _log.Add("Unrelated");
            state.SetHandled(new OrderResult("handled-by-unrelated"));
            return Task.CompletedTask;
        }
    }

    public sealed class UnrelatedNamespaceOrderExceptionAction : IRequestExceptionAction<CreateOrder, InvalidOperationException>
    {
        private readonly List<string> _log;

        public UnrelatedNamespaceOrderExceptionAction(List<string> log)
        {
            _log = log;
        }

        public Task Execute(CreateOrder request, InvalidOperationException exception, CancellationToken cancellationToken)
        {
            _log.Add("Unrelated");
            return Task.CompletedTask;
        }
    }
}
