/*!
* recrovit-rgf-apexcharts.js v1.3.0
*/

window.Recrovit = window.Recrovit || {};
window.Recrovit.RGF = window.Recrovit.RGF || {};
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.ApexCharts = {
    initialize: async function (containerId, chartRef) {
        var container = $(`#${containerId}`);
        var dialog = container.parents('div.modal-content').first();
        if (dialog.resizable('instance') == null) {
            return false;
        }
        dialog.on('resizestop', function (event, ui) {
            RgfApexCharts.resize(containerId, chartRef);
        });
        return true;
    },
    resize: async function (containerId, chartRef, width, height) {
        var container = $(`#${containerId}`).parent();
        if (width == null) {
            width = Math.floor($('.rgf-apexchart-content', container).first().width());
        }
        if (height == null) {
            var h1 = Math.floor(container.height());
            var h2 = h1 - Math.floor($('.rgf-apexchart-header', container).first().outerHeight(true)) | 0;
            height = h2 - Math.floor($('.rgf-apexchart-settings', container).first().outerHeight(true)) | 0;
        }
        await chartRef.invokeMethodAsync('OnResize', width, height);
    },
    createValueFormatter: function (locale) {
        const formatter = new Intl.NumberFormat(locale);

        const formatValue = function (value) {
            if (value === null || value === undefined) {
                return '';
            }

            if (typeof value === 'number') {
                return formatter.format(value);
            }

            return value;
        };

        return function (value) {
            if (Array.isArray(value)) {
                return value.map(formatValue).join('/');
            }

            return formatValue(value);
        };
    },
    createTooltipYFormatter: function (locale) {
        const formatter = new Intl.NumberFormat(locale);

        return function (value) {
            if (value === null || value === undefined) {
                return '';
            }

            return typeof value === 'number'
                ? formatter.format(value)
                : value;
        };
    },
    createLegendFormatter: function (locale) {
        const formatter = new Intl.NumberFormat(locale);

        const formatValue = function (value) {
            if (value === null || value === undefined) {
                return '';
            }

            return typeof value === 'number'
                ? formatter.format(value)
                : value;
        };

        return function (seriesName, opts) {
            const values = opts?.w?.globals?.series?.[opts.seriesIndex];
            const formattedValues = Array.isArray(values)
                ? values.map(formatValue).join('/')
                : formatValue(values);

            return [seriesName, ' - ', formattedValues];
        };
    }
};

var RgfApexCharts = Blazor.ApexCharts;
