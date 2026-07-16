using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public static class DashboardApiExtensions
{
    public static Task<IRgfApiResponse<RgfResult<List<RgfDashboardEntityOption>>>> GetDashboardEntityOptionsAsync(this IRgfApiService service)
        => service.GetAsync<RgfResult<List<RgfDashboardEntityOption>>>("/rgf/api/dashboard/entities");

    public static Task<IRgfApiResponse<RgfResult<RgfDashboardEntitySettingsResult>>> GetDashboardEntitySettingsAsync(this IRgfApiService service, int entityId)
        => service.GetAsync<RgfResult<RgfDashboardEntitySettingsResult>>($"/rgf/api/dashboard/settings/{entityId}");

    public static Task<IRgfApiResponse<RgfResult<RgfDashboardCatalogResult>>> GetDashboardCatalogAsync(this IRgfApiService service)
        => service.GetAsync<RgfResult<RgfDashboardCatalogResult>>("/rgf/api/dashboard/catalog");

    public static Task<IRgfApiResponse<RgfResult<RgfDashboardDefinition>>> GetDashboardAsync(this IRgfApiService service, int dashboardId)
        => service.GetAsync<RgfResult<RgfDashboardDefinition>>($"/rgf/api/dashboard/{dashboardId}");

    public static Task<IRgfApiResponse<RgfResult<RgfDashboardDefinition>>> SaveDashboardAsync(this IRgfApiService service, RgfDashboardDefinition dashboard)
        => service.PostAsync<RgfResult<RgfDashboardDefinition>, RgfDashboardDefinition>("/rgf/api/dashboard/save", dashboard);

    public static Task<IRgfApiResponse<RgfResult<RgfEmptyResult>>> DeleteDashboardAsync(this IRgfApiService service, RgfDashboardDefinition dashboard)
        => service.PostAsync<RgfResult<RgfEmptyResult>, RgfDashboardDefinition>("/rgf/api/dashboard/delete", dashboard);
}
