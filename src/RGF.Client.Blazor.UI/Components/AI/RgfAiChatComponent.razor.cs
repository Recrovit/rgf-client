using Microsoft.AspNetCore.Components;
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

    private string InputId { get; } = RgfBaseComponent.GetNextId();
    private string Prompt { get; set; } = string.Empty;
    private RgfAiConversationSession? subscribedSession;
    private volatile bool disposed;

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
        try
        {
            await Session.SendAsync(message);
        }
        catch (Exception exception)
        {
            if (SendFailed.HasDelegate)
            {
                await SendFailed.InvokeAsync(exception);
            }
        }
    }

    public void Dispose()
    {
        disposed = true;
        if (subscribedSession != null)
        {
            subscribedSession.StateChanged -= OnSessionStateChanged;
            subscribedSession = null;
        }
    }
}
