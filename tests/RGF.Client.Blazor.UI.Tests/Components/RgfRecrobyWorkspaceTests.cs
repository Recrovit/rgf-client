using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class RgfRecrobyWorkspaceTests
{
    // Resource loading is independently covered by configuration tests.
    private sealed class RootWithoutResourceLoading : Recrovit.RecroGridFramework.Client.Blazor.UI.Components.RgfRootComponent
    {
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RootKeepsChildContentAndIndependentToast(bool enabled)
    {
        using var context = Context();
        context.ComponentFactories.AddStub<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ToastComponent>();
        var cut = context.Render<RootWithoutResourceLoading>(p =>
        {
            if (!enabled) p.Add(c => c.EnableRecroby, false);
            p.Add(c => c.ChildContent, "<main>Host workspace</main>");
        });
        Assert.Equal("Host workspace", cut.Find("main").TextContent);
        Assert.Equal(enabled ? 1 : 0, cut.FindComponents<RgfRecrobyWorkspace>().Count);
        Assert.Single(cut.FindComponents<Bunit.TestDoubles.Stub<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ToastComponent>>());
    }
    private static BunitContext Context()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        return context;
    }

    [Fact]
    public void AllDockModesRetainSessionsAndHostContent()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>(p => p.Add(c => c.ChildContent, "<main>Host</main>"));
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        Assert.Single(cut.FindAll("header strong > .bi-robot[aria-hidden=true]"));
        Assert.Single(cut.FindAll(".rgf-recroby-launcher > .bi-robot[aria-hidden=true]"));
        Assert.Equal(string.Empty, cut.Find("[data-action=new]").TextContent.Trim());
        Assert.Equal("New conversation", cut.Find("[data-action=new]").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll(".rgf-recroby-conversation-bar [data-action=new]"));
        Assert.Empty(cut.FindAll("[role=tab], .nav-tabs, select"));
        Assert.Single(cut.FindAll("[data-action=switch]"));
        Assert.Equal("New chat", cut.Find("[data-action=switch]").GetAttribute("title"));
        Assert.Equal("dialog", cut.Find("[data-action=switch]").GetAttribute("aria-haspopup"));
        Assert.Empty(cut.FindAll("header [data-action=new]"));
        Assert.Single(cut.FindAll(".rgf-recroby-choice > .btn-close"));
        var layoutToggle = cut.Find(".rgf-recroby-desktop-controls .dropdown-toggle");
        Assert.Equal(string.Empty, layoutToggle.TextContent.Trim());
        Assert.Single(layoutToggle.QuerySelectorAll("i.bi"));
        Assert.Equal(4, cut.FindAll(".rgf-recroby-desktop-controls .dropdown-menu [data-dock]").Count);
        foreach (var mode in new[] { "DockLeft", "DockRight", "DockBottom", "Floating" })
        {
            var button = cut.Find($"[data-dock={mode}]");
            Assert.NotEmpty(button.GetAttribute("aria-label")!);
            Assert.Single(button.QuerySelectorAll("i.bi[aria-hidden=true]"));
            button.Click();
            Assert.Equal("true", cut.Find($"[data-dock={mode}]").GetAttribute("aria-pressed"));
            Assert.Equal(mode, cut.Find(".rgf-recroby-workspace").GetAttribute("data-mode"));
            Assert.Same(session, cut.FindComponent<RgfAiChatComponent>().Instance.Session);
            Assert.Equal("Host", cut.Find("main").TextContent);
        }
    }

    [Theory]
    [InlineData("DockLeft")]
    [InlineData("DockRight")]
    [InlineData("DockBottom")]
    [InlineData("Floating")]
    public void CollapseReopenPreservesModeSessionAndDraft(string mode)
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        Assert.True(cut.Find("section").HasAttribute("hidden"));
        cut.Find("[aria-label='Open Recroby']").Click();
        cut.Find($"[data-dock={mode}]").Click();
        var chat = cut.FindComponent<RgfAiChatComponent>().Instance;
        cut.Find("textarea").Change("Draft");
        cut.Find("[data-action=switch]").Click();
        cut.Find("[aria-label='Collapse Recroby']").Click();
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        Assert.True(cut.Find("section").HasAttribute("hidden"));
        cut.Find("[aria-label='Open Recroby']").Click();
        Assert.False(cut.Find("section").HasAttribute("hidden"));
        Assert.Equal(mode, cut.Find(".rgf-recroby-workspace").GetAttribute("data-mode"));
        Assert.Same(chat, cut.FindComponent<RgfAiChatComponent>().Instance);
        Assert.Equal("Draft", cut.FindComponent<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base.RgfInputText>().Instance.Value);
    }

    [Fact]
    public void SwitcherRetainsIndependentSessionsAndDraftsWithoutBackend()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var first = cut.FindComponent<RgfAiChatComponent>();
        cut.Find("textarea").Change("Draft one");
        cut.Find("[data-action=new]").Click();
        var chats = cut.FindComponents<RgfAiChatComponent>();
        Assert.Equal(2, chats.Count);
        Assert.NotSame(first.Instance.Session, chats[1].Instance.Session);
        chats[1].Find("textarea").Change("Draft two");
        Assert.Equal(2, cut.FindAll("[data-action=select]").Count);
        Assert.Single(cut.FindAll(".rgf-recroby-conversation-bar > [data-action=new]"));
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        Assert.Empty(cut.FindAll("button button"));
        cut.Find("[data-action=switch]").Click();
        cut.FindAll("[data-action=select]")[0].Click();
        Assert.Equal("false", cut.Find("[data-action=switch]").GetAttribute("aria-expanded"));
        Assert.Equal("Draft one", cut.FindAll("textarea")[0].GetAttribute("value") ?? cut.FindAll("textarea")[0].TextContent);
        Assert.Same(first.Instance, cut.FindComponents<RgfAiChatComponent>()[0].Instance);
        Assert.Equal("Draft two", cut.FindComponents<RgfAiChatComponent>()[1].FindComponent<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base.RgfInputText>().Instance.Value);
        Assert.Equal("New chat", cut.Find(".rgf-recroby-conversation:not([hidden])").GetAttribute("aria-label"));
        cut.Find("[data-action=switch]").Click();
        cut.FindAll(".rgf-recroby-choice > .btn-close")[0].Click();
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
    }

    [Fact]
    public async Task ExternalSessionsSupportConcurrentTurnsAndSurviveConversationSwitch()
    {
        using var context = Context();
        var a = new PendingTransport();
        var b = new PendingTransport();
        var sa = new RgfAiConversationSession(a);
        var sb = new RgfAiConversationSession(b);
        var ca = context.Render<RgfRecrobyComponent>(p => p.Add(c => c.Session, sa));
        var cb = context.Render<RgfRecrobyComponent>(p => p.Add(c => c.Session, sb));
        var ta = sa.SendAsync("A", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        var tb = sb.SendAsync("B", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.True(sa.IsProcessing);
        Assert.True(sb.IsProcessing);
        a.Complete("Answer A");
        await ta;
        Assert.False(sa.IsProcessing);
        Assert.True(sb.IsProcessing);
        b.Complete("Answer B");
        await tb;
        ca.WaitForAssertion(() => Assert.Contains("Answer A", ca.Markup));
        cb.WaitForAssertion(() => Assert.Contains("Answer B", cb.Markup));
        Assert.DoesNotContain("Answer B", ca.Markup);
    }

    [Fact]
    public async Task MissingBackendOnlyFailsOnSendAndDisplaysTransportError()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        Assert.Empty(cut.FindAll("[role=alert]"));
        cut.Find("[aria-label='Open Recroby']").Click();
        cut.Find("textarea").Change("Hello");
        await cut.FindComponent<RgfAiChatComponent>().Find("button").ClickAsync(new());
        Assert.Equal("Hello", cut.Find(".rgf-recroby-title").TextContent);
        Assert.Single(cut.FindAll("[data-error-kind=transport]"));
        Assert.False(cut.FindComponent<RgfAiChatComponent>().Instance.Session.IsProcessing);
    }

    [Theory]
    [InlineData(false, "transport")]
    [InlineData(true, "workflow")]
    public async Task ApiAndWorkflowFailuresStayInTheirOwnConversation(bool envelopeSuccess, string errorKind)
    {
        using var context = Context();
        context.Services.AddSingleton<IRgfApiService>(new FailedApi(envelopeSuccess));
        var cut = context.Render<RgfRecrobyWorkspace>();
        var first = cut.FindComponent<RgfAiChatComponent>();
        first.Find("textarea").Change("Hello");
        await first.Find("button").ClickAsync(new());
        cut.Find("[data-action=new]").Click();
        Assert.Single(first.FindAll($"[data-error-kind={errorKind}]"));
        Assert.Empty(cut.FindComponents<RgfAiChatComponent>()[1].FindAll("[role=alert]"));
    }

    [Fact]
    public async Task WorkspaceRequestsContinueWhileCollapsedAndCanBeCancelledOrClosed()
    {
        using var context = Context();
        var api = new QueuedApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var first = cut.FindComponent<RgfAiChatComponent>();
        first.Find("textarea").Change("First");
        var firstClick = first.Find("button").ClickAsync(new());
        cut.Find("[data-action=new]").Click();
        var second = cut.FindComponents<RgfAiChatComponent>()[1];
        second.Find("textarea").Change("Second");
        var secondClick = second.Find("button").ClickAsync(new());
        Assert.Equal(2, api.Requests.Count);
        Assert.Equal(new[] { "First", "Second" }, cut.FindAll(".rgf-recroby-choice-title").Select(e => e.TextContent));
        cut.Find("[data-action=switch]").Click();
        cut.FindAll("[data-action=select]")[0].Click();
        Assert.Same(first.Instance.Session, cut.FindComponents<RgfAiChatComponent>()[0].Instance.Session);
        Assert.True(first.Instance.Session.IsProcessing);
        Assert.True(second.Instance.Session.IsProcessing);
        cut.Find("[data-dock=DockBottom]").Click();
        Assert.Equal("First", cut.Find(".rgf-recroby-title").TextContent);
        cut.Find("[aria-label='Collapse Recroby']").Click();
        Assert.True(first.Instance.Session.IsProcessing);
        Assert.True(second.Instance.Session.IsProcessing);
        cut.Find("[aria-label='Open Recroby']").Click();
        cut.Find("[data-action=switch]").Click();
        cut.FindAll("[data-action=select]")[0].Click();
        Assert.Equal("false", cut.Find("[data-action=switch]").GetAttribute("aria-expanded"));
        first.Find("[aria-label='Cancel request']").Click();
        await firstClick;
        first.WaitForAssertion(() => Assert.Single(first.FindAll("[data-error-kind=cancelled]")));
        Assert.True(second.Instance.Session.IsProcessing);
        cut.Find("[data-action=switch]").Click();
        cut.Find("[aria-label='Close Second']").Click();
        await secondClick;
        Assert.All(api.Requests, request => Assert.True(request.CancellationToken.IsCancellationRequested));
    }

    [Fact]
    public async Task SwitcherToggleEscapeAndOutsideDismissPreserveSelection()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        cut.Find("[data-action=switch]").Click();
        Assert.Equal("true", cut.Find("[data-action=switch]").GetAttribute("aria-expanded"));
        Assert.False(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        Assert.Equal(cut.Find(".rgf-recroby-switcher").Id, cut.Find("[data-action=switch]").GetAttribute("aria-controls"));
        cut.Find("[data-action=switch]").Click();
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        cut.Find("[data-action=switch]").Click();
        cut.Find(".rgf-recroby-conversation-bar").KeyDown("Escape");
        Assert.Equal("false", cut.Find("[data-action=switch]").GetAttribute("aria-expanded"));
        cut.Find("[data-action=switch]").Click();
        await cut.InvokeAsync(() => cut.Instance.DismissSwitcher(false));
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        Assert.Same(session, cut.FindComponent<RgfAiChatComponent>().Instance.Session);
    }

    [Fact]
    public void ClosingInactiveActiveAndFinalConversationsKeepsFallback()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        cut.Find("[data-action=new]").Click();
        cut.Find("[data-action=new]").Click();
        var active = cut.FindComponents<RgfAiChatComponent>()[2].Instance;
        cut.Find("[data-action=switch]").Click();
        cut.FindAll(".rgf-recroby-choice > .btn-close")[1].Click();
        Assert.Same(active, cut.FindComponents<RgfAiChatComponent>()[1].Instance);
        Assert.Equal("true", cut.Find("[data-action=switch]").GetAttribute("aria-expanded"));
        cut.FindAll(".rgf-recroby-choice > .btn-close")[1].Click();
        Assert.Equal("true", cut.Find("[data-action=select]").GetAttribute("aria-pressed"));
        cut.Find(".rgf-recroby-choice > .btn-close").Click();
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        Assert.Empty(cut.FindAll(".rgf-recroby-type, .rgf-recroby-group-title"));
        Assert.NotSame(active.Session, cut.FindComponent<RgfAiChatComponent>().Instance.Session);
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
    }

    [Fact]
    public void ManyConversationsAreGroupedAndSearchTitlesIgnoringCase()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        Rename(cut, "Conversation 1");
        for (var i = 1; i < 8; i++)
        {
            cut.Find("[data-action=new]").Click();
            Rename(cut, $"Conversation {i + 1}");
        }
        cut.Find("[data-action=switch]").Click();
        Assert.Empty(cut.FindAll("input[type=search]"));
        cut.Find("[data-action=new]").Click();
        cut.Find("[data-action=switch]").Click();
        Assert.Equal(9, cut.FindAll("[data-conversation-type=general] [data-action=select]").Count);
        Assert.Single(cut.FindAll("[role=group]"));
        cut.Find("input[type=search]").Input("cONVERSATION 2");
        Assert.Single(cut.FindAll("[data-action=select]"));
        Assert.Equal("Conversation 2", cut.Find(".rgf-recroby-choice-title").TextContent);
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        cut.Find("input[type=search]").Input("missing");
        Assert.Empty(cut.FindAll("[data-action=select]"));
        Assert.Equal("No conversations found.", cut.Find("[role=status]").TextContent);
        cut.Find("input[type=search]").Input("CONVERSATION 2");
        cut.Find("[data-action=select]").Click();
        Assert.Equal("Conversation 2", cut.Find(".rgf-recroby-title").TextContent);
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        cut.Find("[data-action=switch]").Click();
        Assert.Equal(string.Empty, cut.Find("input[type=search]").GetAttribute("value"));
        Assert.Equal(9, cut.FindAll("[data-action=select]").Count);
    }

    [Fact]
    public void LongTitlesAndUnknownTypesUseTheSameSwitcher()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        cut.Find("[data-action=new]").Click();
        // Supply future conversation metadata without introducing a public opening API.
        var conversations = (System.Collections.IEnumerable)typeof(RgfRecrobyWorkspace)
            .GetField("_conversations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(cut.Instance)!;
        var conversation = conversations.Cast<object>().Last();
        var title = string.Concat(Enumerable.Repeat("Very long conversation title ", 20));
        conversation.GetType().GetProperty("Title")!.SetValue(conversation, title);
        conversation.GetType().GetField("<RecrobyType>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(conversation, "custom");
        cut.Find("[data-action=switch]").Click();
        Assert.Equal(2, cut.FindAll("[role=group]").Count);
        Assert.Equal("General chat", cut.Find("[data-conversation-type=general]").GetAttribute("aria-label"));
        Assert.Equal("General chat", cut.Find("[data-conversation-type=general] .rgf-recroby-group-title").TextContent);
        Assert.Single(cut.FindAll("[data-conversation-type=general] [data-action=select]"));
        Assert.Equal(title, cut.Find("[data-conversation-type=custom] [data-action=select]").GetAttribute("title"));
        Assert.Equal("custom: " + title, cut.Find("[data-action=switch]").GetAttribute("title"));
        Assert.Equal(title, cut.Find(".rgf-recroby-title").TextContent);
        Assert.Equal("custom: " + title, cut.Find(".rgf-recroby-conversation:not([hidden])").GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("[role=tab], [role=tabpanel], select"));
    }

    private static void Rename(IRenderedComponent<RgfRecrobyWorkspace> cut, string title)
    {
        cut.Find("[data-action=rename]").Click();
        cut.Find(".rgf-recroby-title-editor").Input(title);
        cut.Find(".rgf-recroby-title-editor").KeyDown("Enter");
    }

    [Theory]
    [InlineData("12345678901234567890123456789", "12345678901234567890123456789")]
    [InlineData("123456789012345678901234567890", "123456789012345678901234567890")]
    [InlineData("1234567890123456789012345678901", "12345678901234567890123456789…")]
    [InlineData("  Hello\r\n  world\t again  ", "Hello world again")]
    [InlineData("1234567890123456789012345678👩🏽‍💻XY", "1234567890123456789012345678👩🏽‍💻…")]
    [InlineData("1234567890123456789012345678e\u0301XY", "1234567890123456789012345678e\u0301…")]
    public async Task FirstDisplayedPromptTitlesBeforeCompletionAndSurvivesCancellation(string prompt, string title)
    {
        using var context = Context();
        context.Services.AddSingleton<IRgfApiService>(new QueuedApi());
        var cut = context.Render<RgfRecrobyWorkspace>();
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Xunit.TestContext.Current.CancellationToken);
        var send = session.SendAsync(prompt, cancellationToken: cancellation.Token);
        cut.WaitForAssertion(() => Assert.Equal(title, cut.Find(".rgf-recroby-title").TextContent));
        Assert.False(send.IsCompleted);
        Assert.Equal(title, cut.Find("[data-action=switch]").GetAttribute("title"));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        cut.WaitForAssertion(() => Assert.Equal(title, cut.Find(".rgf-recroby-title").TextContent));
    }

    [Fact]
    public async Task HiddenSendsDoNotTitleAndLaterFailuresCannotRetitle()
    {
        using var context = Context();
        context.Services.AddSingleton<IRgfApiService>(new FailedApi(true));
        var cut = context.Render<RgfRecrobyWorkspace>();
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        await session.SendAsync("Hidden", false, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        await session.SendAsync("Visible", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal("Visible", cut.Find(".rgf-recroby-title").TextContent));
        await session.SendAsync("Later", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal("Visible", cut.Find(".rgf-recroby-title").TextContent));
    }

    [Fact]
    public async Task RenameSavesOnlyValidEnterAndPreventsAutomaticTitleEvenForNewChat()
    {
        using var context = Context();
        context.Services.AddSingleton<IRgfApiService>(new FailedApi(true));
        var module = context.JSInterop.SetupModule(invocation => invocation.Arguments[0]?.ToString()?.EndsWith("RgfRecrobyWorkspace.razor.js") == true);
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<RgfRecrobyWorkspace>();
        cut.Find("[data-action=switch]").Click();
        cut.Find("[data-action=rename]").Click();
        Assert.Single(module.Invocations["focusTitleEditor"]);
        Assert.Equal(false, module.Invocations["syncSwitcher"].Last().Arguments[1]);
        Assert.Equal(false, module.Invocations["syncSwitcher"].Last().Arguments[2]);
        Assert.Empty(cut.FindAll("[data-action=switch]"));
        Assert.True(cut.Find(".rgf-recroby-switcher").HasAttribute("hidden"));
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title-editor").GetAttribute("value"));
        Assert.Equal("100", cut.Find(".rgf-recroby-title-editor").GetAttribute("maxlength"));
        cut.Find(".rgf-recroby-title-editor").Input("  ");
        cut.Find(".rgf-recroby-title-editor").KeyDown("Enter");
        Assert.Single(cut.FindAll(".rgf-recroby-title-editor"));
        cut.Find(".rgf-recroby-title-editor").Input(new string('x', 101));
        cut.Find(".rgf-recroby-title-editor").KeyDown("Enter");
        Assert.Single(cut.FindAll(".rgf-recroby-title-editor"));
        cut.Find(".rgf-recroby-title-editor").Input("Discard");
        cut.Find(".rgf-recroby-title-editor").KeyDown("Escape");
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        cut.Find("[data-action=rename]").Click();
        cut.Find(".rgf-recroby-title-editor").Input("Discard on blur");
        cut.Find(".rgf-recroby-title-editor").Blur();
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        var longTitle = new string('x', 100);
        Rename(cut, longTitle);
        Assert.Equal(longTitle, cut.Find(".rgf-recroby-choice-title").TextContent);
        Assert.Equal(longTitle, cut.Find("[data-action=switch]").GetAttribute("title"));
        Assert.Equal("Rename " + longTitle, cut.Find("[data-action=rename]").GetAttribute("aria-label"));
        Rename(cut, "New chat");
        await cut.FindComponent<RgfAiChatComponent>().Instance.Session.SendAsync("Must not replace manual title", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
    }

    [Fact]
    public async Task PendingCompletionCannotReplaceManualTitleAndClosedSessionsAreIgnored()
    {
        using var context = Context();
        context.Services.AddSingleton<IRgfApiService>(new QueuedApi());
        var cut = context.Render<RgfRecrobyWorkspace>();
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Xunit.TestContext.Current.CancellationToken);
        var send = session.SendAsync("Automatic", cancellationToken: cancellation.Token);
        cut.WaitForAssertion(() => Assert.Equal("Automatic", cut.Find(".rgf-recroby-title").TextContent));
        Rename(cut, "Manual");
        var stateChanged = typeof(RgfAiConversationSession).GetField("StateChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var handler = ((Action)stateChanged.GetValue(session)!).GetInvocationList()
            .Single(d => d.Target?.GetType().DeclaringType == typeof(RgfRecrobyWorkspace));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.Equal("Manual", cut.Find(".rgf-recroby-title").TextContent);
        cut.Find("[data-action=switch]").Click();
        cut.Find(".rgf-recroby-choice > .btn-close").Click();
        Assert.DoesNotContain(handler, ((Action?)stateChanged.GetValue(session))?.GetInvocationList() ?? []);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SendAsync("Closed", cancellationToken: cancellation.Token));
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        var remaining = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var remainingHandler = ((Action)stateChanged.GetValue(remaining)!).GetInvocationList()
            .Single(d => d.Target?.GetType().DeclaringType == typeof(RgfRecrobyWorkspace));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        Assert.DoesNotContain(remainingHandler, ((Action?)stateChanged.GetValue(remaining))?.GetInvocationList() ?? []);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => remaining.SendAsync("Disposed", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task QueuedStateChangeRechecksManualTitleOnDispatcher()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var stateChanged = typeof(RgfAiConversationSession).GetField("StateChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var handler = (Action)((Action)stateChanged.GetValue(session)!).GetInvocationList()
            .Single(d => d.Target?.GetType().DeclaringType == typeof(RgfRecrobyWorkspace));
        await cut.InvokeAsync(() =>
        {
            session.Conversation.AddUserMessage("Automatic title waiting for dispatcher");
            // Queue the event behind the current dispatcher operation, then save the manual title.
            Task.Run(handler, Xunit.TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            Rename(cut, "New chat");
        });
        await cut.InvokeAsync(() => { });
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
    }

    [Fact]
    public void SearchDoesNotChangeTheFullCollectionTypeCount()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        for (var i = 1; i < 9; i++) cut.Find("[data-action=new]").Click();
        Rename(cut, "Unique title");
        var conversations = (System.Collections.IEnumerable)typeof(RgfRecrobyWorkspace)
            .GetField("_conversations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(cut.Instance)!;
        var conversation = conversations.Cast<object>().Last();
        conversation.GetType().GetField("<RecrobyType>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(conversation, "custom");
        cut.Find("[data-action=switch]").Click();
        cut.Find("input[type=search]").Input("Unique");
        Assert.Single(cut.FindAll("[data-action=select]"));
        Assert.Equal("custom", cut.Find(".rgf-recroby-group-title").TextContent);
        cut.Find("input[type=search]").Input("New chat");
        Assert.Equal("General chat", cut.Find(".rgf-recroby-group-title").TextContent);
        Assert.DoesNotContain("general:", cut.Markup);
        Assert.Equal("custom", cut.Find(".rgf-recroby-type").TextContent);
    }

    private sealed class FailedApi(bool envelopeSuccess) : IRgfApiService
    {
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class =>
            Task.FromResult((IRgfApiResponse<T>)(object)new ApiResponse<RgfAiResponse>
            { Success = envelopeSuccess, Result = new() { Success = false, Message = "Workflow rejected" } });
    }

    private sealed class QueuedApi : IRgfApiService
    {
        public List<IRgfApiRequest> Requests { get; } = [];
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Requests.Add(request);
            await Task.Delay(Timeout.Infinite, request.CancellationToken);
            throw new InvalidOperationException("Only cancellation completes this fake.");
        }
    }

    private sealed class PendingTransport : IRgfAiTransport
    {
        private readonly TaskCompletionSource<RgfAiResponse> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default) => completion.Task;
        public void Complete(string message) => completion.SetResult(new() { Success = true, Message = message });
    }
}
