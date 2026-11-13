using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using static Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI.RgfRecrobyFilterActionHandler;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration

internal sealed class RgfRecrobyConversationTransport(
    Func<IRgfApiService> resolveApi,
    IRecroDictService recroDict,
    Func<RgfRecrobyGridContext?> getActiveGrid,
    Func<Action, Task> dispatch,
    Func<Func<Task>, Task> dispatchAsync,
    Func<bool> isAbandoned,
    RgfRecrobyFilterActionHandler filterActionHandler,
    Action<Exception, string> logWarning) : IRgfAiTransport
{
    private (string? ConversationId, string? Token, RequestContext Context)? _pending;

    public async Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfAbandoned();
        RequestContext current = default;
        RequestContext original = default;
        RgfAiResponse? rejection = null;
        await dispatch(() =>
        {
            var gridContext = getActiveGrid();
            var manager = gridContext?.Manager;
            var managerVersion = gridContext?.ManagerVersion ?? default;
            var session = manager?.SessionParams;
            var managerIds = (session?.SessionId, session?.GridId);
            var entityId = manager?.EntityDesc.EntityId ?? 0;
            request.GridRequest = gridContext?.CreateGridRequest();

            var target = new GridTarget(gridContext, request.GridRequest?.SessionId, request.GridRequest?.GridId, entityId);
            var continuation = _pending is { } pending && pending.ConversationId == request.ConversationId &&
                pending.Token == request.ConversationToken ? _pending : null;
            if (continuation is { } previous && previous.Context.Target != target)
            {
                rejection = new RgfAiResponse
                {
                    ErrorCode = "PendingGridChanged",
                    Message = previous.Context.Target.Context is null
                        ? recroDict.GetRgfRecrobyString("RecrobyPendingGridAdded")
                        : recroDict.GetRgfRecrobyString("RecrobyPendingGridChanged")
                };
                return;
            }

            current = new(target, manager, managerVersion, managerIds);
            original = continuation?.Context ?? CaptureSnapshots(current, request);
        });

        ThrowIfAbandoned();
        if (rejection is not null)
        {
            return rejection;
        }

        var response = await new RgfAiApiTransport(resolveApi()).SendAsync(request, cancellationToken);
        if (isAbandoned())
        {
            throw new OperationCanceledException();
        }
        if (response is { Success: true, FilterAction: { } action })
        {
            // The conversation keeps response objects as history. Consume the action in this turn.
            response.FilterAction = null;
            var result = ApplyResult.Uncertain;
            var dispatchFailed = false;
            try
            {
                await dispatchAsync(() => filterActionHandler.ApplyAsync(action, current, original,
                    isAbandoned, () => Volatile.Read(ref dispatchFailed), cancellationToken, outcome => result = outcome));
            }
            catch (Exception error)
            {
                Volatile.Write(ref dispatchFailed, true);
                if (error is not OperationCanceledException || (!cancellationToken.IsCancellationRequested && !isAbandoned()))
                {
                    logWarning(error, "The Recroby Grid filter dispatcher failed.");
                }
            }
            if (result != ApplyResult.Applied)
            {
                response.Success = false;
                response.CanContinueAfterFailure = response.WorkflowStatus == "Completed" &&
                    !string.IsNullOrWhiteSpace(response.WorkflowRunId) &&
                    !string.IsNullOrWhiteSpace(response.ConversationId) &&
                    !string.IsNullOrWhiteSpace(response.ConversationToken) &&
                    (request.ConversationId is null || request.ConversationId == response.ConversationId);
                response.ErrorCode = result == ApplyResult.Uncertain
                    ? "RecrobyFilterApplicationUncertain" : "RecrobyFilterNotApplied";
                response.Message = recroDict.GetRgfRecrobyString(result == ApplyResult.Uncertain
                    ? "FilterApplicationUncertain" : "FilterLoadFailed");
            }
        }
        if (isAbandoned())
        {
            throw new OperationCanceledException();
        }
        if (response.Success || response.CanContinueAfterFailure)
        {
            _pending = response.WorkflowStatus == "WaitingForInput"
                ? (response.ConversationId, response.ConversationToken, original)
                : null;
        }
        return response;

        void ThrowIfAbandoned()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isAbandoned())
            {
                throw new OperationCanceledException();
            }
        }
    }
}
