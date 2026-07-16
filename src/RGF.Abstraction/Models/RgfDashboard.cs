#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public enum RgfDashboardViewType
{
    None = 0,
    Grid = 1,
    Tree = 2,
    Chart = 3,
    ChartData = 4
}

public enum RgfDashboardSavedViewType
{
    Grid = 1,
    Chart = 2
}

public enum RgfDashboardSplitDirection
{
    Columns = 1,
    Rows = 2
}

public class RgfDashboardDefinition
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DashboardId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoleId { get; set; }

    public int LayoutVersion { get; set; } = 1;

    public RgfDashboardLayout Layout { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsReadonly { get; set; }
}

public class RgfDashboardLayout
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Width { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Height { get; set; }

    public RgfDashboardPane RootPane { get; set; } = new();

    public List<RgfDashboardItem> Items { get; set; } = [];
}

public class RgfDashboardItem
{
    public int DashboardItemId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    public bool ShowHeader { get; set; } = true;

    public RgfDashboardViewReference ViewReference { get; set; } = new();
}

public class RgfDashboardViewReference
{
    public string EntityName { get; set; } = string.Empty;

    public RgfDashboardViewType ViewType { get; set; } = RgfDashboardViewType.Grid;

    public int? SettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SettingsName { get; set; }
}

public class RgfDashboardEntityOption
{
    public int EntityId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}

public class RgfDashboardSavedViewOption
{
    public int SettingsId { get; set; }

    public string SettingsName { get; set; } = string.Empty;

    public RgfDashboardSavedViewType Type { get; set; }
}

public class RgfDashboardEntitySettingsResult
{
    public IReadOnlyList<RgfDashboardSavedViewOption> SavedViews { get; set; } = [];
}

public class RgfDashboardPane
{
    public string PaneId { get; set; } = Guid.NewGuid().ToString("N");

    public int? DashboardItemId { get; set; }

    public RgfDashboardSplit? Split { get; set; }
}

public class RgfDashboardSplit
{
    public RgfDashboardSplitDirection Direction { get; set; }

    public List<RgfDashboardSplitPane> Panes { get; set; } = [];
}

public class RgfDashboardSplitPane
{
    public decimal Size { get; set; } = 1;

    public decimal? MinSize { get; set; }

    public RgfDashboardPane Pane { get; set; } = new();
}

public class RgfDashboardListItem
{
    public int DashboardId { get; set; }

    public string Name { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoleId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsReadonly { get; set; }
}

public class RgfDashboardCatalogResult
{
    public IReadOnlyList<RgfDashboardListItem> Dashboards { get; set; } = [];

    public RgfDashboardDefinition? SelectedDashboard { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsPublicDashboardSettingAllowed { get; set; }
}
