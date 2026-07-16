using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Services.Dashboard;

namespace Recrovit.RecroGridFramework.Client.Tests.Services.Dashboard;

public sealed class RgfDashboardLayoutValidatorTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_DesignerMode_RejectsInvalidDashboardItemIds(int dashboardItemId)
    {
        var dashboard = CreateDashboardWithItem(dashboardItemId, "Orders");

        var result = RgfDashboardLayoutValidator.Validate(dashboard, RgfDashboardValidationMode.Designer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "item-id-invalid" && issue.DashboardItemId == dashboardItemId);
    }

    [Fact]
    public void Validate_DesignerMode_RejectsMissingEntityReference()
    {
        var dashboard = CreateDashboardWithItem(1, string.Empty);

        var result = RgfDashboardLayoutValidator.Validate(dashboard, RgfDashboardValidationMode.Designer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "item-entity-missing" && issue.DashboardItemId == 1);
    }

    [Fact]
    public void Validate_DesignerMode_RejectsDuplicateDashboardItemIds()
    {
        var dashboard = new RgfDashboardDefinition
        {
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    DashboardItemId = 1
                }
            ,
                Items =
                [
                    new()
                    {
                        DashboardItemId = 1,
                        ViewReference = CreateViewReference("Orders")
                    },
                    new()
                    {
                        DashboardItemId = 1,
                        ViewReference = CreateViewReference("Invoices")
                    }
                ]
            }
        };

        var result = RgfDashboardLayoutValidator.Validate(dashboard, RgfDashboardValidationMode.Designer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "item-id-duplicate" && issue.DashboardItemId == 1);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "item-duplicate");
    }

    [Fact]
    public void Validate_DesignerMode_RejectsDashboardItemsReferencedByMultiplePanes()
    {
        var dashboard = new RgfDashboardDefinition
        {
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    Split = new()
                    {
                        Direction = RgfDashboardSplitDirection.Columns,
                        Panes =
                        [
                            new()
                            {
                                Pane = new()
                                {
                                    PaneId = "left",
                                    DashboardItemId = 1
                                }
                            },
                            new()
                            {
                                Pane = new()
                                {
                                    PaneId = "right",
                                    DashboardItemId = 1
                                }
                            }
                        ]
                    }
                },
                Items =
                [
                    new()
                    {
                        DashboardItemId = 1,
                        ViewReference = CreateViewReference("Orders")
                    }
                ]
            }
        };

        var result = RgfDashboardLayoutValidator.Validate(dashboard, RgfDashboardValidationMode.Designer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "item-duplicate" && issue.DashboardItemId == 1);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "item-id-duplicate");
    }

    private static RgfDashboardDefinition CreateDashboardWithItem(int dashboardItemId, string entityName)
        => new()
        {
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    DashboardItemId = dashboardItemId
                },
                Items =
                [
                    new()
                    {
                        DashboardItemId = dashboardItemId,
                        ViewReference = CreateViewReference(entityName)
                    }
                ]
            }
        };

    private static RgfDashboardViewReference CreateViewReference(string entityName)
        => new()
        {
            EntityName = entityName,
            ViewType = RgfDashboardViewType.Grid
        };
}
