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
        editor.Remark = "Existing";
        if (variant == "switch") f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "Switched", Remark = "Other description" });
        var pending = f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync("provider", "model"));
        f.Cut.WaitForAssertion(() => Assert.True(editor.IsGeneratingNameDescription));
        await f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync());
        Assert.Equal(1, f.Api.Calls);
        Assert.Equal("Before", f.Api.Request!.ExistingName);
        Assert.Equal(RgfNameDescriptionTarget.Description, f.Api.Request.Target);
        Assert.Equal("Existing", f.Api.Request.ExistingDescription);
        Assert.Equal("provider", f.Api.Request.AiProviderOverride);
        Assert.Equal("model", f.Api.Request.AiModelOverride);
        var snapshot = f.Api.Request.ObjectData.GetProperty("conditions")[0].GetProperty("param1").GetString();
        Assert.Equal("alpha", snapshot);
        await f.Cut.InvokeAsync(async () =>
        {
            if (variant == "conditions") f.Filter.Conditions[0].Param1 = "changed";
            if (variant == "name") editor.FilterSettings.SettingsName = "User name";
            if (variant == "description") editor.Remark = "User description";
            if (variant == "switch") editor.OnSetPredefinedFilter(7, "Switched");
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
            Assert.Equal("Before", editor.FilterSettings.SettingsName);
            Assert.Equal("Existing\n\n---\n\nSemantic summary.", editor.Remark);
            Assert.Equal("RGF.Recroby.UI.AiNameDescriptionGenerated", Assert.Single(f.Notifications.Events).Body);
        }
        else
        {
            Assert.NotEqual("Generated", editor.FilterSettings.SettingsName);
            Assert.NotEqual("Semantic summary.", editor.Remark);
            if (variant == "switch") Assert.Equal("Other description", editor.Remark);
            if (variant == "failure") Assert.Equal("RGF.Recroby.UI.AiNameDescriptionFailed", Assert.Single(f.Notifications.Events).Body);
            else if (variant == "switch") Assert.Single(f.Notifications.Events);
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
        editor.Remark = "Draft description";
        if (rename) editor.FilterSettings.SettingsName = "Renamed";
        Assert.Equal(7, editor.FilterSettings.FilterSettingsId);
        Assert.Equal("Draft description", editor.Remark);
        await f.Cut.InvokeAsync(editor.OnSavePredefinedFilter);
        Assert.Equal(rename ? null : 7, f.Filter.Saved!.FilterSettingsId);
        Assert.Equal(rename ? null : "Shared", f.Filter.Saved.RoleId);
        Assert.Equal("Draft description", f.Filter.Saved.Remark);
        Assert.Equal(!rename, f.Filter.Saved.IsReadonly);
        Assert.Equal("Original", original.SettingsName);
        Assert.Equal("Shared", original.RoleId);
        Assert.Equal(7, original.FilterSettingsId);
    }

    [Fact]
    public async Task Generated_description_is_saved_and_reloaded()
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        var pending = f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync());
        f.Api.Release.SetResult();
        await pending;
        await f.Cut.InvokeAsync(editor.OnSavePredefinedFilter);
        Assert.Equal("Generated", f.Filter.Saved!.SettingsName);
        Assert.Equal("Semantic summary.", f.Filter.Saved.Remark);
        editor.FilterSettings.FilterSettingsId = 7;
        f.Filter.Saved.FilterSettingsId = 7;
        f.Filter.PredefinedFilters.Add(RgfFilterSettings.DeepCopy(f.Filter.Saved));
        await f.Cut.InvokeAsync(() => editor.OnCancel());
        await f.Open();
        Assert.Equal("Generated", editor.FilterSettings.SettingsName);
        Assert.Equal("Semantic summary.", editor.Remark);
    }

    [Fact]
    public async Task Failed_save_does_not_replace_saved_description()
    {
        using var f = new Fixture();
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "Original", Remark = "Saved" });
        f.Filter.FailSave = true;
        await f.Open();
        var editor = f.Cut.Instance;
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "Original"));
        editor.Remark = "Changed";
        await f.Cut.InvokeAsync(editor.OnSavePredefinedFilter);
        await f.Cut.InvokeAsync(() => editor.OnCancel());
        await f.Open();
        Assert.Equal("Changed", editor.Remark);
        Assert.Equal("Saved", f.Filter.PredefinedFilters[0].Remark);
    }

    [Fact]
    public async Task Saved_description_is_detached_restored_and_can_be_cleared()
    {
        using var f = new Fixture();
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "Original", Remark = "Saved" });
        await f.Open();
        var editor = f.Cut.Instance;
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "Original"));
        Assert.Equal("Saved", editor.Remark);
        editor.Remark = "Discarded";
        await f.Cut.InvokeAsync(() => editor.OnCancel());
        await f.Open();
        Assert.Equal("Discarded", editor.Remark);
        Assert.Equal("Saved", f.Filter.PredefinedFilters[0].Remark);
        editor.Remark = null;
        await f.Cut.InvokeAsync(editor.OnSavePredefinedFilter);
        Assert.Equal("", f.Filter.Saved!.Remark);
    }

    [Theory]
    [InlineData(null, null, RgfNameDescriptionTarget.Both, "Generated", "Semantic summary.")]
    [InlineData(null, "Existing", RgfNameDescriptionTarget.Both, "Generated", "Existing\n\n---\n\nSemantic summary.")]
    [InlineData("Named", null, RgfNameDescriptionTarget.Description, "Named", "Semantic summary.")]
    [InlineData("Named", "Existing", RgfNameDescriptionTarget.Description, "Named", "Existing\n\n---\n\nSemantic summary.")]
    public async Task Generation_selects_target_and_extends_remark(string? name, string? remark, RgfNameDescriptionTarget target, string expectedName, string expectedRemark)
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        editor.FilterSettings.SettingsName = name!;
        editor.FilterSettings.Remark = remark!;
        f.Api.ResponseName = target == RgfNameDescriptionTarget.Description ? null : "Generated";
        var pending = f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync());
        f.Api.Release.SetResult();
        await pending;
        Assert.Equal(target, f.Api.Request!.Target);
        Assert.Equal(1, f.Api.Calls);
        Assert.Equal(expectedName, editor.FilterSettings.SettingsName);
        Assert.Equal(expectedRemark, editor.Remark);
        Assert.Equal(0, f.Filter.Saves);
    }

    [Theory]
    [InlineData(3980)]
    [InlineData(3981)]
    [InlineData(3992)]
    [InlineData(3993)]
    [InlineData(4000)]
    public async Task Generated_remark_respects_combined_length_limit(int length)
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        editor.FilterSettings.SettingsName = "Name";
        editor.Remark = new string('x', length);
        f.Api.ResponseDescription = new string('y', 18);
        var pending = f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync());
        f.Api.Release.SetResult();
        await pending;
        var expected = new string('x', length);
        if (length < 3993) expected += "\n\n---\n\n" + new string('y', Math.Min(18, 4000 - length - 7));
        Assert.Equal(expected, editor.Remark);
        Assert.Equal("RGF.Recroby.UI.AiNameDescriptionGenerated", Assert.Single(f.Notifications.Events).Body);
    }

    [Fact]
    public async Task Identical_generated_remark_is_not_duplicated()
    {
        using var f = new Fixture();
        await f.Open();
        f.Cut.Instance.Remark = "Semantic summary.";
        var pending = f.Cut.InvokeAsync(() => f.Cut.Instance.GenerateNameDescriptionAsync());
        f.Api.Release.SetResult();
        await pending;
        Assert.Equal("Semantic summary.", f.Cut.Instance.Remark);
    }

    [Fact]
    public async Task Switching_filters_loads_saved_text_without_changing_originals()
    {
        using var f = new Fixture();
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "First", Remark = "First saved" });
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 8, SettingsName = "Second", Remark = "Second saved" });
        await f.Open();
        var editor = f.Cut.Instance;
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "First"));
        editor.Remark = "First draft";
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(8, "Second"));
        Assert.Equal("Second saved", editor.Remark);
        editor.Remark = "Second draft";
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "First"));
        Assert.Equal("First saved", editor.Remark);
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(8, "Second"));
        Assert.Equal("Second saved", editor.Remark);
        Assert.Equal("First saved", f.Filter.PredefinedFilters[0].Remark);
        Assert.Equal(0, f.Filter.Saves);
    }

    [Theory]
    [InlineData("Before\r\nFirst\r\nSecond\r\nAfter", "  First\nSecond  ", false)]
    [InlineData("Before First part after", "First part plus more", true)]
    public async Task Duplicate_check_uses_all_text_and_normalizes_line_endings(string existing, string generated, bool append)
    {
        using var f = new Fixture();
        await f.Open();
        f.Cut.Instance.Remark = existing;
        f.Api.ResponseDescription = generated;
        var pending = f.Cut.InvokeAsync(() => f.Cut.Instance.GenerateNameDescriptionAsync());
        f.Api.Release.SetResult();
        await pending;
        Assert.Equal(append ? existing + "\n\n---\n\n" + generated : existing, f.Cut.Instance.Remark);
    }

    [Fact]
    public async Task Cancel_and_reopen_preserve_text_and_reset_conditions()
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        editor.FilterSettings.SettingsName = "Unsaved";
        editor.Remark = "Draft";
        var resets = f.Filter.Resets;
        await f.Cut.InvokeAsync(() => editor.OnCancel());
        Assert.Equal(resets + 1, f.Filter.Resets);
        await f.Open();
        Assert.Equal("Unsaved", editor.FilterSettings.SettingsName);
        Assert.Equal("Draft", editor.Remark);
        Assert.Equal(0, f.Filter.Saves);
    }

    [Fact]
    public async Task New_manager_does_not_inherit_previous_grid_text()
    {
        using var f = new Fixture();
        await f.Open();
        f.Cut.Instance.FilterSettings.SettingsName = "Old grid";
        f.Cut.Instance.Remark = "Old remark";
        await f.Cut.InvokeAsync(() => f.Cut.Instance.OnCancel());
        f.ReplaceManager();
        await f.Open();
        Assert.Null(f.Cut.Instance.FilterSettings.SettingsName);
        Assert.Null(f.Cut.Instance.Remark);
    }

    [Fact]
    public async Task Successful_save_replaces_draft_with_saved_state()
    {
        using var f = new Fixture();
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "First", Remark = "Old" });
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 8, SettingsName = "Second" });
        f.Filter.UpdateListOnSave = true;
        await f.Open();
        var editor = f.Cut.Instance;
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "First"));
        editor.Remark = "Saved change";
        await f.Cut.InvokeAsync(editor.OnSavePredefinedFilter);
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(8, "Second"));
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "First"));
        Assert.Equal("Saved change", editor.Remark);
        Assert.Equal("Saved change", f.Filter.PredefinedFilters.Single(x => x.FilterSettingsId == 7).Remark);
    }

    [Fact]
    public async Task Deleting_filter_removes_its_text_draft()
    {
        using var f = new Fixture();
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "First", Remark = "Saved" });
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 8, SettingsName = "Second" });
        await f.Open();
        var editor = f.Cut.Instance;
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "First"));
        editor.Remark = "Draft";
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(8, "Second"));
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "First"));
        Assert.True(await f.Cut.InvokeAsync(editor.DeletePredefinedFilter));
        f.Filter.PredefinedFilters.Add(new() { FilterSettingsId = 7, SettingsName = "Replacement", Remark = "Replacement remark" });
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(7, "Replacement"));
        Assert.Equal("Replacement remark", editor.Remark);
    }

    [Fact]
    public async Task Overflow_with_empty_name_keeps_remark_and_accepts_generated_name()
    {
        using var f = new Fixture();
        await f.Open();
        var editor = f.Cut.Instance;
        editor.Remark = new string('x', 4000);
        var pending = f.Cut.InvokeAsync(() => editor.GenerateNameDescriptionAsync());
        f.Api.Release.SetResult();
        await pending;
        Assert.Equal("Generated", editor.FilterSettings.SettingsName);
        Assert.Equal(new string('x', 4000), editor.Remark);
    }

    [Fact]
    public void Description_survives_copy_and_json_round_trip_and_missing_field_is_compatible()
    {
        var settings = new RgfFilterSettings { SettingsName = "Name", Remark = "Description" };
        Assert.Equal("Description", RgfFilterSettings.DeepCopy(settings).Remark);
        var request = RgfGridRequest.Create(new() { SessionId = "session", GridId = "grid" });
        request.FilterSettings = settings;
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Description", JsonSerializer.Deserialize<RgfGridRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.FilterSettings.Remark);
        Assert.Null(JsonSerializer.Deserialize<RgfFilterSettings>("{\"SettingsName\":\"Old\"}")!.Remark);
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
        editor.Remark = "Draft";
        await f.Cut.InvokeAsync(() => editor.OnSetPredefinedFilter(null, "Custom"));
        Assert.Null(editor.FilterSettings.FilterSettingsId);
        Assert.False(editor.FilterSettings.IsReadonly);
        Assert.Null(editor.FilterSettings.RoleId);
        Assert.Equal("Draft", editor.Remark);
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
        internal void ReplaceManager()
        {
            var manager = DispatchProxy.Create<IRgManager, ManagerProxy>();
            ((ManagerProxy)manager).Filter = (IRgFilterHandler)(object)Filter;
            ((ManagerProxy)manager).Notifications = (IRgfNotificationManager)(object)Notifications;
            typeof(RgfEntityParameters).GetProperty(nameof(RgfEntityParameters.Manager))!.SetValue(Parameters, manager);
        }
        public void Dispose() => context.Dispose();
    }

    private sealed class Api : IRgfApiService
    {
        internal int Calls;
        internal bool Failure;
        internal string? ResponseName = "Generated";
        internal string? ResponseDescription = "Semantic summary.";
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
            { Success = !Failure, Result = new(ResponseName, ResponseDescription) };
        }
    }
    public class FilterProxy : DispatchProxy
    {
        public List<RgfFilter.Condition> Conditions { get; } = [new() { Alias = "Name", Param1 = "alpha", LogicalOperator = RgfFilter.LogicalOperator.And, QueryOperator = RgfFilter.QueryOperator.Equal }];
        public List<RgfFilterSettings> PredefinedFilters { get; } = [];
        public int Saves;
        public int Resets;
        public bool FailSave;
        public bool UpdateListOnSave;
        public RgfFilterSettings? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_Conditions" => Conditions,
            "get_PredefinedFilters" => PredefinedFilters,
            "get_RgfFilterProperties" => new[] { new RgfFilterProperty(new() { Alias = "Name" }, new() { Alias = "Name", RgClass = "rg-text" }) },
            "TakeNewAiFilterSettings" => null,
            "StoreFilter" => Conditions.ToArray(),
            "ResetFilter" => Reset(),
            "SelectPredefinedFilter" => RgfFilterSettings.DeepCopy(PredefinedFilters.SingleOrDefault(f => f.FilterSettingsId == (int?)args![0])),
            "SaveFilterSettingsAsync" => Save((RgfFilterSettings)args![0]!),
            "DeleteFilterSettingsAsync" => Delete((int)args![0]!),
            _ => throw new NotSupportedException(method.Name)
        };
        private Task<bool> Save(RgfFilterSettings value) { Saves++; if (FailSave) return Task.FromResult(false); Saved = RgfFilterSettings.DeepCopy(value); if (UpdateListOnSave) { PredefinedFilters.RemoveAll(x => x.FilterSettingsId == value.FilterSettingsId); PredefinedFilters.Add(RgfFilterSettings.DeepCopy(value)); } return Task.FromResult(true); }
        private bool Reset() { Resets++; return true; }
        private Task<bool> Delete(int id) { PredefinedFilters.RemoveAll(x => x.FilterSettingsId == id); return Task.FromResult(true); }
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
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IRecroDictService.GetRgfUiString) => args![0],
            nameof(IRecroDictService.GetRgfRecrobyString) => $"RGF.Recroby.UI.{args![0]}",
            _ => throw new NotSupportedException()
        };
    }
    public class SecurityProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Roles" ? new Dictionary<string, string>() : throw new NotSupportedException();
    }
}
