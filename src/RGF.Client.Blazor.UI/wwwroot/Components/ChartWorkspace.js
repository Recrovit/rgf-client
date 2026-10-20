// RGF-DOC: rgf.client.blazor-ui.entity-ui
const charts = new Map();

export async function activate(containerId, chartRef) {
    const container = document.getElementById(containerId);
    const host = container?.closest('.rgf-entity-chart');
    if (!container || !host) return;
    let state = charts.get(containerId);
    if (!state) {
        state = { frame: 0, width: 0, height: 0, pending: false, disposed: false };
        const measure = async () => {
            state.frame = 0;
            if (state.disposed || !host.isConnected || host.getClientRects().length === 0) return;
            const width = Math.floor(host.getBoundingClientRect().width);
            const top = host.getBoundingClientRect().top;
            const settings = host.querySelector('.rgf-apexchart-settings');
            const footer = host.querySelector('.rgf-chart-workspace')?.lastElementChild;
            const header = container.querySelector('.rgf-apexchart-header');
            let bottom = window.innerHeight;
            for (let ancestor = host.parentElement; ancestor; ancestor = ancestor.parentElement) {
                if (/(auto|scroll|hidden|clip)/.test(getComputedStyle(ancestor).overflowY) && ancestor.clientHeight > 0) {
                    bottom = Math.min(bottom, ancestor.getBoundingClientRect().bottom);
                }
            }
            const height = Math.max(250, Math.floor(bottom - Math.max(0, top)
                - (settings?.getBoundingClientRect().height || 0)
                - (footer === container ? 0 : footer?.getBoundingClientRect().height || 0)
                - (header?.getBoundingClientRect().height || 0) - 24));
            if (width <= 0 || (width === state.width && height === state.height)) return;
            if (state.pending) { state.schedule(); return; }
            state.width = width;
            state.height = height;
            state.pending = true;
            try {
                await chartRef.invokeMethodAsync('OnResizeCommit', width, height);
            } finally {
                state.pending = false;
            }
        };
        state.schedule = () => {
            if (!state.disposed && !state.frame) state.frame = requestAnimationFrame(measure);
        };
        state.observer = new ResizeObserver(state.schedule);
        state.observer.observe(host);
        if (host.firstElementChild) state.observer.observe(host.firstElementChild);
        window.addEventListener('resize', state.schedule);
        charts.set(containerId, state);
    }
    // A tab may have become visible since the last activation even at the same size.
    state.width = 0;
    state.schedule();
}

export function destroy(containerId) {
    const state = charts.get(containerId);
    if (!state) return;
    state.disposed = true;
    cancelAnimationFrame(state.frame);
    state.observer.disconnect();
    window.removeEventListener('resize', state.schedule);
    charts.delete(containerId);
}

// Update the existing renderer; RenderAsync would destroy and recreate it.
export async function resize(chartId, width, height) {
    if (!chartId || !window.ApexCharts?.getChartByID(chartId)) return;
    await window.ApexCharts.exec(chartId, 'updateOptions', { chart: { width, height } }, false, false);
}
