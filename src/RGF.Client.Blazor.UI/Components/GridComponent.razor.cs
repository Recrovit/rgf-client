using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Handlers;
using System;
using System.Linq;

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

    private bool _isDblClick { get; set; }

    public IRgListHandler ListHandler => Manager.ListHandler;

    private DotNetObjectReference<GridComponent>? _selfRef;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _selfRef = DotNetObjectReference.Create(this);
        GridParameters.Events.CreateAttributes.Subscribe(OnCreateAttributes);
    }

    public void Dispose()
    {
        if (_selfRef != null)
        {
            _selfRef.Dispose();
            _selfRef = null;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            _rgfGridRef.Disposables.Add(Manager.ListHandler.GridData.OnAfterChange(this, OnChangeGridData));
        }
        await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.initializeTable", _selfRef, _tableRef);
    }

    [JSInvokable]
    public void SetColumnWidth(int index, int width) => ListHandler.ReplaceColumnWidth(index, width);

    [JSInvokable]
    public Task SetColumnPos(int from, int to) => ListHandler.MoveColumnAsync(from, to);

    protected virtual async Task OnChangeGridData(ObservablePropertyEventArgs<List<RgfDynamicDictionary>> args)
    {
        await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectAllRow", _tableRef);
    }

    protected virtual Task OnCreateAttributes(DataEventArgs<RgfDynamicDictionary> rowData)
    {
        foreach (var prop in EntityDesc.SortedVisibleColumns)
        {
            var attr = rowData.Value["__attributes"] as RgfDynamicDictionary;
            if (attr != null)
            {
                string? propAttr = null;
                if (prop.FormType == PropertyFormType.CheckBox)
                {
                    propAttr = " rg-center";
                }
                else
                {
                    switch (prop.ListType)
                    {
                        case PropertyListType.Numeric:
                            propAttr = " rg-numeric";
                            break;

                        case PropertyListType.String:
                            propAttr = " rg-string";
                            break;

                        case PropertyListType.Image:
                            propAttr = " rg-html";
                            break;
                    }
                }
                if (propAttr != null)
                {
                    attr[$"class-{prop.Alias}"] += propAttr;
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

    protected virtual async Task OnRowClick(MouseEventArgs args, RgfDynamicDictionary rowData, int rowIndex)
    {
        _isDblClick = false;
        await Task.Delay(200);
        if (!_isDblClick)
        {
            if (_rgfGridRef.SelectedItems.Any())
            {
                bool deselect = _rgfGridRef.SelectedItems[0] == rowData;
                await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectAllRow", _tableRef);
                await _rgfGridRef.RowDeselectHandlerAsync(rowData);
                if (deselect)
                {
                    return;
                }
            }
            await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.selectRow", _rowRef[rowIndex], rowIndex);
            await _rgfGridRef.RowSelectHandlerAsync(rowData);
        }
    }

    protected virtual async Task OnRowDoubleClick(MouseEventArgs args, RgfDynamicDictionary rowData, int rowIndex)
    {
        _isDblClick = true;
        await _rgfGridRef.OnRecordDoubleClickAsync(rowData);
        await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.deselectAllRow", _tableRef);
        await _jsRuntime.InvokeVoidAsync(RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".Grid.selectRow", _rowRef[rowIndex], rowIndex);
    }
}
