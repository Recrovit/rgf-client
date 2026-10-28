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
    [Fact]
    public void GridContextNotifiesOnlyAfterActualStateChanges()
    {
        var grid = new RgfRecrobyGridContext();
        var manager = CreateGridManager();
        var replacement = CreateGridManager();
        var states = new List<(object? Manager, string Entity, string Display)>();
        grid.Changed += () => states.Add((grid.Manager, grid.EntityName, grid.DisplayName));

        grid.ClearManager(manager);
        Assert.Empty(states);
        grid.SetManager(manager);
        Assert.Equal((manager, "Orders", "Orders"), Assert.Single(states));
        grid.SetManager(manager);
        grid.ClearManager(replacement);
        Assert.Single(states);

        manager.EntityDesc.Title = "Customer orders";
        grid.SetManager(manager);
        Assert.Equal((manager, "Orders", "Customer orders"), states[1]);
        manager.EntityDesc.EntityName = "ArchivedOrders";
        grid.SetManager(manager);
        Assert.Equal((manager, "ArchivedOrders", "Customer orders"), states[2]);
        grid.SetManager(replacement);
        Assert.Equal((replacement, "Orders", "Orders"), states[3]);
        grid.ClearManager(manager);
        grid.SetManager(replacement);
        Assert.Equal(4, states.Count);

        grid.ClearManager(replacement);
        Assert.Equal(((object?)null, "Orders", "Orders"), states[4]);
        grid.ClearManager(replacement);
        Assert.Equal(5, states.Count);
    }

    [Fact]
    public async Task SharedGridContextUpdatesLabelsAutomaticallyUntilLastConversationCloses()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        AddHistoryToActive(cut);
        var grid = new RgfRecrobyGridContext();
        var manager = CreateGridManager();
        grid.SetManager(manager);
        await context.Services.GetRequiredService<RgfRecrobyWorkspaceService>().OpenOrActivateGridAsync(grid);
        AddHistoryToActive(cut);
        cut.Find("[data-action=new]").Click();
        Assert.Single(GridContextHandlers(grid));

        await Task.Run(() => grid.ClearManager(manager), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Orders (unavailable)", cut.Find(".rgf-recroby-context").TextContent);
            Assert.Equal(2, cut.FindAll("[data-action=select]").Count(e => e.TextContent.Contains("Orders (unavailable)")));
        });
        await Task.Run(() => grid.SetManager(manager), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Orders", cut.Find(".rgf-recroby-context").TextContent);
            Assert.DoesNotContain("unavailable", cut.Markup);
        });

        await cut.FindAll(".rgf-recroby-choice > .btn-close")[1].ClickAsync(new());
        Assert.Single(GridContextHandlers(grid));
        await Task.Run(() => grid.ClearManager(manager), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal("Orders (unavailable)", cut.Find(".rgf-recroby-context").TextContent));
        await Task.Run(() => grid.SetManager(manager), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal("Orders", cut.Find(".rgf-recroby-context").TextContent));

        await cut.FindAll(".rgf-recroby-choice > .btn-close")[1].ClickAsync(new());
        Assert.Empty(GridContextHandlers(grid));
        var renderCount = cut.RenderCount;
        grid.ClearManager(manager);
        await cut.InvokeAsync(() => { });
        Assert.Equal(renderCount, cut.RenderCount);
        Assert.Equal("General chat", cut.Find(".rgf-recroby-context").TextContent);
    }

    [Fact]
    public async Task WorkspaceDetachesAllGridContextsBeforeAsyncComponentCleanup()
    {
        using var context = Context();
        var module = context.JSInterop.SetupModule(invocation => invocation.Arguments[0]?.ToString()?.EndsWith("RgfRecrobyComponent.razor.js") == true);
        module.Mode = JSRuntimeMode.Loose;
        var cleanup = module.SetupVoid("dispose", _ => true);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var grids = new[] { new RgfRecrobyGridContext(), new RgfRecrobyGridContext() };
        foreach (var grid in grids)
        {
            await context.Services.GetRequiredService<RgfRecrobyWorkspaceService>().OpenOrActivateGridAsync(grid);
            Assert.Single(GridContextHandlers(grid));
            AddHistoryToActive(cut);
        }

        var disposal = cut.Instance.DisposeAsync().AsTask();
        try
        {
            cut.WaitForAssertion(() => Assert.NotEmpty(module.Invocations["dispose"]));
            Assert.False(disposal.IsCompleted);
            Assert.All(grids, grid => Assert.Empty(GridContextHandlers(grid)));
        }
        finally { cleanup.SetVoidResult(); }
        await disposal;
    }

    [Fact]
    public async Task QueuedGridContextChangeDoesNotRenderDisposedWorkspace()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var grid = new RgfRecrobyGridContext();
        var manager = CreateGridManager();
        grid.SetManager(manager);
        await context.Services.GetRequiredService<RgfRecrobyWorkspaceService>().OpenOrActivateGridAsync(grid);
        SynchronizationContext? dispatcherContext = null;
        await cut.InvokeAsync(() => dispatcherContext = SynchronizationContext.Current);
        Assert.NotNull(dispatcherContext);
        using var releaseDispatcher = new ManualResetEventSlim();
        var dispatcherBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderCount = 0;
        Task disposal;
        // Post keeps the dispatcher queue occupied until the test explicitly releases it.
        dispatcherContext.Post(_ =>
        {
            dispatcherBlocked.SetResult();
            releaseDispatcher.Wait();
        }, null);
        try
        {
            await dispatcherBlocked.Task.WaitAsync(Xunit.TestContext.Current.CancellationToken);
            renderCount = cut.RenderCount;
            grid.ClearManager(manager);
            Assert.Equal(renderCount, cut.RenderCount);
            disposal = cut.Instance.DisposeAsync().AsTask();
            Assert.Empty(GridContextHandlers(grid));
        }
        finally { releaseDispatcher.Set(); }
        await disposal;
        await cut.InvokeAsync(() => { });
        Assert.Equal(renderCount, cut.RenderCount);
        Assert.Empty(GridContextHandlers(grid));
    }

    private static Delegate[] GridContextHandlers(RgfRecrobyGridContext grid) =>
        ((Action?)typeof(RgfRecrobyGridContext).GetField("Changed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(grid))?.GetInvocationList() ?? [];

    private static Recrovit.RecroGridFramework.Client.Handlers.IRgManager CreateGridManager() =>
        System.Reflection.DispatchProxy.Create<Recrovit.RecroGridFramework.Client.Handlers.IRgManager, GridManagerProxy>();

    public class GridManagerProxy : System.Reflection.DispatchProxy
    {
        private readonly Recrovit.RecroGridFramework.Abstraction.Models.RgfEntity _entity = new() { EntityName = "Orders", Title = "Orders" };
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_EntityDesc" ? _entity : throw new NotSupportedException(targetMethod?.Name);
    }

    [Fact]
    public async Task GridChatsReuseReferenceIdentityAndInheritOnlyContext()
    {
        using var context = Context();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var cut = context.Render<RgfRecrobyWorkspace>();
        Assert.Equal("DockRight", cut.Find(".rgf-recroby-workspace").GetAttribute("data-mode"));
        Assert.True(cut.Find("section").HasAttribute("hidden"));
        Assert.Equal("General chat", cut.Find(".rgf-recroby-context").TextContent);
        Assert.True(cut.Find("[data-new-chat]").HasAttribute("disabled"));
        AddHistoryToActive(cut);
        var grid = new RgfRecrobyGridContext();
        Assert.True(await service.OpenOrActivateGridAsync(grid));
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        var first = cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session;
        Assert.False(cut.Find("section").HasAttribute("hidden"));
        Assert.Equal("DockRight", cut.Find(".rgf-recroby-workspace").GetAttribute("data-mode"));
        Assert.True(await service.OpenOrActivateGridAsync(grid));
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.Same(first, cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session);
        first.Conversation.AddUserMessage("Private history");
        first.Conversation.ApplyResponse(new() { Success = true, ConversationId = "private-id", ConversationToken = "private-token",
            WorkflowStatus = "WaitingForInput", ResponseMode = RgfAiResponseMode.ChoiceOnly,
            Choices = [new() { Code = "private", UserText = "Private choice" }], Message = "Pending" });
        AddHistoryToActive(cut);
        cut.Find("[data-action=new]").Click();
        var second = cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session;
        Assert.NotSame(first, second);
        Assert.Empty(second.Conversation.Messages);
        Assert.Null(second.Conversation.ConversationId);
        Assert.Null(second.Conversation.ConversationToken);
        Assert.False(second.Conversation.IsPendingWorkflow);
        Assert.False(second.IsProcessing);
        Assert.True(await service.OpenOrActivateGridAsync(grid));
        Assert.Equal("true", cut.FindAll("[data-action=select]")[2].GetAttribute("aria-pressed"));
        AddHistoryToActive(cut);
        cut.FindAll("[data-new-chat]")[1].Click();
        Assert.Equal("General chat", cut.Find(".rgf-recroby-context").TextContent);
        AddHistoryToActive(cut);
        Assert.True(await service.OpenOrActivateGridAsync(grid));
        Assert.Equal("true", cut.FindAll("[data-action=select]")[1].GetAttribute("aria-pressed"));
        AddHistoryToActive(cut);
        var otherGrid = new RgfRecrobyGridContext();
        Assert.True(await service.OpenOrActivateGridAsync(otherGrid));
        Assert.Equal(5, cut.FindComponents<RgfAiChatComponent>().Count);
        AddHistoryToActive(cut);
        cut.FindAll("[data-new-chat]")[0].Click();
        Assert.Equal(6, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.Contains("unavailable", cut.Find(".rgf-recroby-context").TextContent);
        while (cut.FindComponents<RgfAiChatComponent>().Count > 1)
            await cut.FindAll(".rgf-recroby-choice > .btn-close")[0].ClickAsync(new());
        await cut.Find(".rgf-recroby-choice > .btn-close").ClickAsync(new());
        Assert.Equal("General chat", cut.Find(".rgf-recroby-context").TextContent);
        Assert.Empty(cut.FindComponent<RgfAiChatComponent>().Instance.Session.Conversation.Messages);
    }

    [Fact]
    public async Task GridOpenRequiresUniqueGridAdapterAndRechecksQueuedDispatch()
    {
        var service = new RgfRecrobyWorkspaceService();
        var grid = new RgfRecrobyGridContext();
        Assert.False(service.CanOpenGrid);
        Assert.False(await service.OpenOrActivateGridAsync(grid));
        using (service.RegisterWorkspace(op => Task.FromResult(op()), (_, _, _, _, _) => true, _ => Task.CompletedTask))
            Assert.False(await service.OpenOrActivateGridAsync(grid));
        Func<bool>? queued = null;
        var completion = new TaskCompletionSource<bool>();
        var calls = 0;
        using var registration = service.RegisterWorkspace(op => { queued = op; return completion.Task; },
            (_, _, _, _, _) => true, _ => Task.CompletedTask, _ => { calls++; return true; });
        Assert.True(service.CanOpenGrid);
        var pending = service.OpenOrActivateGridAsync(grid);
        using var second = service.RegisterWorkspace(op => Task.FromResult(op()), (_, _, _, _, _) => true, _ => Task.CompletedTask);
        Assert.False(service.CanOpenGrid);
        Assert.False(await service.OpenOrActivateGridAsync(grid));
        completion.SetResult(queued!());
        Assert.False(await pending);
        Assert.Equal(0, calls);
        second.Dispose();
        registration.Dispose();
        Assert.False(await service.OpenOrActivateGridAsync(grid));
    }

    [Fact]
    public async Task ContinuationChoicesRemainWithTheirConversationAndUseSharedChat()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        await using var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        var transport = new PendingTransport();
        var session = new RgfAiConversationSession(transport);
        session.Conversation.ApplyResponse(new()
        {
            Success = true, WorkflowStatus = "WaitingForInput", Message = "Select an answer", ResponseMode = RgfAiResponseMode.ChoiceOnly,
            Choices = [new() { Code = "  original\t", UserText = "Localized response" }]
        });
        await owner.OpenOrActivateAsync("custom", "continuation", "Pending conversation", () => session);
        var chat = cut.FindComponents<RgfAiChatComponent>().Last();
        chat.Find("input[type=radio]").Change(true);
        cut.Find("[data-action=new]").Click();
        Assert.Empty(cut.FindComponents<RgfAiChatComponent>().Last().FindAll("fieldset"));
        Assert.True(await owner.OpenOrActivateAsync("custom", "continuation", "Ignored", () => throw new Exception("Must retain session")));
        Assert.Same(chat.Instance, cut.FindComponents<RgfAiChatComponent>()[1].Instance);
        Assert.True(chat.Find("input[type=radio]").HasAttribute("checked"));
        var turn = chat.Find("fieldset button").ClickAsync(new());
        Assert.Equal("  original\t", Assert.Single(transport.Requests).CurrentUserMessage);
        Assert.Single(session.Conversation.Messages);
        cut.Find("[data-action=new]").Click();
        transport.Complete("Next assistant message");
        await turn;
        Assert.Empty(chat.FindAll("fieldset"));
        Assert.Equal(new[] { "Select an answer", "Localized response", "Next assistant message" }, session.Conversation.Messages.Select(m => m.Message));
        Assert.Empty(cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session.Conversation.Messages);
    }

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
        context.Services.AddScoped<RgfRecrobyWorkspaceService>();
        context.Services.AddScoped<IRgfRecrobyWorkspaceService>(p => p.GetRequiredService<RgfRecrobyWorkspaceService>());
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
        Assert.Empty(cut.FindAll("[role=tab], .nav-tabs"));
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
        first.Instance.Session.Conversation.AddUserMessage("Existing history");
        cut.Find("textarea").Change("Draft one");
        cut.Find("[data-action=new]").Click();
        var chats = cut.FindComponents<RgfAiChatComponent>();
        Assert.Equal(2, chats.Count);
        Assert.NotSame(first.Instance.Session, chats[1].Instance.Session);
        chats[1].Find("textarea").Change("Draft two");
        Assert.Equal(2, cut.FindAll("[data-action=select]").Count);
        Assert.Single(cut.FindAll(".rgf-recroby-conversation-bar .rgf-recroby-new-chat > [data-action=new]"));
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
        AddHistoryToActive(cut);
        cut.Find("[data-action=new]").Click();
        AddHistoryToActive(cut);
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
            AddHistoryToActive(cut);
            cut.Find("[data-action=new]").Click();
            Rename(cut, $"Conversation {i + 1}");
        }
        cut.Find("[data-action=switch]").Click();
        Assert.Empty(cut.FindAll("input[type=search]"));
        AddHistoryToActive(cut);
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
        AddHistoryToActive(cut);
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
        Assert.Empty(cut.FindAll("[role=tab], [role=tabpanel]"));
    }

    [Theory]
    [InlineData("[data-action=new]")]
    [InlineData("[data-new-chat]:last-child")]
    public void NewGeneralChatRemovesEmptyRenamedConversationWithDraftAndSettings(string action)
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var previous = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        Rename(cut, "Unused renamed chat");
        previous.Conversation.AiModelOverride = "custom/model";
        cut.Find("textarea").Change("Unsent draft");
        cut.Find(action).Click();
        var current = Assert.Single(cut.FindComponents<RgfAiChatComponent>()).Instance.Session;
        Assert.NotSame(previous, current);
        Assert.Empty(previous.Conversation.Messages);
        Assert.DoesNotContain("Unused renamed chat", cut.Markup);
        Assert.Null(current.Conversation.AiModelOverride);
    }

    [Fact]
    public void NewGeneralChatPreservesHistory()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var previous = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        previous.Conversation.AddUserMessage("Keep this history");
        cut.Find("[data-action=new]").Click();
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.Same(previous, cut.FindComponents<RgfAiChatComponent>()[0].Instance.Session);
        Assert.Equal("Keep this history", Assert.Single(previous.Conversation.Messages).Message);
    }

    [Fact]
    public async Task GridOpensAndNewChatsReplaceOnlyEmptyConversations()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var general = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var firstGrid = new RgfRecrobyGridContext();
        var secondGrid = new RgfRecrobyGridContext();
        Assert.True(await service.OpenOrActivateGridAsync(firstGrid));
        var first = Assert.Single(cut.FindComponents<RgfAiChatComponent>()).Instance.Session;
        Assert.NotSame(general, first);
        Assert.True(await service.OpenOrActivateGridAsync(firstGrid));
        Assert.Same(first, Assert.Single(cut.FindComponents<RgfAiChatComponent>()).Instance.Session);
        cut.Find("[data-new-chat]:first-child").Click();
        var second = Assert.Single(cut.FindComponents<RgfAiChatComponent>()).Instance.Session;
        Assert.NotSame(first, second);
        Assert.Single(GridContextHandlers(firstGrid));
        Assert.True(await service.OpenOrActivateGridAsync(secondGrid));
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
        Assert.Empty(GridContextHandlers(firstGrid));
        var retained = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        retained.Conversation.AddUserMessage("Retain this Grid history");
        Assert.True(await service.OpenOrActivateGridAsync(firstGrid));
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.True(await service.OpenOrActivateGridAsync(secondGrid));
        Assert.Same(retained, Assert.Single(cut.FindComponents<RgfAiChatComponent>()).Instance.Session);
        Assert.Empty(GridContextHandlers(firstGrid));
        Assert.Single(GridContextHandlers(secondGrid));
        cut.Find("[data-new-chat]:last-child").Click();
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.True(await service.OpenOrActivateGridAsync(secondGrid));
        Assert.Same(retained, Assert.Single(cut.FindComponents<RgfAiChatComponent>()).Instance.Session);
    }

    [Theory]
    [InlineData("ConversationId", "server-id")]
    [InlineData("ConversationToken", "server-token")]
    [InlineData("ConversationId", "")]
    [InlineData("ConversationToken", "")]
    [InlineData("IsPendingWorkflow", true)]
    public void BackendStateWithoutDisplayedMessagesPreventsAutomaticRemoval(string property, object value)
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var previous = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        // Model backend state without presentation history, which the public response API normally appends.
        typeof(RgfAiConversationState).GetProperty(property)!.SetValue(previous.Conversation, value);
        cut.Find("[data-action=new]").Click();
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.Same(previous, cut.FindComponents<RgfAiChatComponent>()[0].Instance.Session);
        Assert.Empty(previous.Conversation.Messages);
    }

    [Fact]
    public async Task ProcessingWithoutDisplayedHistoryPreventsAutomaticRemoval()
    {
        using var context = Context();
        var api = new QueuedApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var previous = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        using var cancellation = new CancellationTokenSource();
        var send = previous.SendAsync("Hidden request", false, cancellation.Token);
        try
        {
            Assert.True(previous.IsProcessing);
            Assert.Empty(previous.Conversation.Messages);
            cut.Find("[data-action=new]").Click();
            Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
            Assert.Same(previous, cut.FindComponents<RgfAiChatComponent>()[0].Instance.Session);
            Assert.False(send.IsCompleted);
        }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
    }

    [Fact]
    public async Task EmptyOwnerConversationSurvivesNewChatAndGridOpen()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        await using var owner = service.CreateOwner();
        var session = new RgfAiConversationSession(new PendingTransport());
        await owner.OpenOrActivateAsync("custom", "owned", "Owned", () => session);
        cut.Find("[data-action=new]").Click();
        Assert.Contains(cut.FindComponents<RgfAiChatComponent>(), c => ReferenceEquals(c.Instance.Session, session));
        await owner.OpenOrActivateAsync("custom", "owned", "Owned", () => throw new Exception("Must reuse"));
        await service.OpenOrActivateGridAsync(new());
        Assert.Contains(cut.FindComponents<RgfAiChatComponent>(), c => ReferenceEquals(c.Instance.Session, session));
        Assert.Empty(session.Conversation.Messages);
    }

    [Fact]
    public async Task AutomaticRemovalDetachesEventsAndBlazorAwaitsComponentCleanup()
    {
        using var context = Context();
        var module = context.JSInterop.SetupModule(invocation => invocation.Arguments[0]?.ToString()?.EndsWith("RgfRecrobyComponent.razor.js") == true);
        module.Mode = JSRuntimeMode.Loose;
        var cleanup = module.SetupVoid("dispose", _ => true);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var grid = new RgfRecrobyGridContext();
        Task disposal;
        try
        {
            await service.OpenOrActivateGridAsync(grid);
            var component = cut.FindComponent<RgfRecrobyComponent>().Instance;
            var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
            var before = module.Invocations["dispose"].Count;
            await service.OpenOrActivateGridAsync(new());
            cut.WaitForAssertion(() => Assert.True(module.Invocations["dispose"].Count > before));
            Assert.Empty(GridContextHandlers(grid));
            var handlers = (Action?)typeof(RgfAiConversationSession).GetField("StateChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session);
            Assert.Null(handlers);
            disposal = component.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.Single(cut.FindComponents<RgfAiChatComponent>());
        }
        finally { cleanup.SetVoidResult(); }
        await disposal;
    }

    [Fact]
    public async Task ManualSelectionPreservesEmptyConversation()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var empty = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        await using var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        await owner.OpenOrActivateAsync("custom", "owned", "Owned", () => new(new PendingTransport()));
        cut.FindAll("[data-action=select]")[0].Click();
        cut.FindAll("[data-action=select]")[1].Click();
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.Same(empty, cut.FindComponents<RgfAiChatComponent>()[0].Instance.Session);
        Assert.Empty(empty.Conversation.Messages);
    }

    [Fact]
    public async Task NewChatLeavesClosingConversationUntilExplicitCleanupCompletes()
    {
        using var context = Context();
        var module = context.JSInterop.SetupModule(invocation => invocation.Arguments[0]?.ToString()?.EndsWith("RgfRecrobyComponent.razor.js") == true);
        module.Mode = JSRuntimeMode.Loose;
        var cleanup = module.SetupVoid("dispose", _ => true);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var previous = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var close = cut.Find(".rgf-recroby-choice > .btn-close").ClickAsync(new());
        try
        {
            cut.WaitForAssertion(() => Assert.NotEmpty(module.Invocations["dispose"]));
            cut.Find("[data-action=new]").Click();
            Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
            Assert.Same(previous, cut.FindComponents<RgfAiChatComponent>()[0].Instance.Session);
            Assert.False(close.IsCompleted);
        }
        finally { cleanup.SetVoidResult(); }
        await close;
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
    }

    private static void AddHistoryToActive(IRenderedComponent<RgfRecrobyWorkspace> cut)
    {
        var index = cut.FindAll(".rgf-recroby-conversation").ToList().FindIndex(e => !e.HasAttribute("hidden"));
        cut.FindComponents<RgfAiChatComponent>()[index].Instance.Session.Conversation.AddUserMessage("Existing history");
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
        for (var i = 1; i < 9; i++)
        {
            AddHistoryToActive(cut);
            cut.Find("[data-action=new]").Click();
        }
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

    [Fact]
    public async Task ExternalConversationsReuseOnlyTheirOwnerKeyAndExpandWorkspace()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>();
        await using var first = service.CreateOwner();
        await using var second = service.CreateOwner();
        var created = 0;
        var transport = new PendingTransport();
        var session = new RgfAiConversationSession(transport);
        Assert.True(await first.OpenOrActivateAsync("custom", "entity/1", "Initial title", () => { created++; return session; }));
        Assert.Equal("false", cut.Find(".rgf-recroby-workspace").GetAttribute("data-collapsed"));
        Assert.Same(session, cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session);
        Assert.Equal("Initial title", cut.Find(".rgf-recroby-title").TextContent);
        Assert.Equal(2, cut.FindAll("[data-conversation-type]").Count);
        cut.Find("[aria-label='Collapse Recroby']").Click();
        Assert.True(await first.OpenOrActivateAsync("custom", "entity/1", "Ignored", () => throw new Exception("Must reuse")));
        Assert.Equal(1, created);
        Assert.Equal("false", cut.Find(".rgf-recroby-workspace").GetAttribute("data-collapsed"));
        Rename(cut, "Manual title");
        var send = session.SendAsync("Must not replace the title", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        transport.Complete("Reply");
        await send;
        Assert.True(await first.OpenOrActivateAsync("custom", "entity/1", "Ignored", () => throw new Exception()));
        Assert.Equal("Manual title", cut.Find(".rgf-recroby-title").TextContent);
        Assert.True(await second.OpenOrActivateAsync("custom", "entity/1", "Independent", () => new(new PendingTransport())));
        Assert.Equal(3, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.NotSame(session, cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session);
        cut.Find("[data-action=new]").Click();
        Assert.Equal("General chat", cut.Find(".rgf-recroby-type").TextContent);
        cut.Find("[aria-label='Close Manual title']").Click();
        Assert.True(await first.OpenOrActivateAsync("custom", "entity/1", "Reopened", () => { created++; return new(new PendingTransport()); }));
        Assert.Equal(2, created);
    }
    [Fact]
    public async Task OwnerDisposalClosesAllOwnedConversationsAndPreservesOtherOwners()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>();
        var owner = service.CreateOwner();
        await using var other = service.CreateOwner();
        foreach (var key in new[] { "a", "b" })
            await owner.OpenOrActivateAsync("custom", key, key, () => new(new PendingTransport()));
        await other.OpenOrActivateAsync("custom", "a", "Other", () => new(new PendingTransport()));
        var disposedChats = cut.FindComponents<RgfAiChatComponent>().Skip(1).Take(2).Select(c => c.Instance).ToArray();
        await Task.WhenAll(owner.DisposeAsync().AsTask(), owner.DisposeAsync().AsTask());
        Assert.Equal(2, cut.FindComponents<RgfAiChatComponent>().Count);
        Assert.DoesNotContain("Close a", cut.Markup);
        Assert.Contains("Other", cut.Markup);
        foreach (var chat in disposedChats)
        {
            chat.Session.Conversation.AddUserMessage("Detached");
            Assert.DoesNotContain("Detached", cut.Markup);
        }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.OpenOrActivateAsync("custom", "a", "a", () => throw new Exception()));
    }

    [Fact]
    public async Task ConcurrentOpensCreateOnlyOneSessionAndDisposePreventsLaterOpens()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        var created = 0;
        var opens = Enumerable.Range(0, 20).Select(_ => Task.Run(() => owner.OpenOrActivateAsync("custom", "a", "a",
            () => { Interlocked.Increment(ref created); return new(new PendingTransport()); }))).ToArray();
        Assert.All(await Task.WhenAll(opens), Assert.True);
        Assert.Equal(1, created);
        var racing = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            try { await owner.OpenOrActivateAsync("custom", i.ToString(), "Race", () => new(new PendingTransport())); }
            catch (ObjectDisposedException) { }
        })).ToArray();
        await owner.DisposeAsync();
        await Task.WhenAll(racing);
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task MissingDisabledAndAmbiguousWorkspacesDoNotCallFactory()
    {
        using var context = Context();
        context.ComponentFactories.AddStub<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ToastComponent>();
        var service = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>();
        await using var owner = service.CreateOwner();
        RgfAiConversationSession Factory() => throw new Exception("Factory must not be called");
        Assert.False(await owner.OpenOrActivateAsync("custom", "a", "a", Factory));
        context.Render<RootWithoutResourceLoading>(p => p.Add(c => c.EnableRecroby, false));
        Assert.False(await owner.OpenOrActivateAsync("custom", "a", "a", Factory));
        var first = context.Render<RgfRecrobyWorkspace>();
        var second = context.Render<RgfRecrobyWorkspace>();
        Assert.False(await owner.OpenOrActivateAsync("custom", "a", "a", Factory));
        await second.InvokeAsync(() => second.Instance.DisposeAsync().AsTask());
        Assert.True(await owner.OpenOrActivateAsync("custom", "a", "a", () => new(new PendingTransport())));
        Assert.Equal(2, first.FindComponents<RgfAiChatComponent>().Count);
    }

    [Theory]
    [InlineData("", "key", "Title")]
    [InlineData("general", "key", "Title")]
    [InlineData(" GENERAL ", "key", "Title")]
    [InlineData("custom", " ", "Title")]
    [InlineData("custom", "key", " ")]
    public async Task ExternalKeysAreValidatedEvenWithoutWorkspace(string type, string key, string title)
    {
        using var context = Context();
        await using var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        await Assert.ThrowsAsync<ArgumentException>(() => owner.OpenOrActivateAsync(type, key, title, () => throw new Exception()));
        await Assert.ThrowsAsync<ArgumentException>(() => owner.OpenOrActivateAsync("custom", "key", new string('x', 101), () => throw new Exception()));
        await owner.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.OpenOrActivateAsync("", "", "", null!));
    }

    [Fact]
    public async Task QueuedOpenCannotCreateSessionAfterOwnerDisposal()
    {
        var service = new RgfRecrobyWorkspaceService();
        Func<bool>? queued = null;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = service.RegisterWorkspace(async action =>
        {
            queued = action;
            await release.Task;
            return action();
        }, (_, _, _, _, factory) => { factory(); return true; }, _ => Task.CompletedTask);
        var owner = service.CreateOwner();
        var pending = owner.OpenOrActivateAsync("custom", "a", "a", () => throw new Exception("Must never create"));
        Assert.NotNull(queued);
        await owner.DisposeAsync();
        release.SetResult(true);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
    }

    [Fact]
    public async Task OwnerDisposalAwaitsAdapterCleanupAndSharesCompletion()
    {
        var service = new RgfRecrobyWorkspaceService();
        var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = service.RegisterWorkspace(action => Task.FromResult(action()),
            (_, _, _, _, _) => true, async _ => { closeStarted.SetResult(); await closeFinished.Task; });
        var owner = service.CreateOwner();
        Assert.True(await owner.OpenOrActivateAsync("custom", "a", "a", () => throw new Exception()));
        var first = owner.DisposeAsync().AsTask();
        await closeStarted.Task;
        var second = owner.DisposeAsync().AsTask();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        closeFinished.SetResult();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task OwnerDisposalAwaitsActualConversationBrowserCleanup()
    {
        using var context = Context();
        context.Services.AddSingleton<IRgfApiService>(new CatalogApi());
        var module = context.JSInterop.SetupModule(invocation => invocation.Arguments[0]?.ToString()?.EndsWith("RgfRecrobyComponent.razor.js") == true);
        module.Mode = JSRuntimeMode.Loose;
        var cleanup = module.SetupVoid("dispose", _ => true);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        await owner.OpenOrActivateAsync("custom", "a", "External", () => new(new PendingTransport()));
        var disposal = owner.DisposeAsync().AsTask();
        cut.WaitForAssertion(() => Assert.NotEmpty(module.Invocations["dispose"]));
        Assert.False(disposal.IsCompleted);
        cleanup.SetVoidResult();
        await disposal;
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
    }

    [Fact]
    public async Task ReinitializedRootInvalidatesPreviousOwnersAndConversationState()
    {
        using var context = Context();
        context.ComponentFactories.AddStub<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ToastComponent>();
        var host = context.Render<ReinitializingHost>();
        var service = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>();
        var owner = service.CreateOwner();
        await owner.OpenOrActivateAsync("custom", "a", "Old state", () => new(new PendingTransport()));
        var previous = host.FindComponent<RgfRecrobyWorkspace>().Instance;
        host.Render(p => p.Add(c => c.Revision, 1));
        host.WaitForAssertion(() => Assert.NotSame(previous, host.FindComponent<RgfRecrobyWorkspace>().Instance));
        Assert.Single(host.FindComponents<RgfAiChatComponent>());
        Assert.DoesNotContain("Old state", host.Markup);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.OpenOrActivateAsync("custom", "a", "a", () => throw new Exception()));
        await owner.DisposeAsync();
        await previous.DisposeAsync();
        await using var replacement = service.CreateOwner();
        Assert.True(await replacement.OpenOrActivateAsync("custom", "a", "New state", () => new(new PendingTransport())));
    }

    [Fact]
    public async Task WorkspaceAndOwnerDisposalAwaitTheSameCleanup()
    {
        using var context = Context();
        var module = context.JSInterop.SetupModule(invocation => invocation.Arguments[0]?.ToString()?.EndsWith("RgfRecrobyWorkspace.razor.js") == true);
        module.Mode = JSRuntimeMode.Loose;
        var cleanup = module.SetupVoid("dispose", _ => true);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        await owner.OpenOrActivateAsync("custom", "a", "a", () => new(new PendingTransport()));
        var workspaceDisposal = cut.Instance.DisposeAsync().AsTask();
        cut.WaitForAssertion(() => Assert.NotEmpty(module.Invocations["dispose"]));
        var ownerDisposal = owner.DisposeAsync().AsTask();
        Assert.False(workspaceDisposal.IsCompleted);
        Assert.False(ownerDisposal.IsCompleted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.OpenOrActivateAsync("custom", "b", "b", () => throw new Exception()));
        cleanup.SetVoidResult();
        await Task.WhenAll(workspaceDisposal, ownerDisposal);
        await cut.Instance.DisposeAsync();
        await owner.DisposeAsync();
    }
    [Fact]
    public async Task LastExternalConversationFallsBackToGeneralWithoutCancellingExternalSend()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var owner = context.Services.GetRequiredService<IRgfRecrobyWorkspaceService>().CreateOwner();
        var transport = new PendingTransport();
        var session = new RgfAiConversationSession(transport);
        await owner.OpenOrActivateAsync("custom", "a", "External", () => session);
        cut.Find("[aria-label='Close New chat']").Click();
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
        var send = session.SendAsync("External caller request", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        await owner.DisposeAsync();
        Assert.Single(cut.FindComponents<RgfAiChatComponent>());
        Assert.Equal("New chat", cut.Find(".rgf-recroby-title").TextContent);
        Assert.NotSame(session, cut.FindComponent<RgfAiChatComponent>().Instance.Session);
        Assert.False(send.IsCompleted);
        transport.Complete("External caller response");
        await send;
        Assert.DoesNotContain("External caller response", cut.Markup);
    }
    private sealed class ReinitializingHost : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter] public int Revision { get; set; }
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<RootWithoutResourceLoading>(0);
            builder.SetKey(Revision);
            builder.CloseComponent();
        }
    }

    private sealed class CatalogApi : IRgfApiService
    {
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class =>
            Task.FromResult((IRgfApiResponse<T>)(object)new ApiResponse<RgfAiCatalogResponse>
            { Success = true, Result = new("p", [new("p", "m", [new("m", [], [], null, null)])]) });
        public Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
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
        public List<RgfAiRequest> Requests { get; } = [];
        public Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return completion.Task;
        }
        public void Complete(string message) => completion.SetResult(new() { Success = true, Message = message });
    }
}
