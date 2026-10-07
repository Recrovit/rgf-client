using System.Net;
using System.Text.Json;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Client.AI.Transport;

namespace Recrovit.RecroGridFramework.Client.Tests.AI;

public sealed class RgfAiApiTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulApiEnvelopeReturnsOriginalAiResponseRegardlessOfWorkflowSuccess(bool success)
    {
        var response = new RgfAiResponse { Success = success, Message = "answer", ConversationToken = "token" };
        var api = new RecordingApi { Response = new() { Success = true, StatusCode = HttpStatusCode.OK, Result = response } };
        Assert.Same(response, await new RgfAiApiTransport(api).SendAsync(new() { CurrentUserMessage = "Hello" }, Token));
    }

    [Fact]
    public async Task SendsWebJsonThroughAuthenticatedApiWithCancellation()
    {
        var api = new RecordingApi();
        var request = new RgfAiRequest { CurrentUserMessage = "Hello", ConversationId = "conversation", ConversationToken = "protected", AiModelOverride = "model" };
        await new RgfAiApiTransport(api).SendAsync(request, Token);
        Assert.Equal("/api/rgf/ai/recroby", api.Request!.Uri);
        Assert.True(api.Request.AuthClient);
        Assert.Equal(Token, api.Request.CancellationToken);
        Assert.Equal("application/json", api.ContentType);
        var payload = JsonSerializer.Deserialize<RgfAiRequest>(api.Json!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(request.CurrentUserMessage, payload.CurrentUserMessage);
        Assert.Equal(request.ConversationId, payload.ConversationId);
        Assert.Equal(request.ConversationToken, payload.ConversationToken);
        Assert.Equal(request.AiModelOverride, payload.AiModelOverride);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.OK)]
    public async Task FailedApiEnvelopeThrowsEvenWhenItContainsAResponse(HttpStatusCode status)
    {
        var api = new RecordingApi { Response = new() { Success = false, StatusCode = status, Result = new() { Success = true } } };
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => new RgfAiApiTransport(api).SendAsync(new(), Token));
        Assert.Equal(status, error.StatusCode);
    }

    [Fact]
    public async Task SuccessfulEnvelopeWithNullResultIsProtocolFailure()
    {
        var api = new RecordingApi { Response = new() { Success = true, StatusCode = HttpStatusCode.OK, Result = null! } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RgfAiApiTransport(api).SendAsync(new(), Token));
    }

    [Fact]
    public async Task PreCancelledSendDoesNotInvokeApi()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var api = new RecordingApi();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RgfAiApiTransport(api).SendAsync(new(), cancellation.Token));
        Assert.Null(api.Request);
    }

    [Fact]
    public async Task CancellationReportedAsFailedApiEnvelopeStillThrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new RecordingApi
        {
            Response = new() { Success = false },
            BeforeResponse = cancellation.Cancel
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RgfAiApiTransport(api).SendAsync(new(), cancellation.Token));
        Assert.Equal(cancellation.Token, api.Request!.CancellationToken);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class RecordingApi : IRgfApiService
    {
        public ApiResponse<RgfAiResponse> Response { get; init; } = new() { Success = true, StatusCode = HttpStatusCode.OK, Result = new() { Success = true } };
        public Action? BeforeResponse { get; init; }
        public IRgfApiRequest? Request { get; private set; }
        public string? Json { get; private set; }
        public string? ContentType { get; private set; }
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Assert.Equal(typeof(RgfAiResponse), typeof(T));
            Request = request;
            Json = await request.Content.ReadAsStringAsync(request.CancellationToken);
            ContentType = request.Content.Headers.ContentType?.MediaType;
            BeforeResponse?.Invoke();
            return (IRgfApiResponse<T>)(object)Response;
        }
    }
}
