using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal sealed class FakeRgfApiService : IRgfApiService
{
    public List<RequestRecord> Requests { get; } = [];

    public string[] ScriptReferencesResult { get; set; } = [];

    public bool Success { get; set; } = true;

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

        throw new NotSupportedException($"Unsupported GET result type '{typeof(ResultType)}'.");
    }

    public Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request)
        where ResultType : class
        => throw new NotSupportedException("POST requests are not expected in these tests.");

    internal sealed record RequestRecord(string Uri, bool AuthClient);
}
