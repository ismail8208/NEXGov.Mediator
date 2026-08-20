namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Controls whether automatically-wired exception actions observe every
/// exception, or only exceptions that no exception handler marks
/// handled.
/// </summary>
public enum RequestExceptionActionProcessorStrategy
{
    /// <summary>
    /// Exception actions only observe exceptions that remain unhandled after exception handlers have run.
    /// </summary>
    ApplyForUnhandledExceptions,

    /// <summary>
    /// Exception actions observe every exception, regardless of whether an exception handler later marks it handled.
    /// </summary>
    ApplyForAllExceptions,
}
