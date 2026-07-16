using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public static class RgfDashboardExtensions
{
    public static RgfDashboardDefinition CreateClone(this RgfDashboardDefinition source)
    {
        var clone = source.DeepCopy() ?? RgfDashboardDefinitionHelper.CreateLocalDashboard();
        clone.DashboardId = 0;
        clone.Name = null;
        clone.IsReadonly = false;
        clone.LayoutVersion = source.LayoutVersion;
        clone.Normalize();
        return clone;
    }

    public static void Normalize(this RgfDashboardDefinition? dashboard) => RgfDashboardDefinitionHelper.Normalize(dashboard);

    public static string SerializeSnapshot(this RgfDashboardDefinition? dashboard) => RgfDashboardDefinitionHelper.SerializeSnapshot(dashboard);

    public static bool TryValidateDefinition(this RgfDashboardDefinition? dashboard, out string? errorMessage)
    {
        var validationResult = RgfDashboardLayoutValidator.Validate(dashboard);
        errorMessage = validationResult.FirstErrorMessage;
        return validationResult.IsValid;
    }

    public static RgfDashboardDefinition? DeepCopy(this RgfDashboardDefinition? source)
        => source == null
            ? null
            : new()
            {
                DashboardId = source.DashboardId,
                Name = source.Name,
                Description = source.Description,
                RoleId = source.RoleId,
                LayoutVersion = source.LayoutVersion,
                Layout = source.Layout.DeepCopy() ?? new(),
                IsReadonly = source.IsReadonly
            };

    public static RgfDashboardLayout? DeepCopy(this RgfDashboardLayout? source)
        => source == null
            ? null
            : new()
            {
                Width = source.Width,
                Height = source.Height,
                RootPane = source.RootPane.DeepCopy() ?? new(),
                Items = source.Items?.Select(item => item.DeepCopy() ?? new()).ToList() ?? []
            };

    public static RgfDashboardItem? DeepCopy(this RgfDashboardItem? source)
        => source == null
            ? null
            : new()
            {
                DashboardItemId = source.DashboardItemId,
                Title = source.Title,
                ShowHeader = source.ShowHeader,
                ViewReference = source.ViewReference.DeepCopy() ?? new()
            };

    public static RgfDashboardViewReference? DeepCopy(this RgfDashboardViewReference? source)
        => source == null
            ? null
            : new()
            {
                EntityName = source.EntityName,
                ViewType = source.ViewType,
                SettingsId = source.SettingsId,
                SettingsName = source.SettingsName
            };

    public static RgfDashboardPane? DeepCopy(this RgfDashboardPane? source)
        => source == null
            ? null
            : new()
            {
                PaneId = source.PaneId,
                DashboardItemId = source.DashboardItemId,
                Split = source.Split.DeepCopy()
            };

    public static RgfDashboardSplit? DeepCopy(this RgfDashboardSplit? source)
        => source == null
            ? null
            : new()
            {
                Direction = source.Direction,
                Panes = source.Panes?.Select(splitPane => splitPane.DeepCopy() ?? new()).ToList() ?? []
            };

    public static RgfDashboardSplitPane? DeepCopy(this RgfDashboardSplitPane? source)
        => source == null
            ? null
            : new()
            {
                Size = source.Size,
                MinSize = source.MinSize,
                Pane = source.Pane.DeepCopy() ?? new()
            };
}
