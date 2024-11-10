/*!
* recrovit-rgf-blazor-ui.js v1.3.2
*/

window.Recrovit = window.Recrovit || {};
window.Recrovit.RGF = window.Recrovit.RGF || {};
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.UI = {
    Dialog: {
        initialize: function (dialogId, resizable, uniqueName, focusId) {
            var dialog = document.getElementById(dialogId);
            //var d = new bootstrap.Modal(`#${dialogId}`, { keyboard: false });
            if (resizable) {
                $('div.modal-content', dialog).resizable();
            }
            $('div.modal-dialog', dialog).draggable({ handle: '.modal-header, .dialog-header' });
            if (focusId != null) {
                document.getElementById(focusId).focus();
            }
            else {
                $('.btn-primary:first', dialog).focus();
            }
            $('div.modal-dialog', dialog).height('auto');
            Blazor.UI.Dialog.loadDialogPos(uniqueName, dialogId, true);
        },
        saveDialogPos: function (name, dialogId) {
            const key = `RGF.DialogPos.${name}`;
            if (dialogId == undefined) {
                localStorage.removeItem(key);
            }
            else {
                var $element = $('div.modal-content:first', $('#' + dialogId));
                const dialogPos = [4];
                dialogPos[0] = parseInt($element.css('width'));
                dialogPos[1] = parseInt($element.css('height'));
                $element = $element.parent('div.modal-dialog');
                dialogPos[2] = parseInt($element.offset().top);
                dialogPos[3] = parseInt($element.offset().left);
                localStorage.setItem(key, JSON.stringify(dialogPos));
            }
        },
        loadDialogPos: function (name, dialogId, verticalCenter) {
            var content = $('div.modal-content:first', '#' + dialogId);
            var dialog = content.parent('div.modal-dialog');
            if (name != null) {
                var data = localStorage.getItem(`RGF.DialogPos.${name}`);
                if (data != undefined) {
                    const dialogPos = JSON.parse(data);
                    content.css({
                        width: `${dialogPos[0]}px`,
                        height: `${dialogPos[1]}px`,
                    });
                    dialog.css({
                        top: `${dialogPos[2] < 0 ? 0 : dialogPos[2]}px`,
                        left: `${dialogPos[3] < 0 ? 0 : dialogPos[3]}px`,
                        margin: '0'
                    });
                    return;
                }
                dialog.css('width', '60%');
            }
            if (verticalCenter == true) {
                var windowHeight = $(window).height();
                var dialogHeight = dialog.height();
                var top = ((windowHeight - dialogHeight) / 2).toFixed(0);
                if (top > 0) {
                    dialog.css('margin-top', top + 'px');
                }
            }
        }
    },
    Grid: {
        selectRow: function (row, idx) {
            //$(table).find('tr').eq(idx).addClass('table-active');
            $(row).addClass('table-active');
        },
        deselectAllRow: function (table) {
            $('tr', table).removeClass('table-active');
        },
        initializeTable: function (gridRef, table) {
            var rgfTable = new Recrovit.WebCli.RgfTable(table);
            rgfTable.makeColumnsResizable(function (idx, target, width) {
                gridRef.invokeMethodAsync('SetColumnWidth', idx + 1, parseInt(width) || 0);
            });
            rgfTable.makeColumnsDragable(function (idx, newIdx) {
                if (idx != newIdx && idx + 1 != newIdx) {
                    gridRef.invokeMethodAsync('SetColumnPos', idx, newIdx > idx ? newIdx - 1 : newIdx);
                }
            });
            BlazorGrids.initializeTooltips(gridRef);
        },
        initializeTooltips: function (gridRef) {
            var tooltipTriggerArr = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            tooltipTriggerArr.forEach(function (element) {
                var tooltip = new bootstrap.Tooltip(element, {
                    title: element.innerText,
                    customClass: 'rgf-cell-tooltip',
                    trigger: 'hover',
                    delay: { show: 500 },
                    html: true
                });
                element.addEventListener('show.bs.tooltip', async function () {
                    if (tooltip.tooltipText == null) {
                        var col = $(this).attr('data-cell');
                        var rowIdx = $(this).parent('tr').attr('data-row');
                        tooltip.tooltipText = await gridRef.invokeMethodAsync('GetTooltipText', parseInt(rowIdx), parseInt(col));
                        if (tooltip.tooltipText == null) {
                            tooltip.tooltipText = this.innerText;
                        }
                        tooltip.setContent({ '.tooltip-inner': tooltip.tooltipText })
                    }
                    setTimeout(function () { tooltip.hide(); }, 8000);
                });
            });
        }
    },
    Chart: {
        initialize: async function (containerId, chartRef) {
            Blazor.ApexCharts.onResize = async function (element, chartRef) {
                var container = $(element);
                var w = Math.round($('div.card-body', container).first().width());
                var h1 = Math.round(container.height());
                var h = h1 - Math.round($('div.card-header', container).first().height());
                await chartRef.invokeMethodAsync('Resize', w - 1, h - 16 - 50);
            };
            return await Blazor.ApexCharts.initialize(containerId, chartRef);
        }
    },
    ListBox: {
        resizable: function (listBoxId, width, height) {
            var element = $(`#${listBoxId}`);
            if (width == null) {
                element.width(element.width());
            }
            if (height == null) {
                element.height(element.height());
            }
            var outerWidth = element.outerWidth() + 29;
            var outerHeight = element.outerHeight() + 26;
            element.resizable({
                minWidth: 130,
                minHeight: 61,
                create: function (event, ui) {
                    $(this).resizable("resizeTo", { width: outerWidth, height: outerHeight });
                },
                stop: function (event, ui) {
                    $(this).css({ width: '', height: '' });
                }
            });
        }
    },
    ComboBox: {
        initialize: function (dotNetRef, comboBoxId, value, width) {
            var combo = $(`#${comboBoxId}`).rgcombobox({
                value: value,
                inputClass: 'rgf-combobox-edit form-control form-control-sm',
                button: '<button class="rgf-combobox-button btn btn-outline-secondary" type="button" rgf-bs-combobox=""><i class="bi bi-caret-down-fill"></i></button>',
                noWrapper: true,
                calcWidth: false,
                width: width
            });
            combo.rgcombobox('instance').input.autocomplete('widget').css('z-index', 5000);
            combo.on('change.RGF-Client-Blazor-UI', function (event) {
                var $this = $(this);
                if (event.originalEvent?.type == 'keyup' && event.originalEvent?.key == "Enter") {
                    var text = $this.rgcombobox("instance").input.val();
                    dotNetRef.invokeMethodAsync('OnEnter', text);
                }
                else {
                    var selected = $this.find(":selected");
                    if (selected.length == 1) {
                        var value = selected.val();
                        dotNetRef.invokeMethodAsync('OnSelected', value);
                    }
                    else {
                        var text = $this.rgcombobox("instance").input.val();
                        dotNetRef.invokeMethodAsync('OnChanged', text);
                    }
                }
            });
        },
        setText: function (comboBoxId, text) {
            $(`#${comboBoxId}`).rgcombobox("instance").input.val(text);
        },
        destroy: function (comboBoxId) {
            $(`#${comboBoxId}`).off('change.RGF-Client-Blazor-UI').rgcombobox("destroy");
        }
    }
};

const BlazorGrids = Blazor.UI.Grid;
