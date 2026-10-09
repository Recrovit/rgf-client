using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.blazor-ui.ai-chat
/// <summary>Displays and sends manual turns through a UI-independent conversation session.</summary>
public partial class RgfAiChatComponent : IDisposable
{
    [Parameter, EditorRequired]
    public RgfAiConversationSession Session { get; set; } = null!;

    [Parameter]
    public EventCallback<Exception> SendFailed { get; set; }

    [Parameter]
    public bool ShowSendErrors { get; set; }

    [Parameter] public bool AllowCancellation { get; set; }

    /// <summary>Optional content below the input, beside the buttons, wrapping onto a new line when needed.</summary>
    [Parameter] public RenderFragment? InputFooter { get; set; }

    private string InputId { get; } = RgfBaseComponent.GetNextId();
    private string Prompt { get; set; } = string.Empty;
    private RgfAiConversationSession? subscribedSession;
    private volatile bool disposed;
    private CancellationTokenSource? _sendCancellation;
    private string? _sendError;
    private string? _errorKind;

    private void CancelSend() => _sendCancellation?.Cancel();

    protected override void OnParametersSet()
    {
        if (disposed || ReferenceEquals(Session, subscribedSession))
        {
            return;
        }
        if (subscribedSession != null)
        {
            subscribedSession.StateChanged -= OnSessionStateChanged;
        }
        subscribedSession = Session;
        subscribedSession.StateChanged += OnSessionStateChanged;
    }

    private void OnSessionStateChanged()
    {
        if (!disposed)
        {
            _ = InvokeAsync(() =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
        }
    }

    private async Task OnSendAsync()
    {
        if (Session.IsProcessing || string.IsNullOrWhiteSpace(Prompt))
        {
            return;
        }
        var message = Prompt.Trim();
        Prompt = string.Empty;
        _sendError = null;
        using var cancellation = new CancellationTokenSource();
        _sendCancellation = cancellation;
        try
        {
            var response = await Session.SendAsync(message, cancellationToken: cancellation.Token);
            if (!response.Success)
            {
                var providerMissing = response.ErrorCode == RgfAiErrorCodes.AiProviderNotConfigured;
                _errorKind = providerMissing ? "configuration" : "workflow";
                _sendError = providerMissing
                    ? "No AI provider is configured. Configure an AI provider and model to use Recroby."
                    : "The Recroby workflow could not complete the request.";
            }
        }
        catch (Exception exception)
        {
            _errorKind = exception is OperationCanceledException ? "cancelled" : "transport";
            _sendError = exception is OperationCanceledException ? "Request cancelled." : "The Recroby service could not be reached or returned an invalid response. Please try again.";
            if (SendFailed.HasDelegate)
            {
                await SendFailed.InvokeAsync(exception);
            }
        }
        finally
        {
            if (ReferenceEquals(_sendCancellation, cancellation)) _sendCancellation = null;
        }
    }

    public void Dispose()
    {
        disposed = true;
        if (AllowCancellation) _sendCancellation?.Cancel();
        if (subscribedSession != null)
        {
            subscribedSession.StateChanged -= OnSessionStateChanged;
            subscribedSession = null;
        }
    }
}
