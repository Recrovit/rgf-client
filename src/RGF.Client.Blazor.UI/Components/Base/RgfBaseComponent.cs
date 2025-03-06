using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

public class RgfBaseComponent : ComponentBase, IAsyncDisposable
{
    public enum VisibilityState
    {
        Visible,
        Hidden,
        Collapse,
        Initial,
        Inherit
    }

    [Parameter]
    public string? CssClass { get; set; }

    [Parameter]
    public string? Style { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? LabelCssClass { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? Tooltip { get; set; }

    [Parameter]
    public RgfTooltipOptions? TooltipOptions { get; set; }

    [Parameter]
    public string? DisplayName { get; set; }

    [Parameter]
    public bool Readonly { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public VisibilityState Visibility { get; set; } = VisibilityState.Visible;

    [Parameter]
    public string? Width { get; set; }

    [Parameter]
    public string? MinWidth { get; set; }

    [Parameter]
    public string? Height { get; set; }

    [Parameter]
    public string? Id { get; set; }

    [Parameter]
    public string? Role { get; set; }

    [Parameter]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    [Inject]
    protected IJSRuntime _jsRuntime { get; set; } = null!;

    protected string _baseCssClass { get; set; } = string.Empty;

    protected Dictionary<string, object>? _attributes { get; set; }

    protected ElementReference? _elementReference;

    public static string GetNextId(string format = "rgf-id-{0}") => RgfComponentWrapper.GetNextId(format);

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        _attributes = new Dictionary<string, object>();
        if (AdditionalAttributes.Any() == true)
        {
            _attributes.AddRange(AdditionalAttributes);
        }

        var classAttr = (string.IsNullOrEmpty(CssClass) ? _baseCssClass : $"{_baseCssClass} {CssClass}")?.Trim() ?? string.Empty;
        if (AdditionalAttributes?.TryGetValue("class", out var c) == true && c is string addClass)
        {
            classAttr = classAttr.EnsureContains(addClass, ' ');
        }
        if (!string.IsNullOrWhiteSpace(classAttr))
        {
            _attributes["class"] = classAttr;
        }

        if (Id != null) { _attributes["id"] = Id; }
        if (!string.IsNullOrEmpty(Title)) { _attributes["title"] = Title; }
        if (!string.IsNullOrEmpty(Role)) { _attributes["role"] = Role; }
        if (Readonly) { _attributes["readonly"] = ""; }
        if (Disabled) { _attributes["disabled"] = ""; }

        string styleAttr = Style?.Trim() ?? string.Empty;
        if (styleAttr.Length > 0 && !styleAttr.EndsWith(';'))
        {
            styleAttr += ";";
        }
        if (Width != null)
        {
            styleAttr += $"width:{Width};";
        }
        if (MinWidth != null)
        {
            styleAttr += $"min-width:{MinWidth};";
        }
        if (Height != null)
        {
            styleAttr += $"height:{Height};";
        }
        if (Visibility != VisibilityState.Visible)
        {
            styleAttr += $"visibility:{Visibility.ToString().ToLower()};";
        }
        if (AdditionalAttributes?.TryGetValue("style", out var s) == true && s is string addStyle)
        {
            if (!addStyle.EndsWith(';'))
            {
                addStyle += ";";
            }
            styleAttr += addStyle;
        }
        if (!string.IsNullOrWhiteSpace(styleAttr))
        {
            _attributes["style"] = styleAttr;
        }

        if (Tooltip != null)
        {
            if (TooltipOptions == null)
            {
                TooltipOptions = new RgfTooltipOptions(Tooltip);
            }
            else
            {
                TooltipOptions.Title = Tooltip;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (_elementReference != null && (!firstRender || TooltipOptions != null))
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Base.tooltip", _elementReference, TooltipOptions);
        }
    }

    protected string? GetAttribute(string key)
    {
        if (_attributes == null)
        {
            return null;
        }

        if (_attributes.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_elementReference != null && TooltipOptions != null)
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Base.tooltip", _elementReference, TooltipOptions);
        }
    }
}