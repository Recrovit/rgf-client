using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using System.Globalization;
using System.Security.Claims;

#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRecroSecService
{
    EventDispatcher<EventArgs> AuthenticationStateChanged { get; }

    EventDispatcher<DataEventArgs<RgfUserState>> UserStateChangedEvent { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }

    bool IsAdmin { get; }

    List<string> RoleClaim { get; }

    ClaimsPrincipal CurrentUser { get; }

    RgfUserState UserState { get; }

    IReadOnlyDictionary<string, string> Roles { get; }

    Task<string?> GetAccessTokenAsync();

    string UserLanguage { get; }

    Task<string?> SetUserLanguageAsync(string? language);

    EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; }

    Task<bool> UpdateUserStateSettingsAsync(IDictionary<string, string?> settings);

    Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60);

    Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60);

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
