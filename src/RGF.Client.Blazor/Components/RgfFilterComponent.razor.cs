using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
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

    public RgfFilterParameters FilterParameters { get; private set; } = default!;

    public RgfFilter.Condition? Condition { get; private set; }

    public RgfFilterSettings FilterSettings { get; private set; } = new();

    // RGF-DOC: rgf.client.blazor.filtering
    /// <summary>Editable description for the current editor session; it is not persisted.</summary>
    public string? Description { get; set; }
    public bool IsGeneratingNameDescription => _generation is not null;
    public bool CanGenerateNameDescription => !IsGeneratingNameDescription && HasConditions(FilterHandler.Conditions);

    private static bool HasConditions(IEnumerable<RgfFilter.Condition> conditions)
        => conditions.Any(condition => condition.Conditions is null || HasConditions(condition.Conditions));
    private CancellationTokenSource? _generation;
    private int _generationVersion;
    private bool _disposed;
    private static readonly JsonSerializerOptions GenerationJson = new(JsonSerializerDefaults.Web);

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
        var description = Description;
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
                ObjectType = "Filter", Target = RgfNameDescriptionTarget.Both,
                ObjectData = JsonSerializer.SerializeToElement(data, GenerationJson),
                ExistingName = name, ExistingDescription = description,
                AiProviderOverride = provider, AiModelOverride = model
            };
            var response = await _generationApi.PostAsync<RgfNameDescriptionResponse, RgfNameDescriptionRequest>(
                "/api/rgf/ai/name-description/generate", request, cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!IsCurrent()) return;
            var result = response.Result;
            if (!response.Success || string.IsNullOrWhiteSpace(result?.Name) || result.Name.Length > 200 ||
                string.IsNullOrWhiteSpace(result.Description) || result.Description.Length > 4000)
                throw new InvalidOperationException("Invalid name/description response.");
            settings.SettingsName = result.Name;
            Description = result.Description;
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
            && name == settings.SettingsName && description == Description &&
            (snapshot is null || snapshot == JsonSerializer.Serialize(FilterHandler.Conditions, GenerationJson));
    }

    [Inject] private IRgfApiService _generationApi { get; set; } = null!;

    private Task GenerationToastAsync(bool success) => Manager.ToastManager.RaiseEventAsync(
        new RgfToastEventArgs(Manager.EntityDesc.MenuTitle,
            _recroDict.GetRgfUiString(success ? "AiNameDescriptionGenerated" : "AiNameDescriptionFailed"),
            success ? RgfToastType.Success : RgfToastType.Error, delay: 3000), this);

    public bool IsPublicFilterSettingAllowed => Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.PublicFilterSetting);

    public RgfFilterProperty[] RgfFilterProperties => FilterHandler.RgfFilterProperties;

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

    protected override void OnInitialized()
    {
        base.OnInitialized();

        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.ShowFilter, OnShowFilter, true);

        FilterParameters = EntityParameters.FilterParameters;
        FilterParameters.DialogParameters.Title = _recroDict.GetRgfUiString("Filter");
        FilterParameters.DialogParameters.UniqueName = "filter-" + Manager.EntityDesc.NameVersion.ToLower();
        FilterParameters.DialogParameters.ShowCloseButton = true;
        FilterParameters.DialogParameters.ContentTemplate = FilterTemplate(this);
        FilterParameters.DialogParameters.FooterTemplate = FooterTemplate(this);
        FilterParameters.DialogParameters.Resizable = FilterParameters.DialogParameters.Resizable ?? true;
        FilterParameters.DialogParameters.Width ??= "700px";
        FilterParameters.DialogParameters.Height ??= "400px";
    }

    protected virtual async Task OnShowFilter(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        _logger.LogDebug("OnShowFilter | EntityName:{EntityName}", EntityParameters.EntityName);
        FilterHandler = await Manager.GetFilterHandlerAsync();
        Condition = new RgfFilter.Condition() { Conditions = FilterHandler.Conditions };
        IsFilterActive = Manager.IsFiltered || !FilterHandler.Conditions.Any();
        Open();
        args.Handled = true;
    }

    private void Open()
    {
        InvalidateGeneration();
        Description = null;
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
        _showComponent = true;
        StateHasChanged();
    }

    private Task OnDialogCloseAsync(IRgfEventArgs<RgfDialogEventArgs> args) => CloseDialogAsync();

    private async Task CloseDialogAsync()
    {
        _logger.LogDebug("CloseDialog | EntityName:{EntityName}", EntityParameters.EntityName);
        InvalidateGeneration();
        _showComponent = false;
        await FilterParameters.DialogParameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Destroy, this);
        StateHasChanged();
    }

    public Task RecreateAsync() => CloseDialogAsync().ContinueWith(_ => Task.Run(Open), TaskContinuationOptions.OnlyOnRanToCompletion);

    public virtual Task OnCancel(MouseEventArgs? args = null)
    {
        FilterHandler.ResetFilter();
        return CloseDialogAsync();
    }

    public virtual async Task OnOk(MouseEventArgs? args = null)
    {
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
                Description = null;
                FilterSettings = predefFilter;
                Condition = new RgfFilter.Condition();
                Task.Run(() =>
                {
                    Condition = new RgfFilter.Condition() { Conditions = FilterHandler.Conditions };
                    StateHasChanged();
                    _ = Manager.ToastManager.RaiseEventAsync(new RgfToastEventArgs(Manager.EntityDesc.MenuTitle, RgfToastEventArgs.ActionTemplate(_recroDict.GetRgfUiString("Load"), FilterSettings.SettingsName), RgfToastType.Success, delay: 2000), this);
                });
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
        if (IsSavingAsNewFilter)
        {
            FilterSettings = RgfFilterSettings.DeepCopy(FilterSettings);
            FilterSettings.FilterSettingsId = null;
            FilterSettings.IsReadonly = false;
            FilterSettings.RoleId = null;
        }
        var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, _recroDict.GetRgfUiString("SaveSettings"), FilterSettings.SettingsName);
        await Manager.ToastManager.RaiseEventAsync(toast, this);
        if (await FilterHandler.SaveFilterSettingsAsync(FilterSettings))
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
        InvalidateGeneration();
        EntityParameters?.UnsubscribeFromAll(this);
    }
}
