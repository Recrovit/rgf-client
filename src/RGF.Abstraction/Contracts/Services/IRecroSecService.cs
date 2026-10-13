using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using System.Globalization;
using System.Security.Claims;

#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

/// <summary>Client view of the authenticated principal, backend user state and permission queries; enforcement belongs to the backend.</summary>
public interface IRecroSecService
{
    /// <summary>Notification after the client authentication state changes.</summary>
    EventDispatcher<EventArgs> AuthenticationStateChanged { get; }

    /// <summary>Notification when backend user-state data changes.</summary>
    EventDispatcher<DataEventArgs<RgfUserState>> UserStateChangedEvent { get; }

    /// <summary>Display name from the current principal; may be absent.</summary>
    string? UserName { get; }

    /// <summary>Whether the current principal has an authenticated identity.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Backend user-state administrator flag.</summary>
    bool IsAdmin { get; }

    /// <summary>Role claim values from the client principal, separate from backend role metadata.</summary>
    List<string> RoleClaim { get; }

    /// <summary>Current client principal; initial state may be anonymous until synchronization completes.</summary>
    ClaimsPrincipal CurrentUser { get; }

    /// <summary>Last available backend user-state data.</summary>
    RgfUserState UserState { get; }

    /// <summary>Backend role identifiers and labels; empty when none are available.</summary>
    IReadOnlyDictionary<string, string> Roles { get; }

    /// <summary>Gets a bearer token through the registered adapter, or null when the mode supplies no token.</summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>Effective user language with backend dictionary default fallback.</summary>
    string UserLanguage { get; }

    /// <summary>Attempts to select a supported language and returns the previous language.</summary>
    Task<string?> SetUserLanguageAsync(string? language);

    /// <summary>Notification after the client language changes.</summary>
    EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; }

    /// <summary>Persists user-state settings through the backend and reports success.</summary>
    Task<bool> UpdateUserStateSettingsAsync(IDictionary<string, string?> settings);

    /// <summary>Queries entity permissions, optionally for a record key, with a cache lifetime in seconds.</summary>
    Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60);

    /// <summary>Queries object permissions, optionally for an object key, with a cache lifetime in seconds.</summary>
    Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60);

    /// <summary>Queries multiple permission objects using a cache lifetime in seconds.</summary>
    Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60);
}

public static class IRecroSecServiceExtension
{
    public static CultureInfo UserCultureInfo(this IRecroSecService recroSec)
    {
        string lang = recroSec.UserLanguage.ToLower();
        switch (lang)
        {
            case "hun":
                return new CultureInfo("hu-HU");
            case "eng":
                return new CultureInfo("en");
            default:
                if (lang.Length >= 2)
                {
                    return new CultureInfo(lang.Substring(0, 2));
                }
                break;
        }
        return CultureInfo.CurrentCulture;
    }
}
