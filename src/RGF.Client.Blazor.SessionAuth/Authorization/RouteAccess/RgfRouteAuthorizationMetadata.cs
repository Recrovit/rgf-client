using Microsoft.AspNetCore.Authorization;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authorization.RouteAccess;

internal static class RgfRouteAuthorizationMetadata
{
    public static bool RequiresAuthentication(Type pageType)
    {
        if (pageType.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
        {
            return false;
        }

        return pageType.IsDefined(typeof(AuthorizeAttribute), inherit: true);
    }

    public static IReadOnlyList<IAuthorizeData> GetAuthorizeData(Type pageType)
    {
        if (pageType.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
        {
            return [];
        }

        return pageType
            .GetCustomAttributes(inherit: true)
            .OfType<IAuthorizeData>()
            .ToArray();
    }
}
