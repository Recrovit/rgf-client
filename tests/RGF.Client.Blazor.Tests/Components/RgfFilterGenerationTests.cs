using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Reflection;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Components;

public sealed class RgfFilterGenerationTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("conditions")]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("switch")]
    [InlineData("close")]
    [InlineData("reopen")]
    [InlineData("failure")]
    public async Task Generation_is_detached_atomic_and_never_saves_or_overwrites_newer_state(string variant)
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        editor.FilterSettings.SettingsName = "Before";
        editor.Description = "Existing";
        var pending = f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync("provider", "model"));
        f.Cut.WaitForAssertion(() => Assert.True(editor.IsGeneratingNameDescription));
        await f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync());
        Assert.Equal(1, f.Api.Calls);
        Assert.Equal("Before", f.Api.Request!.ExistingName);
        Assert.Equal("Existing", f.Api.Request.ExistingDescription);
        Assert.Equal("provider", f.Api.Request.AiProviderOverride);
        Assert.Equal("model", f.Api.Request.AiModelOverride);
        var snapshot = f.Api.Request.ObjectData.GetProperty("conditions")[0].GetProperty("param1").GetString();
        Assert.Equal("alpha", snapshot);
        await f.Cut.InvokeAsync(async () =>
        {
            if (variant == "conditions") f.Filter.Conditions[0].Param1 = "changed";
            if (variant == "name") editor.FilterSettings.SettingsName = "User name";
            if (variant == "description") editor.Description = "User description";
            if (variant == "switch") editor.OnSetPredefinedFilter(null, "Switched");
            if (variant is "close" or "reopen") await editor.OnCancel();
        });
        if (variant == "reopen") await f.Open();
        f.Api.Failure = variant == "failure";
        f.Api.Release.SetResult();
        await pending;
        Assert.False(editor.IsGeneratingNameDescription);
        Assert.Equal(0, f.Filter.Saves);
        Assert.Equal("alpha", snapshot);
        if (variant == "success")
        {
            Assert.Equal("Generated", editor.FilterSettings.SettingsName);
            Assert.Equal("Semantic summary.", editor.Description);
            Assert.Equal("AiNameDescriptionGenerated", Assert.Single(f.Notifications.Events).Body);
        }
        else
        {
            Assert.NotEqual("Generated", editor.FilterSettings.SettingsName);
            Assert.NotEqual("Semantic summary.", editor.Description);
            if (variant == "failure") Assert.Equal("AiNameDescriptionFailed", Assert.Single(f.Notifications.Events).Body);
            else Assert.Empty(f.Notifications.Events);
        }
        if (variant is "close" or "reopen") Assert.True(f.Api.Token.IsCancellationRequested);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Renamed_saved_filter_becomes_private_writable_copy_only_at_save(bool rename)
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        var original = new RgfFilterSettings { FilterSettingsId = 7, SettingsName = "Original", RoleId = "Shared", IsReadonly = true };
        f.Filter.PredefinedFilters.Add(original);
        editor.FilterSettings.FilterSettingsId = 7;
        editor.FilterSettings.SettingsName = "Original";
        editor.FilterSettings.RoleId = "Shared";
        editor.FilterSettings.IsReadonly = true;
        editor.Description = "Draft description";
        if (rename) editor.FilterSettings.SettingsName = "Renamed";
        Assert.Equal(7, editor.FilterSettings.FilterSettingsId);
        Assert.Equal("Draft description", editor.Description);
        await f.Cut.InvokeAsync(editor.OnSavePredefinedFilter);
        Assert.Equal(rename ? null : 7, f.Filter.Saved!.FilterSettingsId);
        Assert.Equal(rename ? null : "Shared", f.Filter.Saved.RoleId);
        Assert.Equal(!rename, f.Filter.Saved.IsReadonly);
        Assert.Equal("Original", original.SettingsName);
        Assert.Equal("Shared", original.RoleId);
        Assert.Equal(7, original.FilterSettingsId);
    }

    [Fact]
    public async Task Manual_custom_name_keeps_existing_save_as_behavior_without_inherited_sharing()
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        editor.FilterSettings.FilterSettingsId = 7;
        editor.FilterSettings.RoleId = "Shared";
        editor.FilterSettings.IsReadonly = true;
        editor.Description = "Draft";
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(null, "Custom"));
        Assert.Null(editor.FilterSettings.FilterSettingsId);
        Assert.False(editor.FilterSettings.IsReadonly);
        Assert.Null(editor.FilterSettings.RoleId);
        Assert.Equal("Draft", editor.Description);
    }

    [Fact]
    public async Task Empty_filter_cannot_start_generation()
    {
        using var f = new Fixture();
        await f.Open();
        f.Filter.Conditions.Clear();
        Assert.False(f.Cut.Instance.CanGenerateNameDescription);
        await f.Cut.InvokeAsync(() => f.Cut.Instance.GenerateNameDescriptionAsync());
        Assert.Equal(0, f.Api.Calls);
        f.Filter.Conditions.Add(new() { Conditions = [] });
        Assert.False(f.Cut.Instance.CanGenerateNameDescription);
        f.Filter.Conditions[0].Conditions.Add(new() { Alias = "Name" });
        Assert.True(f.Cut.Instance.CanGenerateNameDescription);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly BunitContext context = new();
        internal readonly Api Api = new();
        internal readonly FilterProxy Filter;
        internal readonly NotificationsProxy Notifications;
        internal readonly RgfEntityParameters Parameters = new("Orders") { DialogTemplate = _ => _ => { } };
        internal readonly IRenderedComponent<RgfFilterComponent> Cut;
        internal Fixture()
        {
            var filter = DispatchProxy.Create<IRgFilterHandler, FilterProxy>();
            Filter = (FilterProxy)filter;
            var notifications = DispatchProxy.Create<IRgfNotificationManager, NotificationsProxy>();
            Notifications = (NotificationsProxy)notifications;
            var manager = DispatchProxy.Create<IRgManager, ManagerProxy>();
            ((ManagerProxy)manager).Filter = filter;
            ((ManagerProxy)manager).Notifications = notifications;
            typeof(RgfEntityParameters).GetProperty(nameof(RgfEntityParameters.Manager))!.SetValue(Parameters, manager);
            context.Services.AddLogging();
            context.Services.AddSingleton<IRgfApiService>(Api);
            context.Services.AddSingleton(DispatchProxy.Create<IRecroDictService, DictionaryProxy>());
            context.Services.AddSingleton(DispatchProxy.Create<IRecroSecService, SecurityProxy>());
            Cut = context.Render<RgfFilterComponent>(p => p.Add(c => c.EntityParameters, Parameters)
                .Add(c => c.FilterTemplate, (RenderFragment<RgfFilterComponent>)(_ => _ => { }))
                .Add(c => c.FooterTemplate, (RenderFragment<RgfFilterComponent>)(_ => _ => { })));
        }
        internal Task Open() => Cut.InvokeAsync(() => Parameters.ToolbarParameters.EventDispatcher.DispatchEventAsync(
            RgfToolbarEventKind.ShowFilter, new RgfEventArgs<RgfToolbarEventArgs>(this, new(RgfToolbarEventKind.ShowFilter))));
        public void Dispose() => context.Dispose();
    }

    private sealed class Api : IRgfApiService
    {
        internal int Calls;
        internal bool Failure;
        internal CancellationToken Token;
        internal RgfNameDescriptionRequest? Request;
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IRgfApiResponse<T>> GetAsync<T>(IRgfApiRequest request) where T : class => throw new NotSupportedException();
        public async Task<IRgfApiResponse<T>> PostAsync<T>(IRgfApiRequest request) where T : class
        {
            Calls++;
            Token = request.CancellationToken;
            Request = JsonSerializer.Deserialize<RgfNameDescriptionRequest>(await request.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await Release.Task;
            return (IRgfApiResponse<T>)(object)new ApiResponse<RgfNameDescriptionResponse>
            { Success = !Failure, Result = new("Generated", "Semantic summary.") };
        }
    }
    public class FilterProxy : DispatchProxy
    {
        public List<RgfFilter.Condition> Conditions { get; } = [new() { Alias = "Name", Param1 = "alpha", LogicalOperator = RgfFilter.LogicalOperator.And, QueryOperator = RgfFilter.QueryOperator.Equal }];
        public List<RgfFilterSettings> PredefinedFilters { get; } = [];
        public int Saves;
        public RgfFilterSettings? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_Conditions" => Conditions,
            "get_PredefinedFilters" => PredefinedFilters,
            "get_RgfFilterProperties" => Array.Empty<RgfFilterProperty>(),
            "ResetFilter" => true,
            "SaveFilterSettingsAsync" => Save((RgfFilterSettings)args![0]!),
            _ => throw new NotSupportedException(method.Name)
        };
        private Task<bool> Save(RgfFilterSettings value) { Saves++; Saved = RgfFilterSettings.DeepCopy(value); return Task.FromResult(true); }
    }
    public class ManagerProxy : DispatchProxy
    {
        public IRgFilterHandler Filter = null!;
        public IRgfNotificationManager Notifications = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_EntityDesc" => new RgfEntity { EntityName = "Orders", EntityVersion = "1", MenuTitle = "Orders", Permissions = new(true) },
            "get_IsFiltered" => false,
            "GetFilterHandlerAsync" => Task.FromResult(Filter),
            "get_ListHandler" => DispatchProxy.Create<IRgListHandler, ListProxy>(),
            "get_ToastManager" => Notifications,
            "CreateGridRequest" => RgfGridRequest.Create(new() { SessionId = "session", GridId = "grid" }),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public class ListProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_SQLTimeout" ? null : throw new NotSupportedException();
    }
    public class NotificationsProxy : DispatchProxy
    {
        public readonly List<RgfToastEventArgs> Events = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != "RaiseEventAsync") throw new NotSupportedException();
            Events.Add((RgfToastEventArgs)args![0]!);
            return Task.CompletedTask;
        }
    }
    public class DictionaryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "GetRgfUiString" ? args![0] : throw new NotSupportedException();
    }
    public class SecurityProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Roles" ? new Dictionary<string, string>() : throw new NotSupportedException();
    }
}
