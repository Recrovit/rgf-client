/*!
* recrovit-rgf-blazor-ui.js v1.0.0
*/

window.Recrovit = window.Recrovit || { RGF: {} };
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
            $('div.modal-dialog', dialog).draggable();
            if (focusId != null) {
                document.getElementById(focusId).focus();
            }
            else {
                $('.btn-primary:first', dialog).focus();
            }
            if (uniqueName != null) {
                Blazor.UI.Dialog.loadDialogPos(uniqueName, dialogId);
            }
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
        loadDialogPos: function (name, dialogId) {
            const data = localStorage.getItem(`RGF.DialogPos.${name}`);
            if (data != undefined) {
                const dialogPos = JSON.parse(data);
                var content = $('div.modal-content:first', '#' + dialogId);
                content.css({
                    width: `${dialogPos[0]}px`,
                    height: `${dialogPos[1]}px`,
                });
                content.parent('div.modal-dialog').css({
                    top: `${dialogPos[2]}px`,
                    left: `${dialogPos[3]}px`,
                    margin: '0'
                });
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
                gridRef.invokeMethodAsync('SetColumnWidth', idx + 1, parseInt(width));
            });
            rgfTable.makeColumnsDragable(function (idx, newIdx) {
                if (idx != newIdx && idx + 1 != newIdx) {
                    gridRef.invokeMethodAsync('SetColumnPos', idx, newIdx > idx ? newIdx - 1 : newIdx);
                }
            });
        }
    }
};