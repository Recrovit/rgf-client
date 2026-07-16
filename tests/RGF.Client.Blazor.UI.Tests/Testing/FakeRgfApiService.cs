using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Net;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal sealed class FakeRgfApiService : IRgfApiService
{
    public List<RequestRecord> Requests { get; } = [];

    public string[] ScriptReferencesResult { get; set; } = [];
    public List<RgfDashboardEntityOption> DashboardEntityOptionsResult { get; set; } = [];
    public RgfDashboardEntitySettingsResult DashboardEntitySettingsResult { get; set; } = new();
    public RgfDashboardCatalogResult DashboardCatalogResult { get; set; } = new();
    public Queue<RgfDashboardCatalogResult> DashboardCatalogResults { get; } = new();
    public RgfDashboardDefinition? DashboardResult { get; set; }
    public RgfDashboardDefinition? SaveDashboardResult { get; set; }
    public RgfEmptyResult DeleteDashboardResult { get; set; } = new();

    public bool Success { get; set; } = true;
    public bool DashboardCatalogSuccess { get; set; } = true;
    public bool DashboardLoadSuccess { get; set; } = true;
    public bool SaveDashboardSuccess { get; set; } = true;
    public bool DeleteDashboardSuccess { get; set; } = true;
    public HttpStatusCode DashboardCatalogStatusCode { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode DashboardLoadStatusCode { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode SaveDashboardStatusCode { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode DeleteDashboardStatusCode { get; set; } = HttpStatusCode.OK;
    public string DashboardCatalogErrorMessage { get; set; } = string.Empty;
    public string DashboardLoadErrorMessage { get; set; } = string.Empty;
    public string SaveDashboardErrorMessage { get; set; } = string.Empty;
    public string DeleteDashboardErrorMessage { get; set; } = string.Empty;
    public Exception? DashboardCatalogException { get; set; }
    public Exception? DashboardLoadException { get; set; }
    public Exception? SaveDashboardException { get; set; }
    public Exception? DeleteDashboardException { get; set; }
    public RgfDashboardDefinition? LastSavedDashboardPayload { get; private set; }
    public RgfDashboardDefinition? LastDeletedDashboardPayload { get; private set; }

    public Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request)
        where ResultType : class
    {
        Requests.Add(new(request.Uri, request.AuthClient));

        if (typeof(ResultType) == typeof(string[]))
        {
            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = Success,
                Result = (ResultType)(object)ScriptReferencesResult,
            });
        }

        if (typeof(ResultType) == typeof(RgfResult<List<RgfDashboardEntityOption>>))
        {
            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = Success,
                Result = (ResultType)(object)new RgfResult<List<RgfDashboardEntityOption>>
                {
                    Result = DashboardEntityOptionsResult
                }
            });
        }

        if (typeof(ResultType) == typeof(RgfResult<RgfDashboardEntitySettingsResult>))
        {
            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = Success,
                Result = (ResultType)(object)new RgfResult<RgfDashboardEntitySettingsResult>
                {
                    Result = DashboardEntitySettingsResult
                }
            });
        }

        if (typeof(ResultType) == typeof(RgfResult<RgfDashboardCatalogResult>))
        {
            if (DashboardCatalogException != null)
            {
                throw DashboardCatalogException;
            }

            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = DashboardCatalogSuccess,
                StatusCode = DashboardCatalogStatusCode,
                ErrorMessage = DashboardCatalogErrorMessage,
                Result = (ResultType)(object)new RgfResult<RgfDashboardCatalogResult>
                {
                    Result = DashboardCatalogResults.Count > 0 ? DashboardCatalogResults.Dequeue() : DashboardCatalogResult
                }
            });
        }

        if (typeof(ResultType) == typeof(RgfResult<RgfDashboardDefinition>) && request.Uri.StartsWith("/rgf/api/dashboard/", StringComparison.Ordinal))
        {
            if (DashboardLoadException != null)
            {
                throw DashboardLoadException;
            }

            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = DashboardLoadSuccess,
                StatusCode = DashboardLoadStatusCode,
                ErrorMessage = DashboardLoadErrorMessage,
                Result = (ResultType)(object)new RgfResult<RgfDashboardDefinition>
                {
                    Result = DashboardResult!
                }
            });
        }

        throw new NotSupportedException($"Unsupported GET result type '{typeof(ResultType)}'.");
    }

    public Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request)
        where ResultType : class
    {
        Requests.Add(new(request.Uri, request.AuthClient));

        if (typeof(ResultType) == typeof(RgfResult<RgfDashboardDefinition>) && string.Equals(request.Uri, "/rgf/api/dashboard/save", StringComparison.Ordinal))
        {
            if (SaveDashboardException != null)
            {
                throw SaveDashboardException;
            }

            LastSavedDashboardPayload = DeserializeDashboard(request);
            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = SaveDashboardSuccess,
                StatusCode = SaveDashboardStatusCode,
                ErrorMessage = SaveDashboardErrorMessage,
                Result = (ResultType)(object)new RgfResult<RgfDashboardDefinition>
                {
                    Result = SaveDashboardResult!
                }
            });
        }

        if (typeof(ResultType) == typeof(RgfResult<RgfEmptyResult>) && string.Equals(request.Uri, "/rgf/api/dashboard/delete", StringComparison.Ordinal))
        {
            if (DeleteDashboardException != null)
            {
                throw DeleteDashboardException;
            }

            LastDeletedDashboardPayload = DeserializeDashboard(request);
            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
            {
                Success = DeleteDashboardSuccess,
                StatusCode = DeleteDashboardStatusCode,
                ErrorMessage = DeleteDashboardErrorMessage,
                Result = (ResultType)(object)new RgfResult<RgfEmptyResult>
                {
                    Result = DeleteDashboardResult
                }
            });
        }

        throw new NotSupportedException($"Unsupported POST result type '{typeof(ResultType)}' for '{request.Uri}'.");
    }

    private static RgfDashboardDefinition? DeserializeDashboard(IRgfApiRequest request)
    {
        var payload = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<RgfDashboardDefinition>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    internal sealed record RequestRecord(string Uri, bool AuthClient);
}
