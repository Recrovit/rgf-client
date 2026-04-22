using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;

public interface IRgfAuthenticationSessionMonitor : IRgfAuthenticationFailureHandler
{
    bool HasValidSession { get; }

    event Action? SessionStateChanged;

    Task EnsureValidatedAsync(CancellationToken cancellationToken);

    Task ProbeAsync(CancellationToken cancellationToken);

    void SetRouteAuthenticationRequired(bool requiresAuthentication);

    IDisposable BeginAuthenticationRequirementScope();
}
