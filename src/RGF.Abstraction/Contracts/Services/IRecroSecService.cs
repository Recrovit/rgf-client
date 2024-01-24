using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRecroSecService
{
    bool IsAuthenticated { get; }

    bool IsAdmin { get; }

    List<string> UserRoles { get; }

    ClaimsPrincipal CurrentUser { get; }

    string UserLanguage { get; }

    Task<string> SetUserLanguageAsync(string language);

    EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; }

    Task<RgfPermissions> GetPermissionsAsync(string objectName, string objectKey, int expiration = 60);

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
