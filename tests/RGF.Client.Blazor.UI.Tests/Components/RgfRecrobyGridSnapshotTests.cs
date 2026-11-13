using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using System.Reflection;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed partial class RgfRecrobyWorkspaceTests
{
    [Theory]
    [InlineData("before", "RecrobyFilterApplicationUncertain")]
    [InlineData("during", "RecrobyFilterApplicationUncertain")]
    [InlineData("after", null)]
    [InlineData("unchanged", "RecrobyFilterNotApplied")]
    [InlineData("cancelled", "RecrobyFilterNotApplied")]
    [InlineData("cancelled-after", null)]
    [InlineData("dispatcher-cancelled", "RecrobyFilterApplicationUncertain")]
    [InlineData("dispatcher-unexpected-cancellation", "RecrobyFilterApplicationUncertain")]
    [InlineData("dispatcher-cancelled-after", null)]
    [InlineData("apply-cancelled", "RecrobyFilterNotApplied")]
    [InlineData("apply-cancelled-partial", "RecrobyFilterApplicationUncertain")]
    [InlineData("apply-cancelled-after", null)]
    [InlineData("apply-unexpected-cancellation", "RecrobyFilterNotApplied")]
    [InlineData("apply-error-after-cancellation", "RecrobyFilterNotApplied")]
    [InlineData("toast-cancelled", "RecrobyFilterNotApplied")]
    [InlineData("toast-unexpected-cancellation", "RecrobyFilterNotApplied")]
    [InlineData("toast-error-after-cancellation", "RecrobyFilterNotApplied")]
    [InlineData("closed", null)]
    [InlineData("http", null)]
    public async Task CompletedFilterTurnPreservesTheIssuedIdentityAcrossDispatcherFailures(string scenario, string? errorCode)
    {
        using var context = Context();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Xunit.TestContext.Current.CancellationToken);
        var api = new SnapshotApi
        {
            Response = new()
            {
                Success = true, Message = "filter response", WorkflowStatus = "Completed",
                ConversationId = "id", ConversationToken = "issued", WorkflowRunId = "run",
                FilterAction = new(RgfAiFilterOperation.ReplaceIfUnchanged, "session", "grid", 7, [])
            },
            HttpSuccess = scenario != "http"
        };
        if (scenario == "cancelled")
        {
            api.BeforeResponse = cancellation.Cancel;
        }
        var manager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        var fake = (SnapshotManager)manager;
        var list = DispatchProxy.Create<IRgListHandler, SnapshotList>();
        var listProxy = (SnapshotList)list;
        fake.List = list;
        listProxy.Settings = fake.Settings;
        listProxy.Refresh = (_, _) => Task.CompletedTask;
        var applications = 0;
        var cancellationError = new OperationCanceledException();
        var applyError = new InvalidOperationException("Apply failed");
        var toastAttempts = 0;
        if (scenario.StartsWith("toast-", StringComparison.Ordinal))
        {
            fake.ToastManager = new CapturingNotifications();
        }
        listProxy.Apply = conditions =>
        {
            applications++;
            if (scenario == "unchanged")
            {
                throw new InvalidOperationException("Apply failed before mutation");
            }
            if (scenario == "during")
            {
                fake.Settings.Conditions![0].Conditions[0].Param1 = "partial";
                throw new InvalidOperationException("Apply failed during mutation");
            }
            switch (scenario)
            {
                case "apply-cancelled-partial":
                    fake.Settings.Conditions![0].Conditions[0].Param1 = "partial";
                    goto case "apply-cancelled";
                case "apply-cancelled":
                    cancellation.Cancel();
                    throw cancellationError;
                case "apply-cancelled-after":
                    fake.Settings.Conditions = conditions;
                    cancellation.Cancel();
                    throw cancellationError;
                case "apply-unexpected-cancellation":
                    throw cancellationError;
                case "apply-error-after-cancellation":
                    cancellation.Cancel();
                    throw applyError;
                case "toast-cancelled":
                case "toast-unexpected-cancellation":
                case "toast-error-after-cancellation":
                    throw applyError;
            }
            fake.Settings.Conditions = conditions;
        };
        var handler = DispatchProxy.Create<IRgFilterHandler, SnapshotFilter>();
        fake.PrepareFilter = () => Task.FromResult(handler);
        ((SnapshotFilter)handler).Handle = method => method switch
        {
            nameof(IRgFilterHandler.InitFilter) => true,
            nameof(IRgFilterHandler.StoreFilter) => Array.Empty<RgfFilter.Condition>(),
            nameof(IRgFilterHandler.SetNewAiFilterSettings) => null,
            _ => throw new NotSupportedException(method)
        };
        var grid = new RgfRecrobyGridContext();
        grid.SetManager(manager);
        var warnings = new List<Exception>();
        Action<Exception, string> log = (error, _) => warnings.Add(error);
        Exception dispatcherError = scenario is "closed" or "dispatcher-cancelled" or
            "dispatcher-unexpected-cancellation" or "dispatcher-cancelled-after"
            ? new OperationCanceledException()
            : new InvalidOperationException("Dispatcher failed");
        Func<Task>? lateCallback = null;
        var abandoned = false;
        var assembly = typeof(RgfRecrobyWorkspace).Assembly;
        var prefix = typeof(RgfRecrobyWorkspace).Namespace + ".";
        // Inject the dispatcher at the internal boundary while exercising the public session lifecycle.
        var refresher = Activator.CreateInstance(assembly.GetType(prefix + "RgfRecrobyGridRefresher", true)!, log);
        var dict = context.Services.GetRequiredService<IRecroDictService>();
        var actionHandler = Activator.CreateInstance(assembly.GetType(prefix + "RgfRecrobyFilterActionHandler", true)!,
            context.Services, dict, (Func<RgfRecrobyGridContext, bool>)(_ => true),
            (Func<IRgfNotificationManager, RgfToastEventArgs, Task>)((_, _) =>
            {
                toastAttempts++;
                if (scenario is "toast-cancelled" or "toast-error-after-cancellation")
                {
                    cancellation.Cancel();
                }
                return Task.FromException(scenario == "toast-error-after-cancellation" ? applyError : cancellationError);
            }), log, refresher);
        var transport = (IRgfAiTransport)Activator.CreateInstance(assembly.GetType(prefix + "RgfRecrobyConversationTransport", true)!,
            (Func<IRgfApiService>)(() => api), dict, (Func<RgfRecrobyGridContext>)(() => grid),
            (Func<Action, Task>)(action => { action(); return Task.CompletedTask; }),
            (Func<Func<Task>, Task>)(async callback =>
            {
                if (scenario == "closed")
                {
                    abandoned = true;
                }
                if (scenario is "before" or "dispatcher-cancelled" or "dispatcher-unexpected-cancellation")
                {
                    lateCallback = callback;
                }
                else
                {
                    await callback();
                }
                if (scenario is "cancelled-after" or "dispatcher-cancelled" or "dispatcher-cancelled-after")
                {
                    cancellation.Cancel();
                }
                if (!scenario.StartsWith("apply-", StringComparison.Ordinal) &&
                    !scenario.StartsWith("toast-", StringComparison.Ordinal))
                {
                    throw dispatcherError;
                }
            }), (Func<bool>)(() => abandoned), actionHandler, log)!;
        var session = new RgfAiConversationSession(transport);
        var other = new RgfAiConversationSession(new RgfAiApiTransport(api));

        if (scenario is "closed" or "http")
        {
            if (scenario == "closed")
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SendAsync("filter", cancellationToken: cancellation.Token));
            }
            else
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => session.SendAsync("filter", cancellationToken: cancellation.Token));
            }
            Assert.Empty(warnings);
            Assert.Null(session.Conversation.ConversationToken);
            Assert.Empty(session.Conversation.Messages);
            Assert.Null(other.Conversation.ConversationToken);
            Assert.Empty(other.Conversation.Messages);
            Assert.Single(api.Json);
            Assert.Equal(0, applications);
            Assert.Equal("value", fake.Settings.Conditions![0].Conditions[0].Param1);
            return;
        }

        var response = await session.SendAsync("filter", cancellationToken: cancellation.Token);
        if (lateCallback is not null)
        {
            await lateCallback();
        }
        Assert.Equal(scenario.StartsWith("toast-", StringComparison.Ordinal) ? 1 : 0, toastAttempts);
        switch (scenario)
        {
            case "dispatcher-cancelled":
            case "dispatcher-cancelled-after":
            case "apply-cancelled":
            case "apply-cancelled-partial":
            case "apply-cancelled-after":
                Assert.Empty(warnings);
                break;
            case "apply-unexpected-cancellation":
                Assert.Same(cancellationError, Assert.Single(warnings));
                break;
            case "apply-error-after-cancellation":
            case "toast-cancelled":
                Assert.Same(applyError, Assert.Single(warnings));
                break;
            case "toast-unexpected-cancellation":
                Assert.Equal(new Exception[] { applyError, cancellationError }, warnings);
                break;
            case "toast-error-after-cancellation":
                Assert.Equal(new[] { applyError, applyError }, warnings);
                break;
            default:
                Assert.Contains(dispatcherError, warnings);
                break;
        }
        Assert.Equal(errorCode is null, response.Success);
        Assert.Equal(errorCode, response.ErrorCode);
        Assert.Equal("run", response.WorkflowRunId);
        Assert.Equal("id", session.Conversation.ConversationId);
        Assert.Equal("issued", session.Conversation.ConversationToken);
        Assert.Equal(response.Success ? 2 : 0, session.Conversation.Messages.Count);
        Assert.Single(api.Json);

        if (response.Success)
        {
            Assert.Equal(1, applications);
            Assert.Empty(fake.Settings.Conditions!);
        }
        else
        {
            var attemptedApply = scenario is "during" or "unchanged" ||
                scenario.StartsWith("apply-", StringComparison.Ordinal) || scenario.StartsWith("toast-", StringComparison.Ordinal);
            Assert.Equal(attemptedApply ? 1 : 0, applications);
            Assert.Equal(scenario is "during" or "apply-cancelled-partial" ? "partial" : "value",
                fake.Settings.Conditions![0].Conditions[0].Param1);
        }
        api.Response = new() { Success = true, ConversationId = "id", ConversationToken = "next" };
        api.BeforeResponse = null;
        await session.SendAsync("next request", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        using var next = JsonDocument.Parse(api.Json[1]);
        Assert.Equal("id", next.RootElement.GetProperty("conversationId").GetString());
        Assert.Equal("issued", next.RootElement.GetProperty("conversationToken").GetString());
        Assert.Equal(2, api.Json.Count);
    }

    [Fact]
    public async Task ClosingWorkspaceConversationDiscardsLateCompletedFilterResponse()
    {
        using var context = Context();
        var api = new SnapshotApi
        {
            Hold = true,
            Response = new()
            {
                Success = true, WorkflowStatus = "Completed", WorkflowRunId = "run",
                ConversationId = "closed", ConversationToken = "issued",
                FilterAction = new(RgfAiFilterOperation.ReplaceIfUnchanged, "session", "grid", 7, [])
            }
        };
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var manager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        var fake = (SnapshotManager)manager;
        var grid = new RgfRecrobyGridContext();
        grid.SetManager(manager);
        Assert.True(await context.Services.GetRequiredService<RgfRecrobyWorkspaceService>().RegisterGridAsync(grid));
        var closing = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var send = closing.SendAsync("clear filter", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        try
        {
            cut.WaitForAssertion(() => Assert.Single(api.Json));
            cut.Find("[data-action=switch]").Click();
            cut.Find(".rgf-recroby-choice > .btn-close").Click();
        }
        finally
        {
            api.Release.TrySetResult();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        var replacement = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        Assert.NotSame(closing, replacement);
        Assert.Null(closing.Conversation.ConversationToken);
        Assert.Empty(closing.Conversation.Messages);
        Assert.Null(replacement.Conversation.ConversationToken);
        Assert.Empty(replacement.Conversation.Messages);
        Assert.Equal("value", fake.Settings.Conditions![0].Conditions[0].Param1);
    }

    [Fact]
    public void SnapshotCopiesNestedConditionsAndSortAndAllowlistsEventOutput()
    {
        var manager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        var fake = (SnapshotManager)manager;
        var grid = new RgfRecrobyGridContext();
        grid.SetManager(manager);
        fake.Created = request =>
        {
            request.Language = "event-language";
            request.ListParam.UserFilter[0].Conditions[0].Alias = "event";
            request.Data = new();
            request.CustomParams = new() { ["secret"] = "discard" };
            request.ParentGridRequest = RgfGridRequest.Create(new());
            request.FilterParent = new();
            request.ListParam.FixFilter = [new()];
        };
        var snapshot = grid.CreateGridRequest()!;
        Assert.Equal(1, fake.Requests);
        Assert.Equal("event-language", snapshot.Language);
        Assert.Equal("event", snapshot.ListParam.UserFilter[0].Conditions[0].Alias);
        snapshot.ListParam.UserFilter[0].Conditions[0].Alias = "changed";
        snapshot.ListParam.Sort[0][0] = 999;
        Assert.Equal("live", fake.Settings.Conditions![0].Conditions[0].Alias);
        Assert.Equal(7, fake.Settings.Sort![0][0]);
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var excluded in new[] { "data", "customParams", "parentGridRequest", "filterParent", "gridSettings", "clientConnectionId", "fixFilter" })
            Assert.False(JsonDocument.Parse(json).RootElement.TryGetProperty(excluded, out _));
        Assert.Null(snapshot.ListParam.FixFilter);
        grid.ClearManager(manager);
        Assert.Null(grid.CreateGridRequest());
    }

    [Fact]
    public async Task WorkspaceSnapshotsTheActiveGridAtTheStartOfEachConversationTurn()
    {
        using var context = Context();
        var api = new SnapshotApi();
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var general = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        await general.SendAsync("general", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.False(JsonDocument.Parse(api.Json[0]).RootElement.TryGetProperty("gridRequest", out _));
        var workspace = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var firstManager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        var secondManager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        ((SnapshotManager)firstManager).GridId = "first";
        ((SnapshotManager)secondManager).GridId = "second";
        var first = new RgfRecrobyGridContext();
        var second = new RgfRecrobyGridContext();
        first.SetManager(firstManager);
        second.SetManager(secondManager);
        await workspace.OpenOrActivateGridAsync(first);
        var firstSession = cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session;
        api.Hold = true;
        var firstSend = firstSession.SendAsync("first", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        cut.Find("[data-action=new]").Click();
        await workspace.OpenOrActivateGridAsync(second);
        var secondSession = cut.FindComponents<RgfAiChatComponent>().Last().Instance.Session;
        var secondSend = secondSession.SendAsync("second", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.NotSame(firstSession, secondSession);
        Assert.Equal(new[] { "first", "second" }, api.Json.Skip(1).Select(ReadGrid).Select(g => g!.GridId));
        api.Release.SetResult();
        await Task.WhenAll(firstSend, secondSend);
        api.Hold = false;
        var replacement = DispatchProxy.Create<IRgManager, SnapshotManager>();
        ((SnapshotManager)replacement).GridId = "replacement";
        ((SnapshotManager)replacement).Settings.Sort![0][0] = 12;
        await cut.InvokeAsync(() =>
        {
            first.SetManager(replacement);
            first.ClearManager(firstManager);
        });
        var token = Xunit.TestContext.Current.CancellationToken;
        await firstSession.SendAsync("first chat while second Grid is active", cancellationToken: token);
        await secondSession.SendAsync("second chat while second Grid is active", cancellationToken: token);
        Assert.All(api.Json.TakeLast(2), json => Assert.Equal("second", ReadGrid(json)!.GridId));
        await cut.InvokeAsync(second.Close);
        await firstSession.SendAsync("replacement after fallback", cancellationToken: token);
        Assert.Equal("replacement", ReadGrid(api.Json.Last())!.GridId);
        Assert.Equal(12, ReadGrid(api.Json.Last())!.ListParam.Sort[0][0]);
        await cut.InvokeAsync(() => first.ClearManager(replacement));
        await firstSession.SendAsync("temporarily unavailable", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.Null(ReadGrid(api.Json.Last()));
        Assert.Contains(cut.FindComponents<RgfAiChatComponent>(), c => ReferenceEquals(c.Instance.Session, firstSession));
    }

    [Fact]
    public async Task PendingWorkflowRejectsOtherAndMissingGridsAndResumesOnTheOriginal()
    {
        using var context = Context();
        var api = new SnapshotApi
        {
            Response = new()
            {
                Success = true, WorkflowStatus = "WaitingForInput",
                ConversationId = "pending-id", ConversationToken = "pending-token", Message = "Choose a filter"
            }
        };
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var a = new RgfRecrobyGridContext();
        var b = new RgfRecrobyGridContext();
        var manager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        a.SetManager(manager);
        var other = DispatchProxy.Create<IRgManager, SnapshotManager>();
        ((SnapshotManager)other).GridId = "other";
        b.SetManager(other);
        Assert.True(await service.RegisterGridAsync(a));
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var token = Xunit.TestContext.Current.CancellationToken;
        await session.SendAsync("begin", cancellationToken: token);
        Assert.True(session.Conversation.IsPendingWorkflow);

        Assert.True(await service.RegisterGridAsync(b));
        var rejection = await session.SendAsync("continue on B", cancellationToken: token);
        Assert.False(rejection.Success);
        Assert.Equal("PendingGridChanged", rejection.ErrorCode);
        Assert.Contains("original Grid", rejection.Message);
        await cut.InvokeAsync(() => { b.Close(); a.ClearManager(manager); });
        rejection = await session.SendAsync("continue without a Grid", cancellationToken: token);
        Assert.Equal("PendingGridChanged", rejection.ErrorCode);
        Assert.Single(api.Json);
        Assert.True(session.Conversation.IsPendingWorkflow);
        Assert.Equal("pending-id", session.Conversation.ConversationId);
        Assert.Equal("pending-token", session.Conversation.ConversationToken);
        Assert.Equal(2, session.Conversation.Messages.Count);

        await cut.InvokeAsync(() => a.SetManager(manager));
        api.Response = new() { Success = true, ConversationId = "pending-id", Message = "Done" };
        Assert.True((await session.SendAsync("continue on A", cancellationToken: token)).Success);
        Assert.False(session.Conversation.IsPendingWorkflow);
        Assert.Equal(2, api.Json.Count);
        Assert.Equal("grid", ReadGrid(api.Json[^1])!.GridId);
        Assert.Equal("pending-token", JsonDocument.Parse(api.Json[^1]).RootElement.GetProperty("conversationToken").GetString());
    }

    private static RgfGridRequest? ReadGrid(string json) =>
        JsonSerializer.Deserialize<RgfAiRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.GridRequest;

    [Fact]
    public async Task ModifyRechecksTheAppliedFilterAfterAwaitingHandlerPreparation()
    {
        using var context = Context();
        var api = new SnapshotApi
        {
            Response = new()
            {
                Success = true, Message = "Modified filter", WorkflowStatus = "Completed",
                ConversationId = "id", ConversationToken = "next", WorkflowRunId = "run",
                FilterAction = new(RgfAiFilterOperation.ReplaceIfUnchanged, "session", "grid", 7, [])
            }
        };
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var manager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        var fake = (SnapshotManager)manager;
        var prepared = new TaskCompletionSource<IRgFilterHandler>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.PrepareFilter = () =>
        {
            started.SetResult();
            return prepared.Task;
        };
        var grid = new RgfRecrobyGridContext();
        grid.SetManager(manager);
        Assert.True(await service.RegisterGridAsync(grid));
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var token = Xunit.TestContext.Current.CancellationToken;
        var send = session.SendAsync("Clear the applied filter", cancellationToken: token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        var handler = DispatchProxy.Create<IRgFilterHandler, SnapshotFilter>();
        try
        {
            await cut.InvokeAsync(() => fake.Settings.Conditions![0].Conditions[0].Param1 = "new value");
        }
        finally
        {
            prepared.SetResult(handler);
        }
        var response = await send;
        Assert.False(response.Success);
        Assert.Equal("RecrobyFilterNotApplied", response.ErrorCode);
        Assert.Empty(session.Conversation.Messages);
        Assert.Equal("next", session.Conversation.ConversationToken);
        Assert.Equal(0, ((SnapshotFilter)handler).InitCalls);
        Assert.Equal("new value", fake.Settings.Conditions![0].Conditions[0].Param1);
    }

    [Fact]
    public async Task RecrobyShowsTheReplyWhileTheAppliedListsRefreshIsPendingAndLogsItsFailure()
    {
        using var context = Context();
        using var logs = new ListLoggerProvider();
        context.Services.AddLogging(logging => logging.AddProvider(logs));
        var api = new SnapshotApi
        {
            Response = new()
            {
                Success = true, Message = "Modified filter",
                FilterAction = new(RgfAiFilterOperation.ReplaceIfUnchanged, "session", "grid", 7, [], "AI description")
            }
        };
        context.Services.AddSingleton<IRgfApiService>(api);
        var cut = context.Render<RgfRecrobyWorkspace>();
        var service = context.Services.GetRequiredService<RgfRecrobyWorkspaceService>();
        var manager = DispatchProxy.Create<IRgManager, SnapshotManager>();
        var fake = (SnapshotManager)manager;
        var list = DispatchProxy.Create<IRgListHandler, SnapshotList>();
        var listProxy = (SnapshotList)list;
        listProxy.Settings = fake.Settings;
        fake.List = list;
        var handler = DispatchProxy.Create<IRgFilterHandler, SnapshotFilter>();
        fake.PrepareFilter = () => Task.FromResult(handler);
        var operations = new List<string>();
        ((SnapshotFilter)handler).Handle = method =>
        {
            operations.Add(method);
            return method switch
            {
                nameof(IRgFilterHandler.InitFilter) => true,
                nameof(IRgFilterHandler.StoreFilter) => Array.Empty<RgfFilter.Condition>(),
                nameof(IRgFilterHandler.SetNewAiFilterSettings) => throw new InvalidOperationException("Notification failed"),
                _ => throw new NotSupportedException(method)
            };
        };
        var refresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        listProxy.Apply = conditions =>
        {
            operations.Add("Apply");
            listProxy.Settings.Conditions = conditions;
        };
        listProxy.Refresh = (settingsId, invalidateAggregates) =>
        {
            operations.Add("Refresh");
            Assert.Null(settingsId);
            Assert.False(invalidateAggregates);
            return refresh.Task;
        };
        var grid = new RgfRecrobyGridContext();
        grid.SetManager(manager);
        grid.RegisterAiFilterAppliedCallback((appliedManager, appliedHandler) =>
        {
            Assert.Same(manager, appliedManager);
            Assert.Same(handler, appliedHandler);
            operations.Add("Notify");
            return Task.CompletedTask;
        });
        Assert.True(await service.RegisterGridAsync(grid));
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var token = Xunit.TestContext.Current.CancellationToken;
        try
        {
            await session.SendAsync("Clear the applied filter", cancellationToken: token)
                .WaitAsync(TimeSpan.FromSeconds(5), token);
            Assert.Equal(new[] { "InitFilter", "StoreFilter", "Apply", "SetNewAiFilterSettings", "Notify", "Refresh" }, operations);
            Assert.Equal("Modified filter", session.Conversation.Messages[^1].Message);
            Assert.Null(api.Response.FilterAction);
            Assert.True(api.Response.Success);
        }
        finally
        {
            refresh.TrySetException(new InvalidOperationException("Test refresh failure"));
        }

        cut.WaitForAssertion(() => Assert.Contains(logs.Entries, entry =>
            entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, refresh.Task.Exception!.InnerException)));
    }

    public class SnapshotManager : DispatchProxy
    {
        public string GridId { get; set; } = "grid";
        public IRgListHandler? List { get; set; }
        public RgfGridSettings Settings { get; } = new()
        {
            Conditions = [new() { Conditions = [new() { Alias = "live", Param1 = "value" }] }],
            Sort = [[7, 1]]
        };
        public Action<RgfGridRequest>? Created { get; set; }
        public Func<Task<IRgFilterHandler>>? PrepareFilter { get; set; }
        public IRgfNotificationManager? ToastManager { get; set; }
        public int Requests { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_EntityDesc")
            {
                return new RgfEntity { EntityId = 7, EntityName = "Orders", Title = "Orders" };
            }
            if (method.Name == nameof(IRgManager.GetFilterHandlerAsync))
            {
                return PrepareFilter!();
            }
            if (method.Name == "get_ToastManager")
            {
                return ToastManager;
            }
            if (method.Name == "get_SessionParams") return new RgfSessionParams { SessionId = "session", GridId = GridId };
            if (method.Name == "get_ListHandler")
            {
                if (List is not null)
                {
                    return List;
                }
                var list = DispatchProxy.Create<IRgListHandler, SnapshotList>();
                ((SnapshotList)list).Settings = Settings;
                return list;
            }
            if (method.Name == nameof(IRgManager.CreateGridRequest))
            {
                Requests++;
                var request = RgfGridRequest.Create(new RgfSessionParams
                { SessionId = "session", GridId = GridId, Language = "eng", ClientConnectionId = "discard" });
                ((Action<RgfGridRequest>?)args![0])?.Invoke(request);
                Created?.Invoke(request);
                return request;
            }
            throw new NotSupportedException(method.Name);
        }
    }

    public class SnapshotList : DispatchProxy
    {
        public RgfGridSettings Settings { get; set; } = null!;
        public Action<RgfFilter.Condition[]>? Apply { get; set; }
        public Func<int?, bool, Task>? Refresh { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case nameof(IRgListHandler.GetGridSettings):
                    return Settings;
                case "get_Initialized":
                    return true;
                case "get_SQLTimeout":
                    return 41;
                case nameof(IRgListHandler.ApplyFilterState):
                    Apply!((RgfFilter.Condition[])args![0]!);
                    return null;
                case nameof(IRgListHandler.RefreshDataAsync):
                    return Refresh!((int?)args![0], (bool)args[1]!);
                default:
                    throw new NotSupportedException(method.Name);
            }
        }
    }

    public class SnapshotFilter : DispatchProxy
    {
        public int InitCalls { get; private set; }
        public Func<string, object?>? Handle { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Handle is not null)
            {
                return Handle(method!.Name);
            }
            Assert.Equal(nameof(IRgFilterHandler.InitFilter), method!.Name);
            InitCalls++;
            return false;
        }
    }

    private sealed class SnapshotApi : IRgfApiService
    {
        public List<string> Json { get; } = [];
        public bool Hold { get; set; }
        public bool HttpSuccess { get; set; } = true;
        public Action? BeforeResponse { get; set; }
        public RgfAiResponse Response { get; set; } = new() { Success = true, Message = "answer" };
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Json.Add(await request.Content.ReadAsStringAsync());
            if (Hold) await Release.Task;
            BeforeResponse?.Invoke();
            return (IRgfApiResponse<T>)(object)new ApiResponse<RgfAiResponse> { Success = HttpSuccess,
                Result = Response };
        }
    }
}
