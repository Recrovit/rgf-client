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
    [Fact]
    public async Task StandaloneRecrobyLocalizesSelectorsAndChatWithoutWorkspace()
    {
        using var context = SelectionContext(out _);
        var security = context.Services.GetRequiredService<IRecroSecService>();
        var dictionary = (FakeDashboardRecroDictService)context.Services.GetRequiredService<IRecroDictService>();
        dictionary.HungarianRecrobyUi["ResponseFormat"] = "Válaszformátum";
        dictionary.HungarianRecrobyUi["FormatMinimal"] = "Minimális";
        dictionary.HungarianUi["Send"] = "Küldés";
        var cut = context.Render<RgfRecrobyComponent>();

        Assert.Contains("Minimal", cut.Find("button[aria-label='Response format']").TextContent);
        await dictionary.InitializeAsync("hun");
        await security.LanguageChangedEvent.InvokeAsync(new("hun"));
        Assert.Contains("Minimális", cut.Find("button[aria-label=Válaszformátum]").TextContent);
        Assert.Single(cut.FindAll("button[aria-label=Küldés]"));
        Assert.Equal(RgfAiResponseFormat.Minimal, cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.ResponseFormat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspaceLanguageChangeSkipsRetryButLaterRenderRetriesFailedCatalog(bool throws)
    {
        using var context = SelectionContext(out var api);
        api.FailNextCatalog = true;
        api.ThrowOnFailure = throws;
        var security = (FakeRecroSecService)context.Services.GetRequiredService<IRecroSecService>();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var dictionary = (FakeDashboardRecroDictService)context.Services.GetRequiredService<IRecroDictService>();
        dictionary.HungarianRecrobyUi["ResponseFormat"] = "Válaszformátum";
        Assert.Equal(1, api.CatalogCalls);

        security.UserLanguage = "hun";
        await dictionary.InitializeAsync("hun");
        await security.LanguageChangedEvent.InvokeAsync(new("hun"));

        Assert.Single(cut.FindAll("button[aria-label=Válaszformátum]"));
        Assert.Equal(1, api.CatalogCalls);
        Assert.Null(api.Request);

        // A subsequent unrelated parameter render restores the original retry behavior.
        cut.Render();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("select").Count));
        Assert.Equal(2, api.CatalogCalls);
    }

    [Fact]
    public async Task StandaloneLanguageChangeDoesNotDelayLaterCatalogRetry()
    {
        using var context = SelectionContext(out var api);
        api.FailNextCatalog = true;
        var security = (FakeRecroSecService)context.Services.GetRequiredService<IRecroSecService>();
        var dictionary = (FakeDashboardRecroDictService)context.Services.GetRequiredService<IRecroDictService>();
        var cut = context.Render<RgfRecrobyComponent>();
        Assert.Equal(1, api.CatalogCalls);

        security.UserLanguage = "hun";
        await dictionary.InitializeAsync("hun");
        await security.LanguageChangedEvent.InvokeAsync(new("hun"));
        Assert.Equal(1, api.CatalogCalls);

        cut.Render();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("select").Count));
        Assert.Equal(2, api.CatalogCalls);
    }

    [Fact]
    public void UserMessagesHaveIndependentCopyButtonsAndStayPlainText()
    {
        using var context = SelectionContext(out var api);
        var session = new RgfAiConversationSession(new SelectionTransport());
        const string first = "  **Első üzenet**\n\n<b>szöveg</b>  ";
        const string second = "Második üzenet";
        session.Conversation.AddUserMessage(first);
        session.Conversation.ApplyResponse(new() { Success = true, Message = "Reply" });
        session.Conversation.AddUserMessage(second);
        var cut = context.Render<RgfRecrobyComponent>(p => p.Add(c => c.Session, session));

        var messages = cut.FindAll(".rgf-ai-user-copy");
        Assert.Equal(2, messages.Count);
        Assert.Equal(first, messages[0].QuerySelector(".rgf-markdown-plain")!.TextContent);
        Assert.All(messages, message => Assert.Null(message.QuerySelector(".rgf-markdown-toggle, b, strong")));
        messages[1].QuerySelector(".rgf-markdown-copy")!.Click();
        Assert.NotNull(cut.FindAll(".rgf-ai-user-copy")[1].QuerySelector(".bi-check-lg"));
        Assert.Null(cut.FindAll(".rgf-ai-user-copy")[0].QuerySelector(".bi-check-lg"));
        cut.FindAll(".rgf-ai-user-copy")[0].QuerySelector(".rgf-markdown-copy")!.Click();

        Assert.Equal(new[] { second, first }, context.JSInterop.Invocations["navigator.clipboard.writeText"]
            .Select(call => (string)Assert.Single(call.Arguments)!));
        Assert.Equal(new[] { first, "Reply", second }, session.Conversation.Messages.Select(message => message.Message));
        Assert.Null(api.Request);
    }

    [Fact]
    public void RecrobyEnablesConversationCopyByDefault()
    {
        using var context = SelectionContext(out var api);
        var cut = context.Render<RgfRecrobyComponent>();
        var button = cut.Find("[data-action=copy-conversation]");
        Assert.True(button.HasAttribute("disabled"));
        Assert.NotNull(cut.Find(".rgf-ai-conversation-frame").QuerySelector("[data-action=copy-conversation]"));

        cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.AddUserMessage("Recroby question");
        cut.Render();
        cut.Find("[data-action=copy-conversation]").Click();

        var invocation = Assert.Single(context.JSInterop.Invocations["navigator.clipboard.writeText"]);
        Assert.Equal("**User:**\n> Recroby question", Assert.Single(invocation.Arguments));
        Assert.Null(api.Request);
    }

    [Theory]
    [InlineData(RgfAiResponseFormat.PlainText)]
    [InlineData(RgfAiResponseFormat.Minimal)]
    [InlineData(RgfAiResponseFormat.Normal)]
    [InlineData(RgfAiResponseFormat.Maximum)]
    public async Task ResponseFormatSelectionReachesRequestsAndSurvivesReset(RgfAiResponseFormat format)
    {
        using var context = SelectionContext(out var api);
        var cut = context.Render<RgfRecrobyComponent>();
        var state = cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation;
        Assert.Equal(RgfAiResponseFormat.Minimal, state.ResponseFormat);
        Assert.Equal(new[] { "Plain text", "Minimal", "Normal", "Maximum" },
            cut.FindAll("select")[3].QuerySelectorAll("option").Select(option => option.TextContent));
        cut.FindAll("select")[3].Change(format.ToString());
        cut.Find("textarea").Change("Hello");
        await cut.Find("[aria-label=Send]").ClickAsync(new MouseEventArgs());
        Assert.Equal(format, api.Request!.ResponseFormat);
        state.Reset();
        cut.Render();
        Assert.Equal(format, state.ResponseFormat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UsedExternalSessionsKeepNullFormatWithoutCatalog(bool pending)
    {
        using var context = SelectionContext(out var api);
        api.FailNextCatalog = true;
        var session = new RgfAiConversationSession(new SelectionTransport());
        session.Conversation.ApplyResponse(new() { Success = true, ConversationId = "external", ConversationToken = "token",
            WorkflowStatus = pending ? "WaitingForInput" : "Completed" });
        var cut = context.Render<RgfRecrobyComponent>(p => p.Add(c => c.Session, session));
        Assert.Null(session.Conversation.ResponseFormat);
        Assert.Contains("Plain text", cut.Find("[aria-label='Response format']").TextContent);
        Assert.Single(cut.FindAll("select"));
        Assert.Equal(pending, cut.Find("select").HasAttribute("disabled"));
        cut.Find("select").Change("Maximum");
        Assert.Equal(pending ? null : RgfAiResponseFormat.Maximum, session.Conversation.ResponseFormat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingHistoryOrProcessingPreventsMinimalInitialization(bool processing)
    {
        using var context = SelectionContext(out _);
        var transport = new SelectionTransport();
        var session = new RgfAiConversationSession(transport);
        Task<RgfAiResponse>? turn = null;
        if (processing) turn = session.SendAsync("Hidden instruction", false, Xunit.TestContext.Current.CancellationToken);
        else session.Conversation.AddUserMessage("Existing message");
        var cut = context.Render<RgfRecrobyComponent>(p => p.Add(c => c.Session, session));
        Assert.Null(session.Conversation.ResponseFormat);
        Assert.Contains("Plain text", cut.Find("[aria-label='Response format']").TextContent);
        if (turn is not null)
        {
            transport.Response.SetResult(new() { Success = true });
            await turn;
            cut.Render();
            Assert.Null(session.Conversation.ResponseFormat);
        }
    }

    [Fact]
    public void WorkspaceAndSelectorUseGlobalInterop()
    {
        using var context = SelectionContext(out _);
        var importCount = context.JSInterop.Invocations["import"].Count;
        var cut = context.Render<RgfRecrobyWorkspace>();
        cut.Find("[aria-label='Open Recroby']").Click();

        Assert.Single(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Workspace.initialize"]);
        Assert.NotEmpty(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Selector.sync"]);
        Assert.Equal(importCount, context.JSInterop.Invocations["import"].Count);
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
        Assert.Single(first.FindAll("[popovertarget]"));
        Assert.Contains("Minimal", first.Find("[aria-label='Response format']").TextContent);
        Assert.Equal(1, api.CatalogCalls);
        first.Render();
        first.WaitForAssertion(() => Assert.Equal(4, first.FindAll("select").Count));
        Assert.Equal(2, api.CatalogCalls);
        var second = context.Render<RgfRecrobyComponent>();
        Assert.Equal(4, second.FindAll("select").Count);
        Assert.Equal(2, api.CatalogCalls);
    }

    [Fact]
    public void ConcurrentCatalogLookupSurvivesClosingOneConversation()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RgfClientBlazorUiTestState.LoadUiResources(context);
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        var api = new DelayedCatalogApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var first = context.Render<RgfRecrobyComponent>();
        var second = context.Render<RgfRecrobyComponent>();
        Assert.Equal(1, api.CatalogCalls);
        first.Dispose();
        api.Catalog.SetResult(new ApiResponse<RgfAiCatalogResponse> { Success = true,
            Result = new("p", [new("p", "model", [new("model", [], [], null, null)])]) });
        second.WaitForAssertion(() => Assert.Equal(4, second.FindAll("select").Count));
        Assert.Equal(1, api.CatalogCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowCatalogDoesNotPreventSendingWithDefaultSettings(bool malformed)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RgfClientBlazorUiTestState.LoadUiResources(context);
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        var api = new DelayedCatalogApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyComponent>();
        Assert.Single(cut.FindAll("[popovertarget]"));
        cut.Find("textarea").Change("Hello");
        await cut.Find("[aria-label=Send]").ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.Messages.OfType<RgfAiResponse>());
        var renders = cut.RenderCount;
        api.Catalog.SetResult(new ApiResponse<RgfAiCatalogResponse> { Success = true, Result = new("", malformed ? null! : []) });
        cut.WaitForState(() => cut.RenderCount > renders);
        Assert.Single(cut.FindAll("[popovertarget]"));
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
        first.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.ApplyResponse(new()
            { Success = true, Message = "**reply**", ResponseFormat = RgfAiResponseFormat.Normal });
        first.Render();
        first.Find("[aria-label='View plain text']").Click();
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
        Assert.Equal("**reply**", first.Find(".rgf-markdown-plain").TextContent);
        Assert.Contains("q / other", second.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Null(second.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.AiReasoningEffortOverride);
        Assert.Equal(1, api.CatalogCalls);
    }

    [Fact]
    public async Task CatalogSelectionsFollowProviderResetEffortAndReachTheRequest()
    {
        using var context = SelectionContext(out var api);
        var cut = context.Render<RgfRecrobyComponent>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("select").Count));
        var state = cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation;
        void AssertEffort(string display, string? effort)
        {
            Assert.Equal(display + " ▾", cut.Find("button[aria-label='Reasoning effort']").TextContent.Trim());
            Assert.Equal(effort, state.AiReasoningEffortOverride);
            var selected = Assert.Single(cut.FindAll("select")[2].QuerySelectorAll("option[selected]"));
            Assert.Equal(effort ?? string.Empty, selected.GetAttribute("value"));
            Assert.Equal(effort ?? "Default", selected.TextContent);
        }
        async Task AssertSentEffort(string? effort)
        {
            cut.Find("textarea").Change("Hello");
            await cut.Find(".rgf-ai-actions > button").ClickAsync(new MouseEventArgs());
            Assert.NotNull(api.Request);
            Assert.Equal(effort, api.Request.AiReasoningEffortOverride);
        }
        AssertEffort("high", null);
        await AssertSentEffort(null);
        foreach (var (label, iconClass, suffix) in new[] {
            ("Provider / Model", "bi-cpu", "-model"), ("Reasoning effort", "bi-lightbulb", "-effort") })
        {
            var button = cut.Find($"button[aria-label='{label}']");
            Assert.Equal($"bi {iconClass} me-1", button.FirstElementChild!.ClassName);
            Assert.Equal("true", button.FirstElementChild.GetAttribute("aria-hidden"));
            Assert.Empty(button.QuerySelectorAll("[class*=fill]"));
            Assert.EndsWith("▾", button.TextContent.Trim());
            var target = button.GetAttribute("popovertarget")!;
            Assert.EndsWith(suffix, target);
            Assert.Equal(label, cut.Find("#" + target).GetAttribute("aria-label"));
            Assert.Equal("auto", cut.Find("#" + target).GetAttribute("popover"));
        }
        Assert.Contains("p / first", cut.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Equal(new[] { "first", "second" }, cut.FindAll("select")[1].QuerySelectorAll("option").Select(o => o.TextContent));
        Assert.False(cut.FindAll("select")[0].HasAttribute("data-close-popover"));
        Assert.True(cut.FindAll("select")[1].HasAttribute("data-close-popover"));
        Assert.True(cut.FindAll("select")[2].HasAttribute("data-close-popover"));
        cut.FindAll("select")[2].Change("low");
        AssertEffort("low", "low");
        await AssertSentEffort("low");
        cut.FindAll("select")[2].Change(string.Empty);
        AssertEffort("high", null);
        await AssertSentEffort(null);
        cut.FindAll("select")[2].Change("high");
        cut.FindAll("select")[1].Change("second");
        AssertEffort("Default", null);
        Assert.Equal(new[] { "Default", "low" }, cut.FindAll("select")[2].QuerySelectorAll("option").Select(o => o.TextContent));
        cut.FindAll("select")[2].Change("low");
        cut.FindAll("select")[1].Change("first");
        AssertEffort("high", null);
        cut.FindAll("select")[2].Change("low");
        cut.FindAll("select")[0].Change("q");
        AssertEffort("Default", null);
        Assert.Equal(new[] { "other" }, cut.FindAll("select")[1].QuerySelectorAll("option").Select(o => o.TextContent));
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
        AssertEffort("high", null);
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
        cut.FindAll("select")[3].Change("Maximum");
        Task<RgfAiResponse>? send = null;
        await cut.InvokeAsync(() => { send = first.SendAsync("Hello"); });
        cut.WaitForAssertion(() => Assert.All(cut.FindAll("select, [popovertarget]"), item => Assert.True(item.HasAttribute("disabled"))));
        transport.Response.SetResult(new() { Success = true, WorkflowStatus = "WaitingForInput" });
        await send!;
        cut.WaitForAssertion(() => Assert.All(cut.FindAll("select, [popovertarget]"), item => Assert.True(item.HasAttribute("disabled"))));
        cut.FindAll("select")[0].Change("q");
        Assert.Equal("p/first", first.Conversation.AiModelOverride);
        cut.FindAll("select")[3].Change("PlainText");
        Assert.Equal(RgfAiResponseFormat.Maximum, first.Conversation.ResponseFormat);
        cut.Render(p => p.Add(c => c.Session, second));
        Assert.All(cut.FindAll("select, [popovertarget]"), item => Assert.False(item.HasAttribute("disabled")));
        cut.FindAll("select")[0].Change("q");
        cut.FindAll("select")[3].Change("Normal");
        cut.Render(p => p.Add(c => c.Session, first));
        Assert.Contains("p / first", cut.Find("[aria-label='Provider / Model']").TextContent);
        Assert.Equal("high", first.Conversation.AiReasoningEffortOverride);
        Assert.Equal("q/other", second.Conversation.AiModelOverride);
        Assert.Equal(RgfAiResponseFormat.Maximum, first.Conversation.ResponseFormat);
        Assert.Equal(RgfAiResponseFormat.Normal, second.Conversation.ResponseFormat);
    }

    private static BunitContext SelectionContext(out CatalogApi api)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RgfClientBlazorUiTestState.LoadUiResources(context);
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
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
        RgfClientBlazorUiTestState.LoadUiResources(context);
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        context.Services.AddSingleton<IRgfApiService>(new ProviderMissingApi());
        var cut = context.Render<RgfRecrobyComponent>();
        Assert.Single(cut.FindAll("[popovertarget]"));
        cut.Find("textarea").Change("Hello");
        await cut.Find("[aria-label=Send]").ClickAsync(new MouseEventArgs());
        Assert.Equal("No AI provider is configured. Configure an AI provider and model to use Recroby.",
            cut.Find("[data-error-kind=configuration]").TextContent);
        var state = cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation;
        Assert.Empty(state.Messages);
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
        RgfClientBlazorUiTestState.LoadUiResources(context);
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
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

        await cut.Find("[aria-label=Send]").ClickAsync(new MouseEventArgs());

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
