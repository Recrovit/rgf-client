using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;

/// <summary>Tracks host session usability and authentication requirements; backend authorization remains separate.</summary>
public interface IRgfAuthenticationSessionMonitor : IRgfAuthenticationFailureHandler
{
    /// <summary>Last known session usability; the standard monitor assumes validity until explicit reauthentication failure.</summary>
    bool HasValidSession { get; }

    /// <summary>Notification when session usability changes.</summary>
    event Action? SessionStateChanged;

    /// <summary>Validates when the session has not yet been confirmed; confirmed checks may avoid HTTP probing.</summary>
    Task EnsureValidatedAsync(CancellationToken cancellationToken);

    /// <summary>Forces a host session probe; explicit reauthentication-required responses invalidate the session.</summary>
    Task ProbeAsync(CancellationToken cancellationToken);

    /// <summary>Sets the active route requirement; an invalid session on a protected route triggers login navigation.</summary>
    void SetRouteAuthenticationRequired(bool requiresAuthentication);

    /// <summary>Adds a component authentication requirement until the returned scope is disposed.</summary>
    IDisposable BeginAuthenticationRequirementScope();
}
