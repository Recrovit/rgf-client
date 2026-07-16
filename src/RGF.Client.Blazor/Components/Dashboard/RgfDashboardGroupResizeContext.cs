using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components.Dashboard;

public sealed class RgfDashboardGroupResizeContext
{
    private readonly RgfDashboardGroupResizeComponent _owner;

    internal RgfDashboardGroupResizeContext(RgfDashboardGroupResizeComponent owner)
    {
        _owner = owner;
    }

    public IReadOnlyList<RgfDashboardSplitPane> RenderablePanes => _owner.RenderablePanesSnapshot;

    public IReadOnlyList<decimal> WorkingSizes => _owner.WorkingSizesSnapshot;

    public bool HasRenderablePanes => _owner.HasRenderablePanesSnapshot;

    public bool IsVertical => _owner.IsVerticalSnapshot;

    public bool IsDragging => _owner.IsDraggingSnapshot;

    public string GetSegmentStyle(RgfDashboardSplitPane splitPane, int index)
        => _owner.GetSegmentStyle(splitPane, index);

    public EventCallback<PointerEventArgs> GetHandlePointerDownCallback(int handleIndex)
        => EventCallback.Factory.Create<PointerEventArgs>(_owner, args => _owner.OnHandlePointerDownAsync(handleIndex, args));

    public EventCallback<PointerEventArgs> OverlayPointerMoveCallback
        => EventCallback.Factory.Create<PointerEventArgs>(_owner, _owner.OnOverlayPointerMoveAsync);

    public EventCallback<PointerEventArgs> OverlayPointerUpCallback
        => EventCallback.Factory.Create<PointerEventArgs>(_owner, _owner.OnOverlayPointerUpAsync);
}
