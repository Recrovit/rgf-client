/*!
* recrovit-rgf-blazor-ui.js v1.12.0
*/

window.Recrovit = window.Recrovit || {};
window.Recrovit.RGF = window.Recrovit.RGF || {};
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.UI = {
    Base: {
        setFocus: function (selector) {
            var element = $(selector);
            if (element.length) {
                element.focus();
                return true;
            }
            return false;
        },
        ensureVisible: async function (selector, setFocus = false, closestSelector = null, duration = 500, offset = 20) {
            try {
                var element = $(selector);
                if (element.length && element.is(':visible')) {

                    if (closestSelector) {
                        var e2 = element.closest(closestSelector);
                        if (e2.length) {
                            element = e2;
                        }
                    }

                    var windowTop = $(window).scrollTop(),
                        windowHeight = $(window).height(),
                        windowBottom = windowTop + windowHeight,
                        elementTop = element.offset().top,
                        elementBottom = elementTop + element.outerHeight(),
                        elementHeight = element.outerHeight();

                    if (elementTop >= windowTop && elementBottom <= windowBottom) {
                        if (setFocus) {
                            element.focus();
                        }
                        return true;
                    }

                    var scrollTo = elementTop - offset;
                    if (elementBottom > windowBottom && elementHeight < windowHeight) {
                        scrollTo = elementBottom - windowHeight + offset;
                    }

                    await new Promise((resolve, reject) => {
                        $('html, body').stop(true, true).animate({ scrollTop: scrollTo }, duration, function () {
                            if (setFocus) {
                                element.focus();
                            }
                            resolve();
                        });
                    });
                    return true;
                }
                return false;
            }
            catch (error) {
                return false;
            }
        },
        tooltip: function (element, options) {
            var $element = $(element);
            if ($element.length !== 1) return null;

            var tooltipInstance = bootstrap.Tooltip.getInstance($element[0]);
            if ($element.is(':disabled') || !options || !options.title) {
                tooltipInstance?.dispose();
                return null;
            }

            if (!tooltipInstance) {
                tooltipInstance = new bootstrap.Tooltip($element[0], {
                    title: options.title,
                    customClass: options.customClass || 'rgf-tooltip-400',
                    placement: options.placement || 'top',
                    trigger: options.trigger || 'hover',
                    html: options.allowHtml ?? false,
                    delay: {
                        show: options.delayShow ?? 500,
                        hide: options.delayHide ?? 100
                    }
                });
            }
            else {
                tooltipInstance.setContent({ '.tooltip-inner': options.title });
            }
            return tooltipInstance;
        },
        registerKeydown: function (dotNetObjRef, selector, keysToPrevent) {
            if (selector) {
                var targetElement = $(selector);
                targetElement.on('keydown.RgfUI', function (e) {
                    if (keysToPrevent && keysToPrevent.includes(e.key)) {
                        e.preventDefault();
                        e.stopPropagation();
                    }
                    var keyboardEventArgs = {
                        key: e.key,
                        code: e.code,
                        location: e.location,
                        repeat: e.repeat,
                        ctrlKey: e.ctrlKey,
                        shiftKey: e.shiftKey,
                        altKey: e.altKey,
                        metaKey: e.metaKey,
                        keyCode: e.keyCode,
                        type: e.type
                    };
                    dotNetObjRef.invokeMethodAsync('OnKeyDownJsCallback', keyboardEventArgs);
                });
            }
        },
        unregisterKeydown: function (selector) {
            if (selector) {
                $(selector).off('keydown.RgfUI');
            }
        }
    },
    Dialog: {
        initialize: function (dialogId, resizable, uniqueName, focusId, isInline) {
            var dialog = document.getElementById(dialogId);
            if (!isInline) {
                $('div.modal-dialog', dialog).draggable({ handle: '.modal-header, .dialog-header' });
                $('div.modal-dialog', dialog).height('auto');
                Blazor.UI.Dialog.loadDialogPos(uniqueName, dialogId, true);
                if (resizable) {
                    var dialogContent = $('div.modal-content', dialog).first();
                    Recrovit.LPUtils.ResizableWithResponsiveFlex(dialogContent);
                    window.setTimeout(function () {
                        Recrovit.LPUtils.ResizeResponsiveFlex(dialogContent);
                    }, 1000);
                }
            }
            if (focusId != null) {
                document.getElementById(focusId).focus();
            }
            else {
                $('.btn-primary:first', dialog).focus();
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
                dialogPos[2] = parseInt($element.offset().top) - window.scrollY;
                dialogPos[3] = parseInt($element.offset().left) - window.scrollX;
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

                    var top = dialogPos[2] < 0 ? 25 : dialogPos[2];
                    var left = dialogPos[3] < 0 ? 25 : dialogPos[3];

                    if ($(window).height() < top + 50) {
                        top = 25;
                    }
                    if ($(window).width() < left + 50) {
                        left = 25;
                    }

                    dialog.css({
                        top: `${top}px`,
                        left: `${left}px`,
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
            //$(table).find('tr').eq(idx).addClass('table-primary');
            $(row).addClass('table-primary');
        },
        deselectRow: function (row, idx) {
            $(row).removeClass('table-primary');
        },
        deselectAllRow: function (table) {
            $('tr.table-primary', table).removeClass('table-primary');
        },
        initializeTable: function (gridRef, table) {
            var rgfTable = new Recrovit.WebCli.RgfTable(table);
            rgfTable.makeColumnsResizable(function (idx, target, width) {
                bootstrap.Tooltip.getInstance(target)?.dispose();
                gridRef.invokeMethodAsync('SetColumnWidth', idx + 1, parseInt(width) || 0);
            });
            rgfTable.makeColumnsDragable(function (idx, newIdx) {
                if (idx != newIdx && idx + 1 != newIdx) {
                    gridRef.invokeMethodAsync('SetColumnPos', idx, newIdx > idx ? newIdx - 1 : newIdx);
                }
            });
            $('th', rgfTable.get_thead()).each(function () {
                $('div.ui-draggable', this).on('dragstart', function (event) {
                    bootstrap.Tooltip.getInstance($(event.target).closest('th')[0])?.dispose();
                });
            });
            BlazorGrids.initializeTooltips(gridRef, table);
        },
        initializeTooltips: function (gridRef, table) {
            $('td', table).each(function () {
                var element = $(this);
                element.off('show.bs.tooltip');
                bootstrap.Tooltip.getInstance(element[0])?.dispose();
            });
            var tooltipTriggerArr = $('td[data-bs-toggle="tooltip"]', table);
            tooltipTriggerArr.each(function () {
                var element = $(this);
                var tooltip = new bootstrap.Tooltip(element[0], {
                    title: element.text(),
                    customClass: 'rgf-tooltip-800 rgf-maxw-50',
                    trigger: 'hover',
                    delay: { show: 500 },
                    html: true
                });
                element.on('show.bs.tooltip', async function () {
                    if (tooltip.tooltipText == null) {
                        var col = element.attr('data-cell');
                        var rowIdx = element.closest('tr').attr('data-row');
                        tooltip.tooltipText = await gridRef.invokeMethodAsync('GetTooltipText', parseInt(rowIdx), parseInt(col));
                        if (tooltip.tooltipText == null) {
                            tooltip.tooltipText = element.text();
                        }
                        tooltip.setContent({ '.tooltip-inner': tooltip.tooltipText })
                    }
                    setTimeout(function () { tooltip.hide(); }, 8000);
                });
            });
        }
    },
    Chart: {
        _instances: {},
        _commitFrameId: 0,
        _measure: function (hostId, containerId) {
            var host = document.getElementById(hostId);
            var container = document.getElementById(containerId);
            if (host == null || container == null || !$(container).is(':visible')) {
                return null;
            }

            var hostRect = host.getBoundingClientRect();
            var width = Math.round(hostRect.width || host.clientWidth || 0);
            var height = Math.round(hostRect.height || host.clientHeight || 0);
            if (width <= 0 || height <= 0) {
                return null;
            }

            var containerStyle = window.getComputedStyle(container);
            width -= Math.round(parseFloat(containerStyle.borderLeftWidth) || 0);
            width -= Math.round(parseFloat(containerStyle.borderRightWidth) || 0);
            height -= Math.round(parseFloat(containerStyle.borderTopWidth) || 0);
            height -= Math.round(parseFloat(containerStyle.borderBottomWidth) || 0);

            var header = $('.rgf-apexchart-header:visible', container).first();
            if (header.length === 1) {
                height -= Math.round(header.outerHeight(true) || 0);
            }

            var settings = $('.rgf-apexchart-settings:visible', container).first();
            if (settings.length === 1) {
                height -= Math.round(settings.outerHeight(true) || 0);
            }

            var body = $('div.card-body', container).first()[0];
            if (body != null) {
                var bodyStyle = window.getComputedStyle(body);
                width -= Math.round(parseFloat(bodyStyle.paddingLeft) || 0);
                width -= Math.round(parseFloat(bodyStyle.paddingRight) || 0);
                height -= Math.round(parseFloat(bodyStyle.paddingTop) || 0);
                height -= Math.round(parseFloat(bodyStyle.paddingBottom) || 0);
            }

            if (width <= 0 || height <= 0) {
                return null;
            }

            return { width: width, height: height };
        },
        _queueResize: function (hostId, containerId, chartRef) {
            var chart = Blazor.UI.Chart;
            var instance = chart._instances[containerId];
            if (instance == null || instance.frameRequested) {
                return;
            }

            instance.frameRequested = true;
            instance.frameId = window.requestAnimationFrame(async function () {
                instance.frameRequested = false;
                instance.frameId = 0;

                var size = chart._measure(hostId, containerId);
                if (size == null) {
                    return;
                }

                if (instance.lastWidth === size.width && instance.lastHeight === size.height) {
                    return;
                }

                instance.lastWidth = size.width;
                instance.lastHeight = size.height;

                await chartRef.invokeMethodAsync('OnResizePreview', size.width, size.height);
            });

            if (instance.commitTimeoutId) {
                window.clearTimeout(instance.commitTimeoutId);
            }

            instance.commitTimeoutId = window.setTimeout(async function () {
                instance.commitTimeoutId = 0;
                await chart.commitResize(hostId, containerId, chartRef);
            }, 150);
        },
        resizePreview: async function (hostId, containerId, chartRef) {
            var size = Blazor.UI.Chart._measure(hostId, containerId);
            if (size == null) {
                return false;
            }

            var instance = Blazor.UI.Chart._instances[containerId];
            if (instance != null) {
                instance.lastWidth = size.width;
                instance.lastHeight = size.height;
            }

            await chartRef.invokeMethodAsync('OnResizePreview', size.width, size.height);
            return true;
        },
        commitResize: async function (hostId, containerId, chartRef) {
            var chart = Blazor.UI.Chart;
            var instance = chart._instances[containerId];
            var size = chart._measure(hostId, containerId);
            if (size == null) {
                return false;
            }

            if (instance != null) {
                instance.lastWidth = size.width;
                instance.lastHeight = size.height;

                if (instance.committedWidth === size.width && instance.committedHeight === size.height) {
                    return false;
                }

                instance.committedWidth = size.width;
                instance.committedHeight = size.height;
            }

            await chartRef.invokeMethodAsync('OnResizeCommit', size.width, size.height);
            return true;
        },
        initialize: async function (hostId, containerId, chartRef) {
            var chart = Blazor.UI.Chart;
            var host = document.getElementById(hostId);
            var container = document.getElementById(containerId);
            if (host == null || container == null || typeof ResizeObserver === 'undefined') {
                return false;
            }

            chart.destroy(containerId);

            var instance = {
                frameId: 0,
                frameRequested: false,
                commitTimeoutId: 0,
                lastWidth: null,
                lastHeight: null,
                committedWidth: null,
                committedHeight: null,
                hostId: hostId,
                chartRef: chartRef,
                observer: null
            };

            instance.observer = new ResizeObserver(function () {
                chart._queueResize(hostId, containerId, chartRef);
            });
            instance.observer.observe(host);
            chart._instances[containerId] = instance;

            return true;
        },
        commitAll: function () {
            var chart = Blazor.UI.Chart;
            if (chart._commitFrameId) {
                window.cancelAnimationFrame(chart._commitFrameId);
            }

            chart._commitFrameId = window.requestAnimationFrame(async function () {
                chart._commitFrameId = 0;

                var entries = Object.entries(chart._instances);
                for (const [containerId, instance] of entries) {
                    if (instance == null || instance.chartRef == null || instance.hostId == null) {
                        continue;
                    }

                    await chart.commitResize(instance.hostId, containerId, instance.chartRef);
                }
            });
        },
        destroy: function (containerId) {
            var instance = Blazor.UI.Chart._instances[containerId];
            if (instance == null) {
                return;
            }

            if (instance.observer != null) {
                instance.observer.disconnect();
            }
            if (instance.frameId) {
                window.cancelAnimationFrame(instance.frameId);
            }
            if (instance.commitTimeoutId) {
                window.clearTimeout(instance.commitTimeoutId);
            }

            delete Blazor.UI.Chart._instances[containerId];
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
        },
        resizableDestroy: function (listBoxId) {
            $(`#${listBoxId}`).resizable('destroy');
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
            $(`#${comboBoxId}`).rgcombobox('instance').input.val(text);
        },
        clearText: function (comboBoxId) {
            $(`#${comboBoxId}`).val('');
        },
        destroy: function (comboBoxId) {
            $(`#${comboBoxId}`).off('change.RGF-Client-Blazor-UI').rgcombobox('destroy');
        }
    },
    SetTheme: {
        getSettings: function (themeKeys, sizeKeys) {
            const currentTheme = document.documentElement.getAttribute('data-bs-theme') ?? '';
            const theme = themeKeys.includes(currentTheme)
                ? currentTheme
                : (themeKeys[0] ?? '');

            const size = Array.from(document.documentElement.classList).find(value => sizeKeys.includes(value))
                ?? (sizeKeys.includes('') ? '' : (sizeKeys[0] ?? ''));

            return { theme, size };
        },
        setTheme: function (themeName) {
            document.documentElement.setAttribute('data-bs-theme', themeName ?? '');
        },
        setSize: function (oldValue, newValue) {
            if (oldValue) {
                document.documentElement.classList.remove(oldValue);
            }

            if (newValue) {
                document.documentElement.classList.add(newValue);
            }
        }
    },
    Menu: {
        hide: function (element) {
            $(element).removeClass('show');
        },
        hideOffcanvas: function (element) {
            if (!element || typeof bootstrap === 'undefined' || !bootstrap.Offcanvas) {
                return;
            }

            // Responsive offcanvas elements become static containers at their
            // desktop breakpoint. Hiding one there would also hide the navbar.
            if (window.getComputedStyle(element).position !== 'fixed') {
                return;
            }

            var offcanvas = bootstrap.Offcanvas.getInstance(element);
            if (!offcanvas || !element.classList.contains('show')) {
                return;
            }

            offcanvas.hide();
        }
    },
    Dashboard: {
        _viewportMonitors: new Map(),
        initializeViewportMonitor: function (dotNetRef, maxWidth) {
            if (!dotNetRef || typeof window.matchMedia !== 'function') {
                return false;
            }

            var mediaQuery = window.matchMedia(`(max-width: ${maxWidth}px)`);
            var entry = {
                dotNetRef: dotNetRef,
                mediaQuery: mediaQuery,
                handler: function (event) {
                    dotNetRef.invokeMethodAsync('OnViewportEditModeChanged', event.matches);
                }
            };

            if (typeof mediaQuery.addEventListener === 'function') {
                mediaQuery.addEventListener('change', entry.handler);
            }
            else if (typeof mediaQuery.addListener === 'function') {
                mediaQuery.addListener(entry.handler);
            }

            Blazor.UI.Dashboard._viewportMonitors.set(dotNetRef, entry);
            return mediaQuery.matches;
        },
        destroyViewportMonitor: function (dotNetRef) {
            if (!dotNetRef) {
                return;
            }

            var entry = Blazor.UI.Dashboard._viewportMonitors.get(dotNetRef);
            if (!entry) {
                return;
            }

            if (typeof entry.mediaQuery.removeEventListener === 'function') {
                entry.mediaQuery.removeEventListener('change', entry.handler);
            }
            else if (typeof entry.mediaQuery.removeListener === 'function') {
                entry.mediaQuery.removeListener(entry.handler);
            }

            Blazor.UI.Dashboard._viewportMonitors.delete(dotNetRef);
        },
        initializeRootResizable: function (element, dotNetRef, options) {
            var $element = $(element);
            if ($element.length !== 1) {
                return;
            }

            if (options?.readOnly) {
                Blazor.UI.Dashboard.destroyRootResizable(element);
                return;
            }

            if ($element.resizable('instance')) {
                $element.resizable('destroy');
            }

            $element.resizable({
                handles: 'se',
                minWidth: options?.minWidth ?? 320,
                minHeight: options?.minHeight ?? 240,
                stop: function () {
                    var width = Math.round($element.outerWidth() || 0);
                    var height = Math.round($element.outerHeight() || 0);
                    dotNetRef.invokeMethodAsync('OnRootSizeChangedJsCallback', width > 0 ? width : null, height > 0 ? height : null);
                }
            });
        },
        destroyRootResizable: function (element) {
            var $element = $(element);
            if ($element.length !== 1) {
                return;
            }

            if ($element.resizable('instance')) {
                $element.resizable('destroy');
            }
        }
    },
    Splitter: {
        initialize: function (container) {
            var $sp = $(container).children('.rgf-splitter');
            $sp.off('mousedown.rgfSplitter');
            if ($sp.prop('data-splitter-disabled')) return;

            $sp.on('mousedown.rgfSplitter', function () {
                var $splitter = $(this);

                const isHorizontal = $splitter.parent().hasClass('horizontal');
                const minSize = isHorizontal ? 100 : 50;

                var $container = $splitter.parent(),
                    $primaryPanel = $splitter.prev(),
                    $secondaryPanel = $splitter.next();

                var isResizing = true;
                $('body').css('cursor', isHorizontal ? 'ew-resize' : 'ns-resize');

                $(document).on('mousemove.rgfSplitter', function (event) {
                    if (!isResizing) return;

                    var newPrimarySize;
                    var newSecondarySize;

                    if (isHorizontal) {
                        var containerOffset = $container.offset().left,
                            containerSize = $container.width();

                        newPrimarySize = event.pageX - containerOffset;
                        newSecondarySize = containerSize - newPrimarySize - $splitter.outerWidth(true);
                        if ($container.resizable('instance')) {
                            $container.resizable('option', 'minWidth', newPrimarySize + minSize);
                        }
                    }
                    else {
                        var containerOffset = $container.offset().top,
                            containerSize = $container.height();

                        newPrimarySize = event.pageY - containerOffset;
                        newSecondarySize = containerSize - newPrimarySize - $splitter.outerHeight(true);
                        if ($container.resizable('instance')) {
                            $container.resizable('option', 'minHeight', newPrimarySize + minSize);
                        }
                    }

                    if (newPrimarySize > minSize && newSecondarySize > minSize) {
                        $primaryPanel.css('flex', `0 0 ${newPrimarySize}px`);
                        $secondaryPanel.css('flex', `0 0 ${newSecondarySize}px`);
                        BlazorSplitter.clearSiblingFlex($primaryPanel, isHorizontal);
                    }
                });

                $(document).on('mouseup.rgfSplitter', function () {
                    isResizing = false;
                    $('body').css('cursor', '');
                    $(document).off("mousemove.rgfSplitter mouseup.rgfSplitter");
                });
            });
        },
        clearSiblingFlex: function ($panel, horizontal) {
            if ($panel.length == 0) {
                return;
            }
            var $container = $panel?.children('.rgf-splitter-wrapper');
            if ($container.length > 0) {
                if (horizontal && $container.hasClass('horizontal') ||
                    !horizontal && $container.hasClass('vertical')) {
                    $container.children('div.rgf-splitter-flex-2').css('flex', '');
                    return;
                }
                BlazorSplitter.clearSiblingFlex($container.children('div.rgf-splitter-flex-1'), horizontal);
                BlazorSplitter.clearSiblingFlex($container.children('div.rgf-splitter-flex-2'), horizontal);
            }
        },
        resizable: function (container) {
            if ($(container).resizable('instance')) {
                return;
            }
            $(container).resizable({
                resize: function (event, ui) {
                    $(this).find('div.rgf-splitter-flex-1, div.rgf-splitter-flex-2').css('flex', '');
                }
            });
        },
        disable: function (container) {
            var $container = $(container),
                $splitter = $container.children('.rgf-splitter'),
                $primaryPanel = $splitter.prev(),
                $secondaryPanel = $splitter.next();

            $primaryPanel.css('flex', '');
            $secondaryPanel.css('flex', '');
            if ($container.resizable('instance')) {
                $container.resizable("destroy");
            }
        }
    }
};

const BlazorBase = Blazor.UI.Base;
const BlazorGrids = Blazor.UI.Grid;
const BlazorSplitter = Blazor.UI.Splitter;
