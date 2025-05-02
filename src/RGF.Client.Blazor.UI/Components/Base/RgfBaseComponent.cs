using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
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
    public string? RecroDictLabel { get; set; }

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
    public string? KeyboardEventTargetSelector { get; set; }

    [Parameter]
    public string[]? KeysToPrevent { get; set; }

    [Parameter]
    public Func<KeyboardEventArgs, Task>? OnKeyDown { get; set; }

    [Parameter]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = [];

    [Inject]
    protected IRecroDictService RecroDict { get; set; } = null!;

    [Inject]
    protected IJSRuntime _jsRuntime { get; set; } = null!;

    protected string _baseCssClass { get; set; } = string.Empty;

    private Dictionary<string, object> _attributes { get; } = [];

    protected IReadOnlyDictionary<string, object>? Attributes => _attributes.Count > 0 ? _attributes : null;

    protected ElementReference? _elementReference;
    private DotNetObjectReference<RgfBaseComponent>? _selfRef;

    private string? _currentKeyboardEventTargetSelector;
    private string[]? _currentKeysToPrevent;
    private Func<KeyboardEventArgs, Task>? _currentOnKeyDown;

    public static string GetNextId(string format = "rgf-id-{0}") => RgfComponentWrapper.GetNextId(format);

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        _attributes.Clear();

        AdditionalAttributes ??= [];
        if (AdditionalAttributes.Count > 0)
        {
            _attributes.AddRange(AdditionalAttributes);
        }

        var classAttr = (string.IsNullOrEmpty(CssClass) ? _baseCssClass : $"{_baseCssClass} {CssClass}")?.Trim() ?? string.Empty;
        if (AdditionalAttributes.TryGetValue("class", out var c) == true && c is string addClass)
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

        if (Width != null) { styleAttr += $"width:{Width};"; }
        if (MinWidth != null) { styleAttr += $"min-width:{MinWidth};"; }
        if (Height != null) { styleAttr += $"height:{Height};"; }

        if (Visibility != VisibilityState.Visible) { styleAttr += $"visibility:{Visibility.ToString().ToLower()};"; }

        if (AdditionalAttributes.TryGetValue("style", out var s) == true && s is string addStyle)
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

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (RecroDictLabel != null)
        {
            Label = await RecroDict.GetItemAsync(RecroDictLabel);
        }

        if (ShouldUpdateKeydownHandler)
        {
            await UnregisterKeydownHandler();
            if (OnKeyDown != null && string.IsNullOrEmpty(KeyboardEventTargetSelector) && Id == null)
            {
                _attributes["id"] = Id = GetNextId();
            }
        }
    }

    private bool ShouldUpdateKeydownHandler =>
        _currentOnKeyDown != OnKeyDown ||
        _currentKeyboardEventTargetSelector != KeyboardEventTargetSelector ||
        (_currentKeysToPrevent?.SequenceEqual(KeysToPrevent ?? Array.Empty<string>()) ?? KeysToPrevent != null);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (_elementReference != null && (!firstRender || TooltipOptions != null))
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Base.tooltip", _elementReference, TooltipOptions);
        }

        if (ShouldUpdateKeydownHandler)
        {
            if (OnKeyDown != null)
            {
                _selfRef ??= DotNetObjectReference.Create(this);
                var selector = KeyboardEventTargetSelector ?? $"#{Id}";
                await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Base.registerKeydown", _selfRef, selector, KeysToPrevent);
            }
            _currentOnKeyDown = OnKeyDown;
            _currentKeyboardEventTargetSelector = KeyboardEventTargetSelector;
            _currentKeysToPrevent = KeysToPrevent;
        }
    }

    protected string? GetAttribute(string key)
    {
        if (_attributes.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    private async Task UnregisterKeydownHandler()
    {
        if (_currentOnKeyDown != null)
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Base.unregisterKeydown", _currentKeyboardEventTargetSelector);
        }
    }

    [JSInvokable]
    public async Task OnKeyDownJsCallback(KeyboardEventArgs args)
    {
        if (OnKeyDown != null)
        {
            await OnKeyDown.Invoke(args);
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_elementReference != null && TooltipOptions != null)
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Base.tooltip", _elementReference);
        }
        if (_selfRef != null)
        {
            await UnregisterKeydownHandler();
            _selfRef.Dispose();
            _selfRef = null;
        }
    }
}