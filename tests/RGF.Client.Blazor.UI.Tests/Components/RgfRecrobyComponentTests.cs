using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class RgfRecrobyComponentTests
{
    [Fact]
    public async Task ProviderConfigurationErrorIsDisplayedWithoutAssistantHistoryOrIdentity()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRgfApiService>(new ProviderMissingApi());
        var cut = context.Render<RgfRecrobyComponent>();
        cut.Find("textarea").Change("Hello");
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.Equal("No AI provider is configured. Configure an AI provider and model to use Recroby.",
            cut.Find("[data-error-kind=configuration]").TextContent);
        var state = cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation;
        Assert.Equal(RgfAiMessageRole.User, Assert.Single(state.Messages).Role);
        Assert.Null(state.ConversationId);
        Assert.Null(state.ConversationToken);
    }

    private sealed class ProviderMissingApi : IRgfApiService
    {
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
            => Task.FromResult<IRgfApiResponse<T>>(new ApiResponse<T>
            {
                Success = true,
                Result = (T)(object)new RgfAiResponse { Success = false, ErrorCode = RgfAiErrorCodes.AiProviderNotConfigured }
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendFailureForwardsOriginalExceptionToOptionalHostCallback(bool useCallback)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        var error = new HttpRequestException("API failed");
        context.Services.AddSingleton<IRgfApiService>(new FailingApi(error));
        Exception? received = null;
        var calls = 0;
        var cut = context.Render<RgfRecrobyComponent>(p =>
        {
            if (useCallback) p.Add(c => c.SendFailed, (Exception exception) => { received = exception; calls++; });
        });
        var child = cut.FindComponent<RgfAiChatComponent>();
        Assert.Equal(cut.Instance.SendFailed, child.Instance.SendFailed);
        cut.Find("textarea").Change("Hello");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        if (useCallback) Assert.Same(error, received);
        else Assert.Null(received);
        Assert.Equal(useCallback ? 1 : 0, calls);
        Assert.False(child.Instance.Session.IsProcessing);
    }

    private sealed class FailingApi(Exception error) : IRgfApiService
    {
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class => Task.FromException<IRgfApiResponse<T>>(error);
    }
}
