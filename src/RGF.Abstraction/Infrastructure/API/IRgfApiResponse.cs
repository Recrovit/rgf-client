#nullable enable
using System.Net;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

/// <summary>HTTP envelope separating transport/parsing success from any business success in its payload.</summary>
public interface IRgfApiResponse<ResultType>
{
    /// <summary>Whether HTTP processing and payload reading succeeded.</summary>
    bool Success { get; }

    /// <summary>HTTP status when available; may remain its default after a transport failure.</summary>
    HttpStatusCode StatusCode { get; }

    /// <summary>HTTP reason phrase when supplied by the server.</summary>
    string? ReasonPhrase { get; set; }

    /// <summary>Typed result; may be absent on failure, so inspect Success before using it.</summary>
    ResultType Result { get; }

    /// <summary>Failure diagnostic for unsuccessful processing; empty when no diagnostic is supplied.</summary>
    string ErrorMessage { get; }
}

/// <summary>Mutable HTTP result envelope populated by API implementations.</summary>
public class ApiResponse<ResultType> : IRgfApiResponse<ResultType>
{
    /// <inheritdoc />
    public bool Success { get; set; }

    /// <inheritdoc />
    public HttpStatusCode StatusCode { get; set; }

    /// <inheritdoc />
    public string? ReasonPhrase { get; set; }

    /// <inheritdoc />
    public ResultType Result { get; set; } = default!;

    /// <inheritdoc />
    public string ErrorMessage { get; set; } = string.Empty;
}
