using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfFilterComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfFilterComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    [Inject]
    private IRecroSecService _recroSec { get; set; } = null!;

    [Inject]
    private IServiceProvider _serviceProvider { get; set; } = null!;

    public RgfFilterParameters FilterParameters { get; private set; } = default!;

    public RgfFilter.Condition? Condition { get; private set; }

    public RgfFilterSettings FilterSettings { get; private set; } = new();

    // RGF-DOC: rgf.client.blazor.filtering
    /// <summary>Whether to show the Ignore case checkbox for supported operators.</summary>
    public bool AllowFilterToLower { get; private set; } = true;

    /// <summary>Editable description saved with the filter settings.</summary>
    public string? Remark { get => FilterSettings.Remark; set => FilterSettings.Remark = value ?? ""; }
    public bool IsGeneratingNameDescription => _generation is not null;
    public bool CanGenerateNameDescription => !IsGeneratingNameDescription && HasConditions(FilterHandler.Conditions);

    private static bool HasConditions(IEnumerable<RgfFilter.Condition> conditions)
        => conditions.Any(condition => condition.Conditions is null || HasConditions(condition.Conditions));
    private CancellationTokenSource? _generation;
    private int _generationVersion;
    private bool _disposed;
    private IRgManager? _draftManager;
    private RgfEntityParameters? _subscribedEntity;
    private RgfEventDispatcher<RgfFilterEventKind, RgfFilterEventArgs>? _aiFilterDispatcher;
    private int _editorVersion;
    private DraftState? _draftBaseline;
    private static readonly JsonSerializerOptions GenerationJson = new(JsonSerializerDefaults.Web);

    private sealed record DraftState(string Conditions, int? Id, string? Name, string? Remark, string? Role,
        bool? Readonly, int? Timeout, bool Active);

    private DraftState CaptureDraft() => new(JsonSerializer.Serialize(DraftConditions(Condition?.Conditions ?? []), GenerationJson),
        FilterSettings.FilterSettingsId, FilterSettings.SettingsName, Remark, FilterSettings.RoleId,
        FilterSettings.IsReadonly, FilterSettings.SQLTimeout, IsFilterActive);

    // Rendering the Ignore case checkbox can initialize a missing value to false.
    private static IEnumerable<object> DraftConditions(IEnumerable<RgfFilter.Condition> conditions) => conditions.Select(condition => new
    {
        condition.Id, condition.Alias, condition.LogicalOperator, condition.QueryOperator, condition.IsQuickFilter,
        condition.Param1,
        Param2 = condition.Param2 ?? (condition.QueryOperator is RgfFilter.QueryOperator.Like or RgfFilter.QueryOperator.NotLike ? (object)false : null),
        Conditions = condition.Conditions is null ? null : DraftConditions(condition.Conditions)
    });

    public bool IsSavingAsNewFilter => FilterSettings.FilterSettingsId is > 0 &&
        FilterHandler.PredefinedFilters.Any(f => f.FilterSettingsId == FilterSettings.FilterSettingsId &&
            !string.Equals(f.SettingsName, FilterSettings.SettingsName, StringComparison.Ordinal));

    private void InvalidateGeneration()
    {
        _generationVersion++;
        _generation?.Cancel();
    }

    public async Task GenerateNameDescriptionAsync(string? provider = null, string? model = null)
    {
        if (!_showComponent || !CanGenerateNameDescription) return;
        using var cancellation = new CancellationTokenSource();
        _generation = cancellation;
        var version = _generationVersion;
        var settings = FilterSettings;
        var manager = Manager;
        var handler = FilterHandler;
        var name = settings.SettingsName;
        var description = Remark;
        var needsName = string.IsNullOrWhiteSpace(name);
        string? snapshot = null;
        try
        {
            // Serialize before awaiting: never call StoreFilter, which changes the handler's reset baseline.
            snapshot = JsonSerializer.Serialize(handler.Conditions, GenerationJson);
            var grid = manager.CreateGridRequest();
            var data = new RgfFilterNameDescriptionData
            {
                SessionId = grid.SessionId, GridId = grid.GridId,
                Conditions = JsonSerializer.Deserialize<RgfFilter.Condition[]>(snapshot, GenerationJson)!
            };
            var request = new RgfNameDescriptionRequest
            {
                ObjectType = "Filter", Target = needsName ? RgfNameDescriptionTarget.Both : RgfNameDescriptionTarget.Description,
                ObjectData = JsonSerializer.SerializeToElement(data, GenerationJson),
                ExistingName = name, ExistingDescription = description,
                AiProviderOverride = provider, AiModelOverride = model
            };
            var response = await _generationApi.PostAsync<RgfNameDescriptionResponse, RgfNameDescriptionRequest>(
                "/api/rgf/ai/name-description/generate", request, cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!IsCurrent()) return;
            var result = response.Result;
            if (!response.Success || result is null ||
                (needsName && (string.IsNullOrWhiteSpace(result.Name) || result.Name.Length > 200)) ||
                string.IsNullOrWhiteSpace(result.Description))
                throw new InvalidOperationException("Invalid name/description response.");
            var existing = description ?? "";
            var generated = result.Description;
            var combined = existing;
            if (!existing.Replace("\r\n", "\n").Contains(generated.Replace("\r\n", "\n").Trim(), StringComparison.Ordinal))
            {
                var separator = string.IsNullOrWhiteSpace(existing) ? "" : "\n\n---\n\n";
                var available = Math.Max(0, 4000 - existing.Length - separator.Length);
                var length = Math.Min(available, generated.Length);
                if (length > 0 && length < generated.Length && char.IsHighSurrogate(generated[length - 1]) && char.IsLowSurrogate(generated[length])) length--;
                if (length > 0) combined += separator + generated[..length];
            }
            if (needsName) settings.SettingsName = result.Name!;
            Remark = combined;
            await GenerationToastAsync(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Filter name/description generation failed.");
            if (IsCurrent()) await GenerationToastAsync(false);
        }
        finally
        {
            if (ReferenceEquals(_generation, cancellation)) _generation = null;
            if (!_disposed) await InvokeAsync(StateHasChanged);
        }

        bool IsCurrent() => _showComponent && ReferenceEquals(manager, Manager) && ReferenceEquals(handler, FilterHandler) && version == _generationVersion && ReferenceEquals(settings, FilterSettings)
            && name == settings.SettingsName && description == Remark &&
            (snapshot is null || snapshot == JsonSerializer.Serialize(FilterHandler.Conditions, GenerationJson));
    }

    [Inject] private IRgfApiService _generationApi { get; set; } = null!;

    private Task GenerationToastAsync(bool success) => Manager.ToastManager.RaiseEventAsync(
        new RgfToastEventArgs(Manager.EntityDesc.MenuTitle,
            _recroDict.GetRgfRecrobyString(success ? "AiNameDescriptionGenerated" : "AiNameDescriptionFailed"),
            success ? RgfToastType.Success : RgfToastType.Error, delay: 3000), this);

    public bool IsPublicFilterSettingAllowed => Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.PublicFilterSetting);

    public RgfFilterProperty[] RgfFilterProperties => FilterHandler.RgfFilterProperties;

    /// <summary>Resolves an editor condition only when its current ID and alias agree.</summary>
    public RgfFilterProperty? GetConditionProperty(RgfFilter.Condition condition)
    {
        var matches = RgfFilterProperties.Where(p => p.Id == condition.Id &&
            (string.IsNullOrWhiteSpace(condition.Alias) || string.Equals(p.Alias, condition.Alias, StringComparison.OrdinalIgnoreCase))).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>Indicates that the editor contains conditions unavailable in the current Filter metadata.</summary>
    public bool HasUnresolvedConditions => FilterHandler is not null && HasUnresolved(FilterHandler.Conditions);

    private bool HasUnresolved(IEnumerable<RgfFilter.Condition> conditions) => conditions.Any(c =>
        c.Conditions is not null ? HasUnresolved(c.Conditions) : GetConditionProperty(c) is null);

    public IReadOnlyDictionary<string, string> VisibleRoles => new[] { new KeyValuePair<string, string>("", "") }.Concat(_recroSec.Roles).ToDictionary(kv => kv.Key, kv => kv.Value);

    public void AddCondition(RgfFilter.Condition condition) { FilterHandler.AddCondition(_logger, condition.ClientId); IsFilterActive = true; }

    public void RemoveCondition(RgfFilter.Condition condition) { FilterHandler.RemoveCondition(condition.ClientId); IsFilterActive = true; }

    public void AddBracket(RgfFilter.Condition condition) { FilterHandler.AddBracket(condition.ClientId); IsFilterActive = true; }

    public void RemoveBracket(RgfFilter.Condition condition) { FilterHandler.RemoveBracket(condition.ClientId); IsFilterActive = true; }

    public bool ChangeProperty(RgfFilter.Condition condition, int newPropertyId) { IsFilterActive = true; return FilterHandler.ChangeProperty(condition, newPropertyId); }

    public bool ChangeQueryOperator(RgfFilter.Condition condition, RgfFilter.QueryOperator newOperator) { IsFilterActive = true; return FilterHandler.ChangeQueryOperator(_logger, condition, newOperator); }

    private EditContext _emptyEditContext = new(new object());

    public bool IsFilterActive { get; set; } = true;

    private IRgManager Manager => EntityParameters.Manager!;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private bool _showComponent { get; set; } = false;

    private RenderFragment? _filterDialog { get; set; }

    public IRgFilterHandler FilterHandler { get; private set; } = null!;

    private void SetFilterParameters()
    {
        FilterParameters = EntityParameters.FilterParameters;
        FilterParameters.DialogParameters.Title = _recroDict.GetRgfUiString("Filter");
        FilterParameters.DialogParameters.ShowCloseButton = true;
        FilterParameters.DialogParameters.ContentTemplate = RenderEditorSection(FilterTemplate(this));
        FilterParameters.DialogParameters.FooterTemplate = RenderEditorSection(FooterTemplate(this));
        FilterParameters.DialogParameters.Resizable = FilterParameters.DialogParameters.Resizable ?? true;
        FilterParameters.DialogParameters.Width ??= "700px";
        FilterParameters.DialogParameters.Height ??= "400px";
    }

    protected override async Task OnParametersSetAsync()
    {
        var entityChanged = !ReferenceEquals(_subscribedEntity, EntityParameters);
        var filterParametersChanged = !ReferenceEquals(FilterParameters, EntityParameters.FilterParameters);
        if (_showComponent && (entityChanged || filterParametersChanged || !ReferenceEquals(_draftManager, EntityParameters.Manager)))
        {
            await CloseDialogAsync();
        }
        if (entityChanged)
        {
            _subscribedEntity?.UnsubscribeFromAll(this);
            _subscribedEntity = EntityParameters;
            EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.ShowFilter, OnShowFilter, true);
        }
        if (entityChanged || filterParametersChanged)
        {
            SetFilterParameters();
        }
    }

    protected virtual async Task OnShowFilter(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        _logger.LogDebug("OnShowFilter | EntityName:{EntityName}", EntityParameters.EntityName);
        var manager = Manager;
        var entity = EntityParameters;
        if (manager is null)
        {
            return;
        }
        var capabilities = _serviceProvider.GetService<RgfRecrobyCapabilityService>();
        AllowFilterToLower = capabilities is null || await capabilities.GetAllowFilterToLowerAsync();
        var handler = await manager.GetFilterHandlerAsync();
        if (_disposed || !ReferenceEquals(entity, EntityParameters) || !ReferenceEquals(manager, EntityParameters.Manager))
        {
            return;
        }
        StopWatchingFilter();
        if (!ReferenceEquals(_draftManager, manager))
        {
            _draftManager = manager;
            FilterSettings = new RgfFilterSettings();
        }
        FilterHandler = handler;
        if (FilterHandler.TakeNewAiFilterSettings() is { } newAiSettings)
        {
            FilterSettings = newAiSettings;
        }
        // Capture the current shared filter, then detach the editable objects from applied requests.
        FilterHandler.StoreFilter();
        FilterHandler.ResetFilter();
        Condition = new RgfFilter.Condition() { Conditions = FilterHandler.Conditions };
        IsFilterActive = Manager.IsFiltered || !FilterHandler.Conditions.Any();
        Open();
        args.Handled = true;
    }

    private void Open()
    {
        InvalidateGeneration();
        FilterParameters.DialogParameters.UniqueName = "filter-" + Manager.EntityDesc.NameVersion.ToLower();
        FilterParameters.DialogParameters.EventDispatcher.Unsubscribe(this);//We'll reset it in case the dialog might have overwritten it
        FilterParameters.DialogParameters.EventDispatcher.Subscribe(RgfDialogEventKind.Close, OnDialogCloseAsync, true);
        if (EntityParameters.DialogTemplate != null)
        {
            _filterDialog = EntityParameters.DialogTemplate(FilterParameters.DialogParameters);
        }
        else
        {
            _filterDialog = RgfDynamicDialog.Create(FilterParameters.DialogParameters, _logger);
        }
        FilterSettings.SQLTimeout = Manager.ListHandler.SQLTimeout;
        _draftBaseline = CaptureDraft();
        _aiFilterDispatcher = FilterParameters.EventDispatcher;
        _aiFilterDispatcher.Subscribe(RgfFilterEventKind.AiFilterApplied, OnAiFilterAppliedAsync);
        if (_draftManager is RgManager manager)
        {
            manager.Initializing += OnManagerInitializing;
        }
        _showComponent = true;
        StateHasChanged();
    }

    private void StopWatchingFilter()
    {
        _aiFilterDispatcher?.Unsubscribe(RgfFilterEventKind.AiFilterApplied, OnAiFilterAppliedAsync);
        _aiFilterDispatcher = null;
        if (_draftManager is RgManager manager)
        {
            manager.Initializing -= OnManagerInitializing;
        }
    }

    private void OnManagerInitializing(RgManager manager) => _ = InvokeAsync(() =>
        !_disposed && _showComponent && ReferenceEquals(_draftManager, manager) ? CloseDialogAsync() : Task.CompletedTask);

    private Task OnAiFilterAppliedAsync(IRgfEventArgs<RgfFilterEventArgs> args) =>
        args.Sender is IRgFilterHandler handler ? SyncAiFilterAsync(handler) : Task.CompletedTask;

    // RGF-DOC: rgf.client.recroby.integration
    private async Task SyncAiFilterAsync(IRgFilterHandler handler)
    {
        try
        {
            await InvokeAsync(async () =>
            {
                if (_disposed || !_showComponent || !ReferenceEquals(_draftManager, EntityParameters.Manager) ||
                    !ReferenceEquals(handler, FilterHandler) || !ReferenceEquals(_subscribedEntity, EntityParameters) ||
                    !ReferenceEquals(_aiFilterDispatcher, EntityParameters.FilterParameters.EventDispatcher) ||
                    Manager.ListHandler is not { Initialized: true } ||
                    handler.TakeNewAiFilterSettings() is not { } settings)
                {
                    return;
                }

                var discardedEdits = CaptureDraft() != _draftBaseline;
                InvalidateGeneration();
                _generation = null;
                Condition = new RgfFilter.Condition { Conditions = handler.Conditions };
                FilterSettings = settings;
                FilterSettings.SQLTimeout = Manager.ListHandler.SQLTimeout;
                IsFilterActive = Manager.IsFiltered || !handler.Conditions.Any();
                _draftBaseline = CaptureDraft();
                _editorVersion++;
                StateHasChanged();

                if (discardedEdits)
                {
                    await Manager.ToastManager.RaiseEventAsync(new RgfToastEventArgs(Manager.EntityDesc.MenuTitle,
                        _recroDict.GetRgfRecrobyString("FilterDraftReplaced"), RgfToastType.Warning), this);
                }
            });
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Could not synchronize the open filter editor.");
        }
    }

    private Task OnDialogCloseAsync(IRgfEventArgs<RgfDialogEventArgs> args) => OnCancel();

    private async Task CloseDialogAsync()
    {
        _logger.LogDebug("CloseDialog | EntityName:{EntityName}", EntityParameters.EntityName);
        InvalidateGeneration();
        StopWatchingFilter();
        _showComponent = false;
        FilterParameters.DialogParameters.EventDispatcher.Unsubscribe(this);
        await FilterParameters.DialogParameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Destroy, this);
        StateHasChanged();
    }

    public Task RecreateAsync() => InvokeAsync(async () =>
    {
        await CloseDialogAsync();
        if (!_disposed && ReferenceEquals(_draftManager, EntityParameters.Manager))
        {
            Open();
        }
    });

    public virtual Task OnCancel(MouseEventArgs? args = null)
    {
        FilterHandler.ResetFilter();
        return CloseDialogAsync();
    }

    public virtual async Task OnOk(MouseEventArgs? args = null)
    {
        if (HasUnresolvedConditions) return;
        _logger.LogDebug("OnOk | EntityName:{EntityName}", EntityParameters.EntityName);
        await CloseDialogAsync();
        var conditions = IsFilterActive ? FilterHandler.StoreFilter() : [];
        await Manager.ListHandler.SetFilterAsync(conditions, FilterSettings.SQLTimeout);
    }

    private Task HandleKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            return OnOk();
        }
        return Task.CompletedTask;
    }

    public virtual bool OnSetPredefinedFilter(int? filterSettingsId, string name)
    {
        InvalidateGeneration();
        _logger.LogDebug("SetPredefinedFilter | {id}:{name}", filterSettingsId, name);
        RgfFilterSettings? predefFilter = null;
        if (filterSettingsId > 0)
        {
            predefFilter = FilterHandler.SelectPredefinedFilter(filterSettingsId);
            if (predefFilter != null)
            {
                FilterSettings = predefFilter;
                Condition = new RgfFilter.Condition() { Conditions = FilterHandler.Conditions };
                _ = Manager.ToastManager.RaiseEventAsync(new RgfToastEventArgs(Manager.EntityDesc.MenuTitle, RgfToastEventArgs.ActionTemplate(_recroDict.GetRgfUiString("Load"), FilterSettings.SettingsName), RgfToastType.Success, delay: 2000), this);
                return true;
            }
        }

        FilterSettings = RgfFilterSettings.DeepCopy(FilterSettings);
        FilterSettings.SettingsName = name;
        FilterSettings.FilterSettingsId = null;
        FilterSettings.IsReadonly = false;
        FilterSettings.RoleId = null;
        return false;
    }

    public virtual async Task OnSavePredefinedFilter()
    {
        if (HasUnresolvedConditions) return;
        if (IsSavingAsNewFilter)
        {
            FilterSettings = RgfFilterSettings.DeepCopy(FilterSettings);
            FilterSettings.FilterSettingsId = null;
            FilterSettings.IsReadonly = false;
            FilterSettings.RoleId = null;
        }
        var settings = FilterSettings;
        var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, _recroDict.GetRgfUiString("SaveSettings"), settings.SettingsName);
        await Manager.ToastManager.RaiseEventAsync(toast, this);
        if (_disposed || !ReferenceEquals(settings, FilterSettings) || !ReferenceEquals(_draftManager, EntityParameters.Manager))
        {
            return;
        }
        if (await FilterHandler.SaveFilterSettingsAsync(settings))
        {
            await Manager.ToastManager.RaiseEventAsync(toast.RecreateAsSuccess(_recroDict.GetRgfUiString("Processed")), this);
            StateHasChanged();
        }
    }

    public void OnDeletePredefinedFilter() => _dynamicDialog.PromptDeletionConfirmation(DeletePredefinedFilter, $"{_recroDict.GetRgfUiString("Filter")}: {FilterSettings.SettingsName}");

    public virtual async Task<bool> DeletePredefinedFilter()
    {
        if (FilterSettings.FilterSettingsId != null && FilterSettings.FilterSettingsId != 0)
        {
            var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, _recroDict.GetRgfUiString("Delete"), FilterSettings.SettingsName);
            await Manager.ToastManager.RaiseEventAsync(toast, this);
            if (await FilterHandler.DeleteFilterSettingsAsync(FilterSettings.FilterSettingsId.Value))
            {
                FilterSettings = new() { SettingsName = "", FilterSettingsId = null };
                await Manager.ToastManager.RaiseEventAsync(toast.Recreate(_recroDict.GetRgfUiString("Processed"), RgfToastType.Info), this);
                StateHasChanged();
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        _disposed = true;
        StopWatchingFilter();
        InvalidateGeneration();
        _subscribedEntity?.UnsubscribeFromAll(this);
    }
}
