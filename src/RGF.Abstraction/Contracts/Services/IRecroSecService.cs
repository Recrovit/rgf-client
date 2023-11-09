using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRecroSecService
{
    bool IsAuthenticated { get; }

    bool IsAdmin { get; }

    List<string> UserRoles { get; }

    ClaimsPrincipal CurrentUser { get; }

    Task<RgfPermissions> GetPermissionsAsync(string objectName, string objectKey, int expiration = 60);

    Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60);
}
