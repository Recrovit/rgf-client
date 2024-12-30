using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class GridComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<GridComponent> _logger { get; set; } = default!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = default!;

    private RgfGridComponent _rgfGridRef { get; set; } = default!;

    private ElementReference _tableRef { get; set; }

    private Dictionary<int, ElementReference> _rowRef { get; set; } = new();

    private RgfEntity EntityDesc => Manager.EntityDesc;

    public IRgListHandler ListHandler => Manager.ListHandler;

    private DotNetObjectReference<GridComponent>? _selfRef;

    private List<IDisposable> _disposables { get; set; } = new();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _selfRef = DotNetObjectReference.Create(this);
        GridParameters.EventDispatcher.Subscribe(RgfListEventKind.CreateRowData, OnCreateAttributes);
        GridParameters.EnableMultiRowSelection ??= true;
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe([RgfToolbarEventKind.Read, RgfToolbarEventKind.Edit], OnSetFormItem);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            _disposables.Add(_rgfGridRef.GridDataSource.OnAfterChange(this, OnChangedGridData));
        }
        await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.initializeTable", _selfRef, _tableRef);
    }

    public void Dispose()
    {
        if (_selfRef != null)
        {
            _selfRef.Dispose();
            _selfRef = null;
        }
        if (_disposables != null)
        {
            _disposables.ForEach(disposable => disposable.Dispose());
            _disposables = null!;
        }
        GridParameters.EventDispatcher.Unsubscribe(RgfListEventKind.CreateRowData, OnCreateAttributes);
        EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe([RgfToolbarEventKind.Read, RgfToolbarEventKind.Edit], OnSetFormItem);
    }

    [JSInvokable]
    public void SetColumnWidth(int index, int width) => ListHandler.ReplaceColumnWidth(index, width);

    [JSInvokable]
    public Task SetColumnPos(int from, int to) => ListHandler.MoveColumnAsync(from, to);

    [JSInvokable]
    public string? GetTooltipText(int relativeRowIndex, int colId)
    {
        var absoluteRowIndex = ListHandler.ToAbsoluteRowIndex(relativeRowIndex);
        var tooltip = _rgfGridRef.GetColumnData(absoluteRowIndex, colId)?.ToString();
        if (string.IsNullOrEmpty(tooltip))
        {
            return null;
        }
        var prop = EntityDesc.Properties.FirstOrDefault(e => e.Id == colId);
        if (prop?.ListType == PropertyListType.String)
        {
            tooltip = tooltip?.Replace(Environment.NewLine, "<br>");
        }
        return tooltip;
    }

    protected virtual async Task OnChangedGridData(ObservablePropertyEventArgs<List<RgfDynamicDictionary>> args)
    {
        if (GridParameters.EnableMultiRowSelection != true)
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectAllRow", _tableRef);
        }
    }

    protected virtual Task OnCreateAttributes(IRgfEventArgs<RgfListEventArgs> arg)
    {
        _logger.LogDebug("CreateAttributes");
        var rowData = arg.Args.Data ?? throw new ArgumentException();
        foreach (var prop in EntityDesc.SortedVisibleColumns)
        {
            string? propClass = null;
            if (prop.FormType == PropertyFormType.CheckBox)
            {
                propClass = "rg-center";
            }
            else
            {
                switch (prop.ListType)
                {
                    case PropertyListType.Numeric:
                        propClass = "rg-numeric";
                        break;

                    case PropertyListType.String:
                        propClass = "rg-string";
                        break;

                    case PropertyListType.Image:
                        propClass = "rg-html";
                        break;
                }
            }
            var attributes = rowData.GetOrNew<RgfDynamicDictionary>("__attributes");
            var propAttributes = attributes.GetOrNew<RgfDynamicDictionary>(prop.Alias);
            if (propClass != null)
            {
                propAttributes.Set<string>("class", (old) => string.IsNullOrEmpty(old) ? propClass : old.EnsureContains(propClass, ' '));
            }
            if (prop.Options?.GetBoolValue("RGO_EnableGridDataTooltip") == true)
            {
                var tt = rowData.GetMember(prop.Alias)?.ToString();
                if (!string.IsNullOrEmpty(tt))
                {
                    propAttributes.SetMember("data-bs-toggle", "tooltip");
                }
            }
        }
        return Task.CompletedTask;
    }

    private async Task OnSort(MouseEventArgs args, RgfProperty property)
    {
        var dict = new Dictionary<string, int>();
        if (args.ShiftKey)
        {
            int idx = 1;
            bool add = true;
            foreach (var item in EntityDesc.SortColumns)
            {
                int sort = item.Sort;
                if (property.Id == item.Id)
                {
                    add = false;
                    if (!args.CtrlKey)
                    {
                        //remove
                        continue;
                    }
                    dict.Add(item.Alias, item.Sort > 0 ? -idx : idx);//reverse
                }
                else
                {
                    dict.Add(item.Alias, item.Sort > 0 ? idx : -idx);
                }
                idx++;
            }
            if (add)
            {
                dict.Add(property.Alias, args.CtrlKey ? -idx : idx);
            }
        }
        else
        {
            if (EntityDesc.SortColumns.Count() == 1 && EntityDesc.SortColumns.Single().Id == property.Id)
            {
                dict.Add(property.Alias, -EntityDesc.SortColumns.Single().Sort);
            }
            else
            {
                dict.Add(property.Alias, args.CtrlKey ? -1 : 1);
            }
        }
        await ListHandler.SetSortAsync(dict);
    }

    protected virtual async Task OnRowClick(MouseEventArgs args, RgfDynamicDictionary rowData, int relativeRowIndex)
    {
        if (args.Detail == 1)
        {
            if (_rgfGridRef.SelectedItems.Any())
            {
                int absoluteRowIndex = ListHandler.GetAbsoluteRowIndex(rowData);
                int selectedItemsCount = _rgfGridRef.SelectedItems.Count;
                bool deselect = _rgfGridRef.SelectedItems.ContainsKey(absoluteRowIndex);
                if (GridParameters.EnableMultiRowSelection == true && (args.CtrlKey || args.ShiftKey))
                {
                    if (args.ShiftKey)
                    {
                        int minIdx = _rgfGridRef.SelectedItems.Keys.Where(key => key < absoluteRowIndex).DefaultIfEmpty(-1).Max();
                        if (minIdx >= 0)
                        {
                            for (int i = minIdx + 1; i < absoluteRowIndex; i++)
                            {
                                var data = await ListHandler.EnsureVisibleAsync(i);
                                if (data != null)
                                {
                                    await _rgfGridRef.RowSelectHandlerAsync(data);
                                    int idx = ListHandler.GetRelativeRowIndex(data);
                                    await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.selectRow", _rowRef[idx], idx);
                                }
                            }
                        }
                    }
                    else if (deselect)
                    {
                        await _rgfGridRef.RowDeselectHandlerAsync(rowData);
                        int rowIndex = Manager.ListHandler.ToRelativeRowIndex(absoluteRowIndex);
                        if (rowIndex != -1)
                        {
                            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectRow", _rowRef[rowIndex], rowIndex);
                        }
                        return;
                    }
                }
                else
                {
                    await _rgfGridRef.RowDeselectHandlerAsync(null);
                    await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectAllRow", _tableRef);
                    if (deselect && selectedItemsCount == 1)
                    {
                        return;
                    }
                }
            }
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.selectRow", _rowRef[relativeRowIndex], relativeRowIndex);
            await _rgfGridRef.RowSelectHandlerAsync(rowData);
        }
        else
        {
            await _rgfGridRef.OnRecordDoubleClickAsync(rowData);
        }
    }

    private async Task OnSetFormItem(IRgfEventArgs<RgfToolbarEventArgs> arg)
    {
        var data = _rgfGridRef.SelectedItems.Single();
        int rowIndex = Manager.ListHandler.ToRelativeRowIndex(data.Key);
        if (rowIndex != -1)
        {
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectAllRow", _tableRef);
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.selectRow", _rowRef[rowIndex], rowIndex);
        }
    }
}