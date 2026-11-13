using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration

internal sealed class RgfRecrobyFilterActionHandler(
    IServiceProvider services,
    IRecroDictService recroDict,
    Func<RgfRecrobyGridContext, bool> isGridRegistered,
    Func<IRgfNotificationManager, RgfToastEventArgs, Task> raiseToastAsync,
    Action<Exception, string> logWarning,
    RgfRecrobyGridRefresher gridRefresher)
{
    internal readonly record struct GridTarget(RgfRecrobyGridContext? Context, string? SessionId, string? GridId, int EntityId);

    internal readonly record struct RequestContext(GridTarget Target, IRgManager? Manager,
        (long Assignment, long Initialization) ManagerVersion, (string? SessionId, string? GridId) ManagerIds,
        string? CreateSnapshot = null, string? ModifySnapshot = null);

    internal static RequestContext CaptureSnapshots(RequestContext context, RgfAiRequest request)
    {
        var applied = context.Manager?.ListHandler.GetGridSettings().Conditions;
        var appliedJson = applied is { Length: > 0 } ? CaptureFilter(applied) : null;
        return context with
        {
            CreateSnapshot = context.Manager is not null ? CaptureCreateFilter(applied) : null,
            ModifySnapshot = appliedJson == CaptureFilter(request.GridRequest?.ListParam?.UserFilter) ? appliedJson : null
        };
    }

    private static string CaptureFilter(RgfFilter.Condition[]? conditions) => JsonSerializer.Serialize(conditions);

    // The wire format omits IsQuickFilter, so capture its position in the condition tree separately.
    private static string CaptureCreateFilter(RgfFilter.Condition[]? conditions) =>
        JsonSerializer.Serialize(new { Conditions = conditions ?? [], QuickFlags = QuickFlags(conditions ?? []) });

    private static IEnumerable<bool> QuickFlags(IEnumerable<RgfFilter.Condition> conditions)
    {
        foreach (var condition in conditions)
        {
            yield return condition.IsQuickFilter;
            foreach (var flag in QuickFlags(condition.Conditions ?? []))
            {
                yield return flag;
            }
        }
    }

    internal async Task ApplyAsync(RgfAiFilterAction action, RequestContext current, RequestContext original,
        Action throwIfAbandoned, Func<bool> isAbandoned, CancellationToken cancellationToken)
    {
        var manager = current.Manager;
        var target = current.Target;
        var gridContext = target.Context;
        throwIfAbandoned();
        IRgListHandler? list = null;
        IRgFilterHandler? appliedHandler = null;
        var stale = false;
        try
        {
            if (CanApply())
            {
                var handler = await manager!.GetFilterHandlerAsync();
                throwIfAbandoned();
                if (CanApply() && handler is not null &&
                    handler.InitFilter(JsonSerializer.Serialize(action.Conditions)))
                {
                    // Keep editor changes separate from the applied conditions until the user accepts them.
                    var applied = RgfGridSettings.DeepCopy(new RgfGridSettings { Conditions = handler.StoreFilter() })!;
                    list = manager.ListHandler;
                    list.ApplyFilterState(applied.Conditions ?? [], list.SQLTimeout);
                    handler.SetNewAiFilterSettings(action.Remark);
                    appliedHandler = handler;
                }
            }
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested && !isAbandoned())
        {
            list = null;
            logWarning(error, "The Recroby filter could not be loaded.");
        }

        if (list is null)
        {
            throwIfAbandoned();
            await ReportFilterLoadFailureAsync(manager, stale);
            return;
        }

        try
        {
            await gridContext!.NotifyAiFilterAppliedAsync(manager!, appliedHandler!);
        }
        catch (Exception error)
        {
            logWarning(error, "The Recroby filter applied notification failed.");
        }
        _ = gridRefresher.RefreshAsync(list, invalidateAggregates: false);

        bool CanApply()
        {
            var matches = IsTargetCurrent() && HasOriginalFilter();
            stale = !matches && action.Operation == RgfAiFilterOperation.ReplaceIfUnchanged;
            return matches && (action.Operation switch
            {
                RgfAiFilterOperation.Replace => action.Conditions is { Length: > 0 },
                RgfAiFilterOperation.ReplaceIfUnchanged => action.Conditions is not null,
                _ => false
            });
        }

        bool IsTargetCurrent() =>
            manager is not null && gridContext is { IsClosed: false } && isGridRegistered(gridContext) &&
            ReferenceEquals(gridContext.Manager, manager) && gridContext.ManagerVersion == current.ManagerVersion &&
            manager.ListHandler is { Initialized: true } &&
            !string.IsNullOrEmpty(action.SessionId) && !string.IsNullOrEmpty(action.GridId) && action.EntityId > 0 &&
            action.SessionId == target.SessionId && action.GridId == target.GridId && action.EntityId == target.EntityId &&
            action.SessionId == current.ManagerIds.SessionId && action.GridId == current.ManagerIds.GridId &&
            action.SessionId == manager.SessionParams.SessionId && action.GridId == manager.SessionParams.GridId &&
            action.EntityId == manager.EntityDesc.EntityId;

        bool HasOriginalFilter()
        {
            if (!ReferenceEquals(manager, original.Manager) || current.ManagerVersion != original.ManagerVersion)
            {
                return false;
            }

            var conditions = manager!.ListHandler.GetGridSettings().Conditions;
            return action.Operation switch
            {
                RgfAiFilterOperation.Replace => original.CreateSnapshot == CaptureCreateFilter(conditions),
                RgfAiFilterOperation.ReplaceIfUnchanged => original.ModifySnapshot is not null &&
                    original.ModifySnapshot == CaptureFilter(conditions),
                _ => false
            };
        }
    }

    private async Task ReportFilterLoadFailureAsync(IRgManager? manager, bool stale)
    {
        try
        {
            var toast = manager?.ToastManager ?? services.GetService<IRgfEventNotificationService>()
                ?.GetNotificationManager(RgfToastEventArgs.NotificationManagerScope);
            if (toast is not null)
            {
                await raiseToastAsync(toast, new RgfToastEventArgs("Recroby",
                    recroDict.GetRgfRecrobyString(stale ? "ModifyStaleFilter" : "FilterLoadFailed"),
                    RgfToastType.Error));
            }
        }
        catch (Exception error)
        {
            logWarning(error, "The Recroby filter error notification could not be shown.");
        }
    }
}
