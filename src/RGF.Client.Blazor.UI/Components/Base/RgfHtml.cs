using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

public class RgfHtml : RgfBaseComponent
{
    [Parameter, EditorRequired]
    public string TagName { get; set; } = default!;

    [Parameter]
    public object? RenderKey { get; set; }

    [Parameter]
    public RenderFragment ChildContent { get; set; } = null!;

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> OnDblClick { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> OnContextMenu { get; set; }

    protected RenderFragment? Content { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        base.BuildRenderTree(builder);

        if (string.IsNullOrEmpty(TagName))
        {
            return;
        }

        int sequence = 0;
        builder.OpenElement(sequence++, TagName);
        if (RenderKey != null)
        {
            builder.SetKey(RenderKey);
        }

        var attributes = Attributes;
        if (attributes != null && attributes.Count > 0)
        {
            foreach (var attribute in attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }
        }

        if (OnClick.HasDelegate)
        {
            builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnClick));
        }

        if (OnDblClick.HasDelegate)
        {
            builder.AddAttribute(sequence++, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnDblClick));
        }

        if (OnContextMenu.HasDelegate)
        {
            builder.AddAttribute(sequence++, "oncontextmenu", EventCallback.Factory.Create<MouseEventArgs>(this, OnContextMenu));
        }

        builder.AddElementReferenceCapture(sequence++, capturedRef => _elementReference = capturedRef);

        if (ChildContent != null)
        {
            builder.AddContent(sequence++, ChildContent);
        }

        builder.CloseElement();
    }
}