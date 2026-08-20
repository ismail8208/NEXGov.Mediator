using NEXGov.Mediator.Internal;

namespace NEXGov.Mediator;

/// <summary>
/// Default <see cref="IMediator"/> implementation that resolves request
/// handlers and notification handlers from an <see cref="IServiceProvider"/>
/// at the time each request is sent or notification is published.
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve request handlers for each dispatched request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public Mediator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No matching <see cref="IRequestHandler{TRequest,TResponse}"/> is registered.</exception>
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)RequestHandlerWrapperCache.GetResponseWrapper(request.GetType(), typeof(TResponse));

        return wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No matching <see cref="IRequestHandler{TRequest}"/> is registered.</exception>
    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = RequestHandlerWrapperCache.GetVoidWrapper(request.GetType());

        await wrapper.Handle(request, _serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="request"/> does not implement <see cref="IRequest"/> or <see cref="IRequest{TResponse}"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="request"/> implements more than one closed <see cref="IRequest{TResponse}"/> contract, so the
    /// response contract is ambiguous; or no matching handler is registered.
    /// </exception>
    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var concreteType = request.GetType();

        var responseContracts = concreteType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            .ToArray();

        if (responseContracts.Length > 1)
        {
            var responseTypeNames = string.Join(", ", responseContracts.Select(i => i.GetGenericArguments()[0].FullName));

            throw new InvalidOperationException(
                $"Request type '{concreteType.FullName}' implements multiple IRequest<TResponse> contracts ({responseTypeNames}). " +
                "Dynamic Send(object) cannot determine a unique response contract for this request. " +
                "Use the generic Send<TResponse> overload instead, which specifies the response type explicitly.");
        }

        if (responseContracts.Length == 1)
        {
            var responseType = responseContracts[0].GetGenericArguments()[0];
            var wrapper = RequestHandlerWrapperCache.GetResponseWrapper(concreteType, responseType);

            return await wrapper.Handle(request, _serviceProvider, cancellationToken).ConfigureAwait(false);
        }

        if (request is IRequest)
        {
            var wrapper = RequestHandlerWrapperCache.GetVoidWrapper(concreteType);

            await wrapper.Handle(request, _serviceProvider, cancellationToken).ConfigureAwait(false);

            return null;
        }

        throw new ArgumentException(
            $"'{concreteType.FullName}' is not a supported request. It must implement IRequest or IRequest<TResponse>.",
            nameof(request));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var wrapper = NotificationHandlerWrapperCache.GetWrapper(notification.GetType());

        await wrapper.Handle(notification, _serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="notification"/> does not implement <see cref="INotification"/>.</exception>
    public async Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification is not INotification)
        {
            throw new ArgumentException(
                $"'{notification.GetType().FullName}' is not a supported notification. It must implement INotification.",
                nameof(notification));
        }

        var wrapper = NotificationHandlerWrapperCache.GetWrapper(notification.GetType());

        await wrapper.Handle(notification, _serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No matching <see cref="IStreamRequestHandler{TRequest,TResponse}"/> is registered.</exception>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (StreamRequestHandlerWrapper<TResponse>)StreamRequestHandlerWrapperCache.GetWrapper(request.GetType(), typeof(TResponse));

        return wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="request"/> does not implement <see cref="IStreamRequest{TResponse}"/>.</exception>
    /// <exception cref="InvalidOperationException">No matching <see cref="IStreamRequestHandler{TRequest,TResponse}"/> is registered.</exception>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var concreteType = request.GetType();

        var streamInterfaceType = concreteType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));

        if (streamInterfaceType is null)
        {
            throw new ArgumentException(
                $"'{concreteType.FullName}' is not a supported stream request. It must implement IStreamRequest<TResponse>.",
                nameof(request));
        }

        var responseType = streamInterfaceType.GetGenericArguments()[0];
        var wrapper = StreamRequestHandlerWrapperCache.GetWrapper(concreteType, responseType);

        return wrapper.Handle(request, _serviceProvider, cancellationToken);
    }
}
