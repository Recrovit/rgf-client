using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Reflection;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed partial class RgfRecrobyWorkspaceTests
{
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
                Success = true, Message = "Modified filter",
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
        await send;
        Assert.Equal(0, ((SnapshotFilter)handler).InitCalls);
        Assert.Equal("new value", fake.Settings.Conditions![0].Conditions[0].Param1);
    }

    public class SnapshotManager : DispatchProxy
    {
        public string GridId { get; set; } = "grid";
        public RgfGridSettings Settings { get; } = new()
        {
            Conditions = [new() { Conditions = [new() { Alias = "live", Param1 = "value" }] }],
            Sort = [[7, 1]]
        };
        public Action<RgfGridRequest>? Created { get; set; }
        public Func<Task<IRgFilterHandler>>? PrepareFilter { get; set; }
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
                return null;
            }
            if (method.Name == "get_SessionParams") return new RgfSessionParams { SessionId = "session", GridId = GridId };
            if (method.Name == "get_ListHandler")
            {
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
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IRgListHandler.GetGridSettings) => Settings,
            "get_Initialized" => true,
            _ => throw new NotSupportedException(method.Name)
        };
    }

    public class SnapshotFilter : DispatchProxy
    {
        public int InitCalls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal(nameof(IRgFilterHandler.InitFilter), method!.Name);
            InitCalls++;
            return false;
        }
    }

    private sealed class SnapshotApi : IRgfApiService
    {
        public List<string> Json { get; } = [];
        public bool Hold { get; set; }
        public RgfAiResponse Response { get; set; } = new() { Success = true, Message = "answer" };
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Json.Add(await request.Content.ReadAsStringAsync());
            if (Hold) await Release.Task;
            return (IRgfApiResponse<T>)(object)new ApiResponse<RgfAiResponse> { Success = true,
                Result = Response };
        }
    }
}
