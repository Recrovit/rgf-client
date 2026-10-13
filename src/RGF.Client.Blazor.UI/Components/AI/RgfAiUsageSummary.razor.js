/*!
* RgfAiUsageSummary.razor.js v1.0.0
*/

// RGF-DOC: rgf.client.blazor-ui.ai-chat
const instances = new Map();
let active;

function hidden(element) {
    return !element.isConnected || !!element.closest('[hidden]') || element.getClientRects().length === 0;
}

function stop() {
    if (!active) return;
    active.controller.abort();
    active.resize.disconnect();
    active.mutations.disconnect();
    active.button.setAttribute('aria-expanded', 'false');
    active = undefined;
}

function close() {
    const panel = active?.panel;
    stop();
    if (panel?.matches(':popover-open')) panel.hidePopover();
}

function position() {
    if (!active) return;
    const { element, button, panel } = active;
    if (hidden(element) || !panel.matches(':popover-open')) { close(); return; }
    const viewport = window.visualViewport;
    const left = (viewport?.offsetLeft ?? 0) + 8;
    const top = (viewport?.offsetTop ?? 0) + 8;
    const width = Math.max(0, (viewport?.width ?? window.innerWidth) - 16);
    const height = Math.max(0, (viewport?.height ?? window.innerHeight) - 16);
    const anchor = button.getBoundingClientRect();
    panel.style.width = `${Math.min(320, width)}px`;
    const above = Math.max(0, Math.min(height, anchor.top - top - 4));
    const below = Math.max(0, Math.min(height, top + height - anchor.bottom - 4));
    const upwards = above >= below;
    panel.style.maxHeight = `${upwards ? above : below}px`;
    panel.style.left = `${Math.max(left, Math.min(anchor.left, left + width - panel.offsetWidth))}px`;
    panel.style.top = `${Math.max(top, Math.min(upwards ? anchor.top - panel.offsetHeight - 4 : anchor.bottom + 4, top + height - panel.offsetHeight))}px`;
}

function start(element, button, panel) {
    close();
    if (hidden(element)) { panel.hidePopover(); return; }
    const controller = new AbortController();
    const options = { signal: controller.signal };
    const resize = new ResizeObserver(position);
    const mutations = new MutationObserver(position);
    active = { element, button, panel, controller, resize, mutations };
    button.setAttribute('aria-expanded', 'true');
    resize.observe(element);
    resize.observe(panel);
    for (let ancestor = element.parentElement; ancestor; ancestor = ancestor.parentElement)
        mutations.observe(ancestor, { attributes: true, attributeFilter: ['hidden', 'style', 'class', 'data-mode'] });
    window.addEventListener('resize', position, options);
    window.addEventListener('scroll', position, { ...options, capture: true });
    window.visualViewport?.addEventListener('resize', position, options);
    window.visualViewport?.addEventListener('scroll', position, options);
    position();
    panel.focus({ preventScroll: true });
}

export function sync(element) {
    for (const time of element.querySelectorAll('time[data-local-time], time[data-local-date]')) {
        const dateOnly = time.hasAttribute('data-local-date');
        // DateOnly is a calendar date, not a UTC instant.
        const date = new Date(time.dateTime + (dateOnly ? 'T00:00:00' : ''));
        time.textContent = dateOnly ? date.toLocaleDateString() : date.toLocaleString();
    }
    let state = instances.get(element.id);
    if (!state) {
        state = { element, controller: new AbortController(), panels: new WeakSet() };
        instances.set(element.id, state);
    }
    for (const button of element.querySelectorAll('[popovertarget]')) {
        const panel = element.querySelector(`#${CSS.escape(button.getAttribute('popovertarget'))}`);
        if (!panel || state.panels.has(panel)) continue;
        state.panels.add(panel);
        const options = { signal: state.controller.signal };
        panel.addEventListener('toggle', () => {
            if (panel.matches(':popover-open')) {
                if (active?.panel !== panel) start(element, button, panel);
            } else if (active?.panel === panel) stop();
        }, options);
        panel.addEventListener('keydown', event => {
            if (event.key !== 'Escape') return;
            event.preventDefault();
            event.stopPropagation();
            close();
            button.focus({ preventScroll: true });
        }, options);
    }
    if (active?.element === element) position();
}

export function dispose(id) {
    const state = instances.get(id);
    if (!state) return;
    if (active?.element === state.element) close();
    state.controller.abort();
    instances.delete(id);
}
