namespace NEXGov.Mediator;

/// <summary>
/// Combines <see cref="ISender"/> and <see cref="IPublisher"/> into a
/// single mediator abstraction supporting both request dispatch and
/// notification publishing.
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
