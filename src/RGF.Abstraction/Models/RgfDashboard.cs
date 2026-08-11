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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ShowHeader { get; set; } = true;

    public RgfDashboardViewReference ViewReference { get; set; } = new();
}

public class RgfDashboardViewReference
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public RgfDashboardViewType ViewType { get; set; } = RgfDashboardViewType.Grid;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExportKey { get; set; }
}

public class RgfDashboardEntityOption
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EntityId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}

public class RgfDashboardSavedViewOption
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExportKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; } = string.Empty;

    public RgfDashboardSavedViewType Type { get; set; }
}

public class RgfDashboardEntitySettingsResult
{
    public IReadOnlyList<RgfDashboardSavedViewOption> SavedViews { get; set; } = [];
}

public class RgfDashboardPane
{
    [JsonIgnore]
    public string PaneId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

public class RgfDashboardExport
{
    public RgfDashboardDefinition Dashboard { get; set; } = new();

    public List<RgfChartSettings> ChartSettings { get; set; } = [];

    public List<RgfGridSettings> GridSettings { get; set; } = [];
}
