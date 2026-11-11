using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.blazor-ui.ai-chat
/// <summary>Displays server-reported usage and the Credit snapshot for one response.</summary>
public partial class RgfAiUsageSummary : IAsyncDisposable
{
    [Inject]
    private IRecroSecService RecroSec { get; set; } = null!;

    [Inject]
    private IRecroDictService RecroDict { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        RecroSec.LanguageChangedEvent.Subscribe(OnLanguageChangedAsync);
    }

    private Task OnLanguageChangedAsync(DataEventArgs<string> _) => InvokeAsync(() =>
    {
        if (!_disposed) StateHasChanged();
    });

    [Inject] private IJSRuntime JS { get; set; } = null!;
    /// <summary>The original response; no values are inferred from model configuration or other turns.</summary>
    [Parameter, EditorRequired] public RgfAiResponse Response { get; set; } = null!;

    private readonly string _id = $"rgf-ai-usage-{Guid.NewGuid():N}";
    private ElementReference _element;
    private IJSObjectReference? _module;
    private bool _disposed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (Response.CreditUsage is null && Response.CreditState is null)
        {
            if (_module is not null) await _module.InvokeVoidAsync("dispose", _id);
            return;
        }
        var module = _module ?? await JS.InvokeAsync<IJSObjectReference>("import",
            $"{RgfClientConfiguration.AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Components/AI/RgfAiUsageSummary.razor.js");
        if (module is null) return;
        if (_disposed) { await module.DisposeAsync(); return; }
        _module = module;
        await module.InvokeVoidAsync("sync", _element);
    }

    /// <summary>Closes the panel and releases all browser listeners and observers.</summary>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        RecroSec.LanguageChangedEvent.Unsubscribe(OnLanguageChangedAsync);
        try
        {
            if (_module is not null)
            {
                // The DOM node may already be removed when Blazor runs asynchronous disposal.
                await _module.InvokeVoidAsync("dispose", _id);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
    }
    private string PanelId(string group) => $"{_id}-{group}";
    private string Credit(decimal? value) => value?.ToString("0.########", CultureInfo.CurrentCulture) ?? RecroDict.GetRgfUiString("Unknown");
    private string Count(long? value) => value?.ToString(CultureInfo.CurrentCulture) ?? RecroDict.GetRgfUiString("Unknown");
    private string Identity(string? value) => string.IsNullOrWhiteSpace(value) ? RecroDict.GetRgfUiString("Unknown") : value;
    private static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    private static string Duration(TimeSpan value) => value.TotalSeconds < 60
        ? $"{value.TotalSeconds.ToString("0.#", CultureInfo.CurrentCulture)} s"
        : $"{(long)value.TotalMinutes}m {value.Seconds}s";
    private string ModelLabel
    {
        get
        {
            var models = Response.CreditUsage!.Models.DistinctBy(m => (m.ProviderId, m.ModelId)).ToArray();
            return models.Length == 1 ? Identity(models[0].ModelId) : RecroDict.GetRgfRecrobyString("ModelCount", models.Length);
        }
    }
    private string StateLabel(RgfAiCreditState state) => state.Mode switch
    {
        AiCreditMode.Disabled => RecroDict.GetRgfUiString("Disabled"),
        AiCreditMode.Unlimited => RecroDict.GetRgfRecrobyString("CreditUnlimited"),
        AiCreditMode.Prepaid => Credit(state.Prepaid?.Balance),
        AiCreditMode.Periodic => RecroDict.GetRgfRecrobyString("CreditLimits"),
        _ => RecroDict.GetRgfUiString("Unknown")
    };
}
