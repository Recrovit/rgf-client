using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class TreeNodeComponent : IDisposable
{
    [Inject]
    private ILogger<TreeNodeComponent> _logger { get; set; } = default!;

    private RgfEntityParameters? EntityParameters => Node.EntityParameters;

    private IRgManager Manager => TreeComponent.Manager;

    public async override Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);

        Node.TooltipText = null;

        if (EntityParameters != null)
        {
            var eventArg = new RgfEventArgs<RgfTreeEventArgs>(this, new RgfTreeEventArgs(RgfTreeEventKind.NodeParametersSet, this, Node));
            await EntityParameters.TreeParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);

            if (Node.TooltipText != null)
            {
                return;
            }
        }

        if (Node.Property != null && Node.Property.ListType != PropertyListType.RecroGrid)
        {
            StringBuilder str = new();
            str.Append("<div class='rgf-tree-tooltip'>");
            foreach (var prop in Manager.EntityDesc.SortedVisibleColumns)
            {
                var data = Node.RowData.GetItemData(prop.Alias).StringValue;
                str.Append("<div class='row justify-content-start align-items-center'>")
                .AppendFormat("<div class='rgf-tooltip-title col-3 text-end text-truncate'>{0}</div>", prop.ColTitle)
                .Append("<div class='col-auto'>:</div>")
                .AppendFormat("<div class='rgf-tooltip-data col text-start text-truncate'>{0}</div>", data)
                .Append("</div>");
            }
            str.Append("</div>");
            Node.TooltipText = str.ToString();
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            EntityParameters?.EventDispatcher.Subscribe(RgfEntityEventKind.Initialized, OnEntityInitialized);
        }
    }

    private void OnEntityInitialized(IRgfEventArgs<RgfEntityEventArgs> args)
    {
        StateHasChanged();
    }

    private void OnEmbedded(MouseEventArgs arg)
    {
        if (EntityParameters != null)
        {
            EntityParameters.DisplayMode = EntityParameters.DisplayMode == RfgDisplayMode.Tree ? RfgDisplayMode.Grid : RfgDisplayMode.Tree;
        }
    }

    public void Dispose()
    {
        EntityParameters?.UnsubscribeFromAll(this);
    }
}