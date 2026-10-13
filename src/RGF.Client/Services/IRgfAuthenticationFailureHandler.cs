namespace Recrovit.RecroGridFramework.Client.Services;

/// <summary>Receives API authentication failures for integration-specific state updates or reauthentication.</summary>
public interface IRgfAuthenticationFailureHandler
{
    /// <summary>Handles an unauthorized response using its explicit reauthentication signal.</summary>
    /// <param name="context">HTTP status, headers and reauthentication signal.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>Completion of failure handling.</returns>
    ValueTask HandleUnauthorizedAsync(RgfAuthenticationFailureContext context, CancellationToken cancellationToken);
}

/// <summary>Default failure adapter that performs no navigation or authentication-state update.</summary>
public sealed class NoOpRgfAuthenticationFailureHandler : IRgfAuthenticationFailureHandler
{
    /// <inheritdoc />
    public ValueTask HandleUnauthorizedAsync(RgfAuthenticationFailureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
