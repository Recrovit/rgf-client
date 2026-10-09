using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class RgfRecrobyComponentTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/test-root")]
    public void RecrobyModuleImportsUseConfiguredAppRoot(string root)
    {
        RgfClientBlazorUiTestState.ConfigureClientPaths(root, "https://api.example.test");
        try
        {
            using var context = SelectionContext(out _);
            context.Render<RgfRecrobyWorkspace>();
            foreach (var module in new[] { "RgfRecrobyComponent", "RgfRecrobyWorkspace" })
                Assert.Contains(context.JSInterop.Invocations, invocation => invocation.Identifier == "import"
                    && Equals(invocation.Arguments[0], $"{root}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Components/AI/{module}.razor.js"));
        }
        finally { RgfClientBlazorUiTestState.Reset(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedCatalogIsRetriedOnLaterRenderAndSuccessIsShared(bool throws)
    {
        using var context = SelectionContext(out var api);
        api.FailNextCatalog = true;
        api.ThrowOnFailure = throws;
        var first = context.Render<RgfRecrobyComponent>();
        Assert.Empty(first.FindAll("[popovertarget]"));
        Assert.Equal(1, api.CatalogCalls);
        first.Render();
        first.WaitForAssertion(() => Assert.Equal(3, first.FindAll("select").Count));
        Assert.Equal(2, api.CatalogCalls);
        var second = context.Render<RgfRecrobyComponent>();
        Assert.Equal(3, second.FindAll("select").Count);
        Assert.Equal(2, api.CatalogCalls);
    }

    [Fact]
    public void ConcurrentCatalogLookupSurvivesClosingOneConversation()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        var api = new DelayedCatalogApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var first = context.Render<RgfRecrobyComponent>();
        var second = context.Render<RgfRecrobyComponent>();
        Assert.Equal(1, api.CatalogCalls);
        first.Dispose();
        api.Catalog.SetResult(new ApiResponse<RgfAiCatalogResponse> { Success = true,
            Result = new("p", [new("p", "model", [new("model", [], [], null, null)])]) });
        second.WaitForAssertion(() => Assert.Equal(3, second.FindAll("select").Count));
        Assert.Equal(1, api.CatalogCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowCatalogDoesNotPreventSendingWithDefaultSettings(bool malformed)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        var api = new DelayedCatalogApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyComponent>();
        Assert.Empty(cut.FindAll("[popovertarget]"));
        cut.Find("textarea").Change("Hello");
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.Messages.OfType<RgfAiResponse>());
        var renders = cut.RenderCount;
        api.Catalog.SetResult(new ApiResponse<RgfAiCatalogResponse> { Success = true, Result = new("", malformed ? null! : []) });
        cut.WaitForState(() => cut.RenderCount > renders);
        Assert.Empty(cut.FindAll("[popovertarget]"));
    }

    private sealed class DelayedCatalogApi : IRgfApiService
    {
        public int CatalogCalls { get; private set; }
        public TaskCompletionSource<IRgfApiResponse<RgfAiCatalogResponse>> Catalog { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class
        {
            CatalogCalls++;
            return (IRgfApiResponse<T>)(object)await Catalog.Task;
        }
        public Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
            => Task.FromResult<IRgfApiResponse<T>>(new ApiResponse<T> { Success = true,
                Result = (T)(object)new RgfAiResponse { Success = true, WorkflowStatus = "Completed" } });
    }

    [Theory]
    [InlineData("Floating")]
    [InlineData("DockLeft")]
    [InlineData("DockRight")]
    [InlineData("DockBottom")]
    public void WorkspacePreservesSelectionsAcrossSwitchCollapseAndReopen(string mode)
    {
        using var context = SelectionContext(out var api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        cut.Find("[aria-label='Open Recroby']").Click();
        cut.Find($"[data-dock={mode}]").Click();
        var first = cut.FindComponents<RgfRecrobyComponent>()[0];
        first.FindAll("select")[2].Change("high");
        cut.Find("[data-action=new]").Click();
        var second = cut.FindComponents<RgfRecrobyComponent>()[1];
        second.FindAll("select")[0].Change("q");
        cut.Find("[data-action=switch]").Click();
        cut.FindAll("[data-action=select]")[0].Click();
        cut.Find("[aria-label='Collapse Recroby']").Click();
        cut.Find("[aria-label='Open Recroby']").Click();
        Assert.Contains("p / first", first.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Contains("high", first.Find("button[aria-label='Reasoning effort']").TextContent);
        Assert.Contains("q / other", second.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Null(second.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.AiReasoningEffortOverride);
        Assert.Equal(1, api.CatalogCalls);
    }

    [Fact]
    public async Task CatalogSelectionsFollowProviderResetEffortAndReachTheRequest()
    {
        using var context = SelectionContext(out var api);
        var cut = context.Render<RgfRecrobyComponent>();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("select").Count));
        Assert.Contains("p / first", cut.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Equal(new[] { "first", "second" }, cut.FindAll("select")[1].QuerySelectorAll("option").Select(o => o.TextContent));
        cut.FindAll("select")[2].Change("high");
        cut.FindAll("select")[1].Change("second");
        Assert.Equal("Default", cut.Find("button[aria-label='Reasoning effort']").TextContent.Trim().TrimEnd('▾').Trim());
        Assert.Equal(new[] { "Default", "low" }, cut.FindAll("select")[2].QuerySelectorAll("option").Select(o => o.TextContent));
        cut.FindAll("select")[2].Change("low");
        cut.FindAll("select")[0].Change("q");
        Assert.Equal(new[] { "other" }, cut.FindAll("select")[1].QuerySelectorAll("option").Select(o => o.TextContent));
        var state = cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation;
        Assert.Equal("q/other", state.AiModelOverride);
        Assert.Null(state.AiReasoningEffortOverride);
        cut.FindAll("select")[2].Change("high");
        cut.Find("textarea").Change("Hello");
        Assert.Empty(cut.FindAll(".rgf-ai-actions textarea"));
        await cut.Find(".rgf-ai-actions > button").ClickAsync(new MouseEventArgs());
        Assert.Equal("q/other", api.Request!.AiModelOverride);
        Assert.Equal("high", api.Request.AiReasoningEffortOverride);
        Assert.Equal("/api/rgf/ai/recroby/catalog", api.CatalogUri);
        cut.FindAll("select")[0].Change("p");
        Assert.Equal("p/first", state.AiModelOverride);
        Assert.Null(state.CreateRequest("Again").AiReasoningEffortOverride);
        Assert.All(cut.FindAll("[popover]"), panel => Assert.Equal("auto", panel.GetAttribute("popover")));
    }

    [Fact]
    public async Task ProcessingAndPendingWorkflowDisableSelectorsAndSessionsRetainIndependentChoices()
    {
        using var context = SelectionContext(out _);
        var transport = new SelectionTransport();
        var first = new RgfAiConversationSession(transport);
        var second = new RgfAiConversationSession(new SelectionTransport());
        var cut = context.Render<RgfRecrobyComponent>(p => p.Add(c => c.Session, first));
        cut.FindAll("select")[2].Change("high");
        Task<RgfAiResponse>? send = null;
        await cut.InvokeAsync(() => { send = first.SendAsync("Hello"); });
        cut.WaitForAssertion(() => Assert.All(cut.FindAll("select, [popovertarget]"), item => Assert.True(item.HasAttribute("disabled"))));
        transport.Response.SetResult(new() { Success = true, WorkflowStatus = "WaitingForInput" });
        await send!;
        cut.WaitForAssertion(() => Assert.All(cut.FindAll("select, [popovertarget]"), item => Assert.True(item.HasAttribute("disabled"))));
        cut.FindAll("select")[0].Change("q");
        Assert.Equal("p/first", first.Conversation.AiModelOverride);
        cut.Render(p => p.Add(c => c.Session, second));
        Assert.All(cut.FindAll("select, [popovertarget]"), item => Assert.False(item.HasAttribute("disabled")));
        cut.FindAll("select")[0].Change("q");
        cut.Render(p => p.Add(c => c.Session, first));
        Assert.Contains("p / first", cut.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Equal("high", first.Conversation.AiReasoningEffortOverride);
        Assert.Equal("q/other", second.Conversation.AiModelOverride);
    }

    private static BunitContext SelectionContext(out CatalogApi api)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        api = new CatalogApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        return context;
    }

    private sealed class SelectionTransport : IRgfAiTransport
    {
        public TaskCompletionSource<RgfAiResponse> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default) => Response.Task;
    }

    private sealed class CatalogApi : IRgfApiService
    {
        public int CatalogCalls { get; private set; }
        public bool FailNextCatalog { get; set; }
        public bool ThrowOnFailure { get; set; }
        public RgfAiRequest? Request { get; private set; }
        public string? CatalogUri { get; private set; }
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class
        {
            CatalogCalls++;
            if (FailNextCatalog)
            {
                FailNextCatalog = false;
                if (ThrowOnFailure) throw new HttpRequestException("Transient catalog failure");
                return Task.FromResult<IRgfApiResponse<T>>(new ApiResponse<T> { Success = false });
            }
            CatalogUri = request.Uri;
            return Task.FromResult<IRgfApiResponse<T>>(new ApiResponse<T> { Success = true,
                Result = (T)(object)new RgfAiCatalogResponse("p", [
                    new("p", "first", [new("first", [], ["low", "high"], null, "high"), new("second", [], ["low"], null, null)]),
                    new("q", "other", [new("other", [], ["high"], null, null)])]) });
        }
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Request = System.Text.Json.JsonSerializer.Deserialize<RgfAiRequest>(await request.Content.ReadAsStringAsync(),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            return new ApiResponse<T> { Success = true,
                Result = (T)(object)new RgfAiResponse { Success = true, WorkflowStatus = "Completed" } };
        }
    }

    [Fact]
    public async Task ProviderConfigurationErrorIsDisplayedWithoutAssistantHistoryOrIdentity()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRgfApiService>(new ProviderMissingApi());
        var cut = context.Render<RgfRecrobyComponent>();
        Assert.Empty(cut.FindAll("[popovertarget]"));
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
