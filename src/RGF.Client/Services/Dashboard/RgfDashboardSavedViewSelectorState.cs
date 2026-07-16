using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public sealed class RgfDashboardSavedViewSelectorState
{
    public IReadOnlyList<RgfDashboardEntityOption> EntityOptions { get; private set; } = [];

    public RgfDashboardEntitySettingsResult EntitySettings { get; private set; } = new();

    public IReadOnlyList<KeyValuePair<int?, string>> SavedViews { get; private set; } = [];

    public string? SelectedEntityName { get; private set; }

    public RgfDashboardViewType SelectedViewType { get; private set; } = RgfDashboardViewType.Grid;

    public int? SelectedSettingsId { get; private set; }

    public string? SelectedSettingsName { get; private set; }

    public int? MissingSettingsId { get; private set; }

    public string? MissingSettingsName { get; private set; }

    public bool IsEntityOptionsLoading { get; private set; }

    public bool IsSettingsLoading { get; private set; }

    public string? EntityOptionsError { get; private set; }

    public string? SettingsError { get; private set; }

    public bool HasMissingSelectedView => MissingSettingsId > 0;

    public bool CanSubmitSelection
        => !string.IsNullOrWhiteSpace(SelectedEntityName)
            && SelectedSettingsId > 0
            && !HasMissingSelectedView;

    public IReadOnlyList<KeyValuePair<RgfDashboardViewType, string>> AllowedViewTypes
    {
        get
        {
            List<KeyValuePair<RgfDashboardViewType, string>> viewTypes = [];

            if (EntitySettings.SavedViews.Any(e => e.Type == RgfDashboardSavedViewType.Grid))
            {
                viewTypes.Add(new(RgfDashboardViewType.Grid, "Grid"));
                viewTypes.Add(new(RgfDashboardViewType.Tree, "Tree"));
            }

            if (EntitySettings.SavedViews.Any(e => e.Type == RgfDashboardSavedViewType.Chart))
            {
                viewTypes.Add(new(RgfDashboardViewType.Chart, "Chart"));
                viewTypes.Add(new(RgfDashboardViewType.ChartData, "Chart data"));
            }

            return viewTypes;
        }
    }

    public async Task LoadEntityOptionsAsync(
        IReadOnlyList<RgfDashboardEntityOption> preferredEntityOptions,
        IRgfApiService apiService,
        ILogger logger)
    {
        EntityOptionsError = null;
        IsEntityOptionsLoading = true;

        try
        {
            if (preferredEntityOptions.Count > 0)
            {
                EntityOptions = preferredEntityOptions;
                return;
            }

            var response = await apiService.GetDashboardEntityOptionsAsync();
            if (response.Success)
            {
                EntityOptions = response.Result.Result;
            }
            else
            {
                EntityOptions = [];
                EntityOptionsError = response.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load dashboard entity options.");
            EntityOptions = [];
            EntityOptionsError = ex.Message;
        }
        finally
        {
            IsEntityOptionsLoading = false;
        }
    }

    public async Task EnsureDefaultEntitySelectionAsync(IRgfApiService apiService, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(SelectedEntityName) || EntityOptions.Count == 0)
        {
            return;
        }

        await SelectEntityAsync(EntityOptions[0].EntityName, apiService, logger);
    }

    public async Task SelectEntityAsync(string? entityName, IRgfApiService apiService, ILogger logger)
    {
        SelectedEntityName = NormalizeEntityName(entityName);
        ClearSavedViewSelection();
        await LoadEntitySettingsAsync(apiService, logger);
    }

    public void SelectViewType(RgfDashboardViewType viewType)
    {
        SelectedViewType = viewType;
        EnsureSelectedViewType();
        RebuildSavedViews(preserveSelection: true);
    }

    public void SelectSettings(int? settingsId)
    {
        MissingSettingsId = null;
        MissingSettingsName = null;
        SelectedSettingsId = settingsId;
        SelectedSettingsName = SavedViews.FirstOrDefault(e => e.Key == settingsId).Value;
    }

    public async Task InitializeFromViewReferenceAsync(
        string? entityName,
        RgfDashboardViewType viewType,
        int? settingsId,
        string? settingsName,
        IRgfApiService apiService,
        ILogger logger)
    {
        SelectedViewType = viewType;
        SelectedEntityName = NormalizeEntityName(entityName);
        ClearSavedViewSelection();

        await LoadEntitySettingsAsync(apiService, logger);

        if (string.IsNullOrWhiteSpace(SelectedEntityName))
        {
            return;
        }

        ApplyPreferredSavedViewSelection(settingsId, settingsName, markMissingSelection: true);
    }

    public void ResetSelection(bool clearEntity)
    {
        if (clearEntity)
        {
            SelectedEntityName = null;
            EntitySettings = new();
            SavedViews = [];
        }

        ClearSavedViewSelection();
        SettingsError = null;
    }

    private async Task LoadEntitySettingsAsync(IRgfApiService apiService, ILogger logger)
    {
        EntitySettings = new();
        SavedViews = [];
        SettingsError = null;

        if (string.IsNullOrWhiteSpace(SelectedEntityName))
        {
            return;
        }

        var entityOption = EntityOptions.FirstOrDefault(e => string.Equals(e.EntityName, SelectedEntityName, StringComparison.OrdinalIgnoreCase));
        if (entityOption == null || entityOption.EntityId <= 0)
        {
            return;
        }

        IsSettingsLoading = true;
        try
        {
            var response = await apiService.GetDashboardEntitySettingsAsync(entityOption.EntityId);
            if (response.Success)
            {
                EntitySettings = response.Result.Result ?? new();
                EnsureSelectedViewType();
                RebuildSavedViews(preserveSelection: false);
            }
            else
            {
                EntitySettings = new();
                SavedViews = [];
                SettingsError = response.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load dashboard saved views for {EntityName}.", SelectedEntityName);
            EntitySettings = new();
            SavedViews = [];
            SettingsError = ex.Message;
        }
        finally
        {
            IsSettingsLoading = false;
        }
    }

    private void EnsureSelectedViewType()
    {
        var allowedViewTypes = AllowedViewTypes;
        if (allowedViewTypes.Count == 0)
        {
            SelectedViewType = RgfDashboardViewType.Grid;
            return;
        }

        if (!allowedViewTypes.Any(e => e.Key == SelectedViewType))
        {
            SelectedViewType = allowedViewTypes[0].Key;
        }
    }

    private void RebuildSavedViews(bool preserveSelection)
    {
        var selectedSettingsId = preserveSelection ? SelectedSettingsId : null;
        var selectedSettingsName = preserveSelection ? SelectedSettingsName : null;

        SavedViews = EntitySettings.SavedViews
            .Where(e => e.Type == GetSavedViewType(SelectedViewType))
            .Where(e => e.SettingsId > 0)
            .Select(e => new KeyValuePair<int?, string>(e.SettingsId, e.SettingsName))
            .ToList();

        ApplyPreferredSavedViewSelection(selectedSettingsId, selectedSettingsName, markMissingSelection: false);
    }

    private void ApplyPreferredSavedViewSelection(int? settingsId, string? settingsName, bool markMissingSelection)
    {
        SelectedSettingsId = null;
        SelectedSettingsName = null;
        MissingSettingsId = null;
        MissingSettingsName = null;

        if (settingsId is not > 0)
        {
            return;
        }

        var matchingSavedView = SavedViews.FirstOrDefault(e => e.Key == settingsId);
        if (matchingSavedView.Key > 0)
        {
            SelectedSettingsId = matchingSavedView.Key;
            SelectedSettingsName = matchingSavedView.Value;
            return;
        }

        if (markMissingSelection)
        {
            MissingSettingsId = settingsId;
            MissingSettingsName = string.IsNullOrWhiteSpace(settingsName) ? null : settingsName.Trim();
        }
    }

    private void ClearSavedViewSelection()
    {
        SelectedSettingsId = null;
        SelectedSettingsName = null;
        MissingSettingsId = null;
        MissingSettingsName = null;
    }

    private static string? NormalizeEntityName(string? entityName)
        => string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim();

    private static RgfDashboardSavedViewType GetSavedViewType(RgfDashboardViewType viewType)
        => viewType is RgfDashboardViewType.Chart or RgfDashboardViewType.ChartData
            ? RgfDashboardSavedViewType.Chart
            : RgfDashboardSavedViewType.Grid;
}
