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
    public async Task WorkspaceSerializesConversationSpecificSnapshotsDuringConcurrentSendsAndManagerReplacement()
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
        first.SetManager(replacement);
        first.ClearManager(firstManager);
        await firstSession.SendAsync("updated while second is active", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.Equal("replacement", ReadGrid(api.Json.Last())!.GridId);
        Assert.Equal(12, ReadGrid(api.Json.Last())!.ListParam.Sort[0][0]);
        first.ClearManager(replacement);
        await firstSession.SendAsync("temporarily unavailable", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.Null(ReadGrid(api.Json.Last()));
        Assert.Contains(cut.FindComponents<RgfAiChatComponent>(), c => ReferenceEquals(c.Instance.Session, firstSession));
    }

    private static RgfGridRequest? ReadGrid(string json) =>
        JsonSerializer.Deserialize<RgfAiRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.GridRequest;

    public class SnapshotManager : DispatchProxy
    {
        public string GridId { get; set; } = "grid";
        public RgfGridSettings Settings { get; } = new()
        {
            Conditions = [new() { Conditions = [new() { Alias = "live", Param1 = "value" }] }],
            Sort = [[7, 1]]
        };
        public Action<RgfGridRequest>? Created { get; set; }
        public int Requests { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_EntityDesc") return new RgfEntity { EntityName = "Orders", Title = "Orders" };
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
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IRgListHandler.GetGridSettings) ? Settings : throw new NotSupportedException(method.Name);
    }

    private sealed class SnapshotApi : IRgfApiService
    {
        public List<string> Json { get; } = [];
        public bool Hold { get; set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Json.Add(await request.Content.ReadAsStringAsync());
            if (Hold) await Release.Task;
            return (IRgfApiResponse<T>)(object)new ApiResponse<RgfAiResponse> { Success = true,
                Result = new() { Success = true, Message = "answer" } };
        }
    }
}
