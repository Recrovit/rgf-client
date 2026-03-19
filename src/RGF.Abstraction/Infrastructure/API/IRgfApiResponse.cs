#nullable enable
using System.Net;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

public interface IRgfApiResponse<ResultType>
{
    bool Success { get; }

    HttpStatusCode StatusCode { get; }

    string? ReasonPhrase { get; set; }

    ResultType Result { get; }

    string ErrorMessage { get; }
}

public class ApiResponse<ResultType> : IRgfApiResponse<ResultType>
{
    public bool Success { get; set; }

    public HttpStatusCode StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public ResultType Result { get; set; } = default!;

    public string ErrorMessage { get; set; } = string.Empty;
}
