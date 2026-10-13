using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

// RGF-DOC: rgf.client.blazor-ui.entity-ui

/// <summary>Session and presentation settings for one entity component, including handler references assigned during initialization.</summary>
public class RgfEntityParameters : RgfSessionParams
{
    /// <summary>Creates entity settings, optionally inheriting a parent session.</summary>
    /// <param name="entityName">Backend entity name.</param>
    /// <param name="sessionParam">Optional session context copied by the base contract.</param>
    public RgfEntityParameters(string entityName, RgfSessionParams? sessionParam = null) : base(sessionParam)
    {
        EntityName = entityName;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the initialization process
    /// is deferred and will not run automatically. When set to true, 
    /// the initialization must be manually triggered at a later time.
    /// </summary>
    public bool DeferredInitialization { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the initial entity initialization
    /// should skip the first automatic list data request while still loading
    /// skeleton metadata and initializing the manager.
    /// </summary>
    public bool SkipInitialDataLoad { get; set; }

    /// <summary>Manager assigned by the entity component; may exist before initialization completes.</summary>
    public IRgManager? Manager { get; internal set; }

    /// <summary>Parent entity context for nested UI relationships.</summary>
    public RgfEntityParameters? ParentEntityParameters { get; internal set; }

    /// <summary>Backend entity name supplied at construction.</summary>
    public string EntityName { get; }

    /// <summary>Optional backend selection settings for the initial grid request.</summary>
    public RgfSelectParam? SelectParam { get; set; }

    /// <summary>Optional dialog presentation for this entity.</summary>
    public RenderFragment<RgfDialogParameters>? DialogTemplate { get; set; }

    /// <summary>Toolbar presentation and command events.</summary>
    public RgfToolbarParameters ToolbarParameters { get; set; } = new();

    /// <summary>Grid presentation, events and initial saved settings.</summary>
    public RgfGridParameters GridParameters { get; set; } = new();

    /// <summary>Filter presentation and dialog settings.</summary>
    public RgfFilterParameters FilterParameters { get; set; } = new();

    /// <summary>Paging presentation settings.</summary>
    public RgfPagerParameters PagerParameters { get; set; } = new();

    /// <summary>Form presentation, active view key and events.</summary>
    public RgfFormParameters FormParameters { get; set; } = new();

    /// <summary>Chart presentation and dialog settings.</summary>
    public RgfChartParameters ChartParameters { get; set; } = new();

    /// <summary>Tree presentation and events.</summary>
    public RgfTreeParameters TreeParameters { get; set; } = new();

    /// <summary>Optional title rendered with the initialized manager.</summary>
    public RenderFragment<IRgManager>? TitleTemplate { get; set; }

    /// <summary>Requests form-only presentation; initialization falls back when no unique record can be selected.</summary>
    public bool FormOnly { get; set; }

    /// <summary>Attempts to open the read form when initialization yields one selected record.</summary>
    public bool AutoOpenForm { get; set; }

    /// <summary>Requested grid/tree mode; null uses backend options with a grid fallback.</summary>
    public RfgDisplayMode? DisplayMode { get; set; }

    /// <summary>Initial backend list request settings.</summary>
    public RgfListParam? ListParam { get; set; }

    /// <summary>Parent filter relationship forwarded during initialization.</summary>
    public RgfFilterParent? FilterParent { get; set; }

    /// <summary>Application-defined parameters forwarded on the initial grid request.</summary>
    public Dictionary<string, object>? CustomParameters { get; set; }

    /// <summary>Entity lifecycle and presentation event subscriptions.</summary>
    public RgfEventDispatcher<RgfEntityEventKind, RgfEntityEventArgs> EventDispatcher { get; } = new();

    /// <summary>Removes a subscriber from entity and child dispatchers, recursively including the parent context.</summary>
    public void UnsubscribeFromAll(object subscriber)
    {
        ParentEntityParameters?.UnsubscribeFromAll(subscriber);

        EventDispatcher.Unsubscribe(subscriber);

        FilterParameters.DialogParameters.EventDispatcher.Unsubscribe(subscriber);

        ToolbarParameters.EventDispatcher.Unsubscribe(subscriber);
        ToolbarParameters.MenuEventDispatcher.Unsubscribe(subscriber);

        GridParameters.EventDispatcher.Unsubscribe(subscriber);
        FormParameters.EventDispatcher.Unsubscribe(subscriber);

        ChartParameters.EventDispatcher.Unsubscribe(subscriber);
        ChartParameters.DialogParameters.EventDispatcher.Unsubscribe(subscriber);

        TreeParameters.EventDispatcher.Unsubscribe(subscriber);
    }
}
