using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.blazor-ui.ai-chat
/// <summary>Displays and sends manual turns through a UI-independent conversation session.</summary>
public partial class RgfAiChatComponent : IDisposable
{
    /// <summary>Required conversation session; the component subscribes to state but does not own the transport.</summary>
    [Parameter, EditorRequired]
    public RgfAiConversationSession Session { get; set; } = null!;

    /// <summary>Optional callback for caught manual-send exceptions, including cancellation.</summary>
    [Parameter]
    public EventCallback<Exception> SendFailed { get; set; }

    /// <summary>Shows manual transport, workflow and cancellation alerts; defaults to false. Provider, supported Credit and invalid continuation warnings remain visible.</summary>
    [Parameter]
    public bool ShowSendErrors { get; set; }

    /// <summary>Uses theme-aware subtle user messages and transparent responses; defaults to false.</summary>
    [Parameter]
    public bool UseSubtleMessageStyle { get; set; } = false;

    /// <summary>Shows Cancel and cancels this component's manual turn on disposal; defaults to false and does not own external turns.</summary>
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
    private RgfAiResponse? _failedResponse;
    private RgfAiResponse? _choiceResponse;
    private RgfAiChoice? _selectedChoice;

    private RgfAiResponse? PendingResponse => Session.Conversation.IsPendingWorkflow
        ? Session.Conversation.Messages.LastOrDefault() as RgfAiResponse : null;
    private bool TextInputDisabled => Session.IsProcessing || PendingResponse?.ResponseMode == RgfAiResponseMode.ChoiceOnly;
    private bool HasChoices => PendingResponse is { Choices.Count: > 0, ResponseMode:
        RgfAiResponseMode.ChoiceOnly or RgfAiResponseMode.ChoiceOrText };

    private void SynchronizeChoices()
    {
        var response = PendingResponse;
        if (!ReferenceEquals(_choiceResponse, response))
        {
            _choiceResponse = response;
            _selectedChoice = null;
        }
    }

    private void SelectChoice(RgfAiResponse response, RgfAiChoice choice)
    {
        if (!Session.IsProcessing && HasChoices && ReferenceEquals(response, PendingResponse)
            && response.Choices!.Contains(choice)) _selectedChoice = choice;
    }

    private Task ConfirmChoiceAsync()
    {
        if (Session.IsProcessing || !HasChoices || _selectedChoice is null
            || !ReferenceEquals(_choiceResponse, PendingResponse)
            || !PendingResponse!.Choices!.Contains(_selectedChoice)) return Task.CompletedTask;
        return SendTurnAsync(_selectedChoice.Code, _selectedChoice.UserText);
    }

    private static readonly HashSet<string> CreditErrorCodes = new(StringComparer.Ordinal)
    {
        "AiCredit.UserDisabled",
        "AiCredit.InsufficientCredit",
        "AiCredit.BalanceExpired",
        "AiCredit.InvalidConfiguration",
        "AiCredit.UserNotFound",
        "AiCredit.InfrastructureFailure"
    };

    private void CancelSend() => _sendCancellation?.Cancel();

    protected override void OnParametersSet()
    {
        SynchronizeChoices();
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
        _selectedChoice = null;
        Prompt = string.Empty;
        ClearSendError();
    }

    private void ClearSendError()
    {
        _sendError = null;
        _errorKind = null;
        _failedResponse = null;
    }

    private void OnSessionStateChanged()
    {
        var notifyingSession = subscribedSession;
        var requestStarted = notifyingSession?.IsProcessing == true;
        if (!disposed)
        {
            _ = InvokeAsync(() =>
            {
                if (!disposed && ReferenceEquals(Session, notifyingSession))
                {
                    if (requestStarted) ClearSendError();
                    SynchronizeChoices();
                    StateHasChanged();
                }
            });
        }
    }

    private Task OnSendAsync()
    {
        if (TextInputDisabled || string.IsNullOrWhiteSpace(Prompt))
        {
            return Task.CompletedTask;
        }
        var message = Prompt.Trim();
        Prompt = string.Empty;
        return SendTurnAsync(message, message);
    }

    private async Task SendTurnAsync(string message, string presentationText)
    {
        ClearSendError();
        var sendingSession = Session;
        using var cancellation = new CancellationTokenSource();
        _sendCancellation = cancellation;
        try
        {
            var response = await sendingSession.SendWithPresentationAsync(message, presentationText, cancellationToken: cancellation.Token);
            if (disposed || !ReferenceEquals(Session, sendingSession)) return;
            if (!response.Success)
            {
                _failedResponse = response;
                var providerMissing = response.ErrorCode == RgfAiErrorCodes.AiProviderNotConfigured;
                var configurationFailure = response.ErrorCode is "AiCredit.InvalidConfiguration"
                    or RgfAiErrorCodes.AiProviderAuthenticationFailed;
                var creditRejected = response.ErrorCode != null && CreditErrorCodes.Contains(response.ErrorCode);
                var invalidContinuation = response.ErrorCode == "InvalidContinuationResponse";
                _errorKind = providerMissing || configurationFailure ? "configuration" : creditRejected ? "credit"
                    : invalidContinuation ? "continuation" : "workflow";
                _sendError = configurationFailure
                    ? "Recroby could not complete the request due to a configuration error. Please contact your system administrator."
                    : providerMissing
                    ? "No AI provider is configured. Configure an AI provider and model to use Recroby."
                    : (creditRejected || invalidContinuation) && !string.IsNullOrWhiteSpace(response.Message)
                    ? response.Message
                    : "The Recroby workflow could not complete the request.";
            }
        }
        catch (Exception exception)
        {
            if (!disposed && ReferenceEquals(Session, sendingSession))
            {
                _errorKind = exception is OperationCanceledException ? "cancelled" : "transport";
                _sendError = exception is OperationCanceledException ? "Request cancelled." : "The Recroby service could not be reached or returned an invalid response. Please try again.";
            }
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

    /// <summary>Detaches session notifications and cancels an owned manual turn only when AllowCancellation is enabled.</summary>
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
