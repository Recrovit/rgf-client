/*!
* RgfRecrobyWorkspace.razor.js v1.0.0
*/

// RGF-DOC: rgf.client.recroby.integration
const instances = new WeakMap();
const modes = ["Floating", "DockLeft", "DockRight", "DockBottom"];
const storageKey = "rgf.recroby.workspace.v1";
const clamp = (value, min, max) => Math.max(min, Math.min(value, Math.max(min, max)));

export function normalize(settings) {
    const defaults = { mode: 0, isCollapsed: true, x: 48, y: 80, width: 440, height: 560, dockWidth: 400, dockHeight: 320 };
    const result = { ...defaults };
    for (const key of ["x", "y", "width", "height", "dockWidth", "dockHeight"])
        if (Number.isFinite(settings?.[key])) result[key] = settings[key];
    if (Number.isInteger(settings?.mode) && settings.mode >= 0 && settings.mode < modes.length) result.mode = settings.mode;
    if (typeof settings?.isCollapsed === "boolean") result.isCollapsed = settings.isCollapsed;
    for (const key of ["width", "height", "dockWidth", "dockHeight"]) result[key] = clamp(result[key], 160, 4000);
    return result;
}

export function bounds(settings, viewport, workspace) {
    const s = normalize(settings);
    const width = clamp(s.width, Math.min(320, viewport.width), viewport.width);
    const height = clamp(s.height, Math.min(240, viewport.height), viewport.height);
    return {
        ...s, width, height,
        x: clamp(s.x, viewport.left, viewport.left + viewport.width - width),
        y: clamp(s.y, viewport.top, viewport.top + viewport.height - height),
        dockWidth: clamp(s.dockWidth, Math.min(280, workspace.width / 2), workspace.width / 2),
        dockHeight: clamp(s.dockHeight, Math.min(200, viewport.height / 2), viewport.height / 2)
    };
}

export function initialize(element, reference, settings) {
    dispose(element);
    const controller = new AbortController();
    const state = { settings: normalize(settings), reference, controller, drag: null };
    try {
        const saved = localStorage.getItem(storageKey);
        if (saved) state.settings = normalize(JSON.parse(saved));
    } catch { /* Storage may be blocked or contain an older invalid value. */ }
    instances.set(element, state);
    const listen = (target, event, handler, capture = false) => target.addEventListener(event, handler, { signal: controller.signal, capture });
    const notify = () => {
        save(state.settings);
        reference.invokeMethodAsync("LayoutChanged", { ...state.settings }).catch(() => { });
    };
    const render = () => apply(element, state);
    const dismiss = restore => reference.invokeMethodAsync("DismissSwitcher", restore).catch(() => { });
    listen(document, "pointerdown", event => {
        const popup = element.querySelector(".rgf-recroby-switcher");
        if (popup?.matches(":popover-open") && !popup.contains(event.target) &&
            !element.querySelector("[data-action=switch]")?.contains(event.target)) dismiss(false);
    }, true);
    listen(document, "focusin", event => {
        const popup = element.querySelector(".rgf-recroby-switcher");
        if (popup?.matches(":popover-open") && !popup.contains(event.target) &&
            !element.querySelector("[data-action=switch]")?.contains(event.target)) dismiss(false);
    });
    listen(element, "keydown", event => {
        const popup = element.querySelector(".rgf-recroby-switcher");
        if (!popup?.matches(":popover-open")) return;
        if (event.key === "Escape") {
            event.preventDefault(); event.stopPropagation(); dismiss(true); return;
        }
        if (!event.target.matches("[data-action=select]")) return;
        const buttons = [...popup.querySelectorAll("[data-action=select]")];
        const index = buttons.indexOf(event.target);
        const next = event.key === "ArrowDown" ? (index + 1) % buttons.length
            : event.key === "ArrowUp" ? (index + buttons.length - 1) % buttons.length
            : event.key === "Home" ? 0 : event.key === "End" ? buttons.length - 1 : -1;
        if (next >= 0) { event.preventDefault(); buttons[next].focus(); }
    });
    listen(window, "resize", render);
    listen(window, "scroll", render, true);
    if (window.visualViewport) {
        listen(window.visualViewport, "resize", render);
        listen(window.visualViewport, "scroll", render);
    }
    const observer = new ResizeObserver(render);
    // Ancestor and preceding sibling sizes can move the root without resizing its parent.
    let hosts = new Set();
    const observeHosts = () => {
        const next = new Set([element]);
        for (let host = element.parentElement; host; host = host.parentElement) {
            next.add(host);
            for (let sibling = host.firstElementChild; sibling; sibling = sibling.nextElementSibling) {
                if (sibling === element || sibling.contains(element)) break;
                next.add(sibling);
            }
        }
        for (const host of hosts) if (!next.has(host)) observer.unobserve(host);
        for (const host of next) if (!hosts.has(host)) observer.observe(host);
        hosts = next;
    };
    observeHosts();
    state.observer = observer;
    const panel = element.querySelector(".rgf-recroby-panel");
    if (panel) observer.observe(panel);
    const content = element.querySelector(".rgf-recroby-content");
    const mutations = new MutationObserver(() => {
        observeHosts();
        if (findNavbar(element) !== state.navbar || state.settings.mode === 3) render();
    });
    if (content) mutations.observe(content, { childList: true, subtree: true });
    for (let host = element.parentElement; host; host = host.parentElement)
        mutations.observe(host, { childList: true });
    state.mutations = mutations;
    listen(element, "pointerdown", event => {
        if (window.matchMedia("(max-width: 767.98px)").matches || state.settings.isCollapsed) return;
        if (event.button !== 0 || event.target.closest("button,input,textarea,select,a")) return;
        const resize = event.target.closest("[data-resize-handle]");
        if (!resize && (!event.target.closest("[data-drag-handle]") || state.settings.mode !== 0)) return;
        const size = currentBounds(element, state);
        state.drag = {
            id: event.pointerId, x: event.clientX, y: event.clientY, resize: !!resize,
            start: { ...state.settings, ...(state.settings.mode === 0 ? { x: size.x, y: size.y, width: size.width, height: size.height } : {}) }
        };
        event.target.setPointerCapture(event.pointerId);
        event.preventDefault();
    });
    listen(element, "pointermove", event => {
        const d = state.drag;
        if (!d || d.id !== event.pointerId) return;
        if (window.matchMedia("(max-width: 767.98px)").matches) { state.drag = null; return; }
        const dx = event.clientX - d.x, dy = event.clientY - d.y;
        if (!d.resize) { state.settings.x = d.start.x + dx; state.settings.y = d.start.y + dy; }
        else if (d.start.mode === 0) { state.settings.width = d.start.width + dx; state.settings.height = d.start.height + dy; }
        else if (d.start.mode === 3) state.settings.dockHeight = d.start.dockHeight - dy;
        else state.settings.dockWidth = d.start.dockWidth + (d.start.mode === 1 ? dx : -dx);
        render();
    });
    const endDrag = event => {
        const d = state.drag;
        if (!d || d.id !== event.pointerId) return;
        if (!d.resize && event.type !== "pointercancel") {
            if (event.clientX <= 32) state.settings.mode = 1;
            else if (event.clientX >= window.innerWidth - 32) state.settings.mode = 2;
            else if (event.clientY >= window.innerHeight - 32) state.settings.mode = 3;
        }
        state.drag = null;
        state.settings = normalize(state.settings);
        render(); notify();
    };
    listen(element, "pointerup", endDrag);
    listen(element, "pointercancel", endDrag);
    listen(element, "keydown", event => {
        if (window.matchMedia("(max-width: 767.98px)").matches || state.settings.isCollapsed) return;
        if (!event.target.closest("[data-resize-handle]")) return;
        const dx = event.key === "ArrowRight" ? 16 : event.key === "ArrowLeft" ? -16 : 0;
        const dy = event.key === "ArrowDown" ? 16 : event.key === "ArrowUp" ? -16 : 0;
        if (!dx && !dy) return;
        if (state.settings.mode === 0) { state.settings.width += dx; state.settings.height += dy; }
        else if (state.settings.mode === 3) state.settings.dockHeight -= dy;
        else state.settings.dockWidth += state.settings.mode === 1 ? dx : -dx;
        event.preventDefault(); render(); notify();
    });
    render(); notify();
}

function currentBounds(element, state) {
    const r = element.getBoundingClientRect();
    const v = window.visualViewport;
    return bounds(state.settings, { left: v?.offsetLeft ?? 0, top: v?.offsetTop ?? 0, width: v?.width ?? window.innerWidth, height: v?.height ?? window.innerHeight },
        { width: r.width, height: r.height });
}

function findNavbar(element) {
    const content = element.querySelector(".rgf-recroby-content");
    return content?.querySelector(".rgf-navbar") ?? content?.querySelector(".navbar") ?? null;
}

function apply(element, state) {
    const navbar = findNavbar(element);
    if (navbar !== state.navbar) {
        if (state.navbar) state.observer.unobserve(state.navbar);
        state.navbar = navbar;
        if (navbar) state.observer.observe(navbar);
    }
    const navbarHeight = navbar?.getBoundingClientRect().height ?? 0;
    const panel = element.querySelector(".rgf-recroby-panel");
    const topBorder = panel ? parseFloat(getComputedStyle(panel).borderTopWidth) || 0 : 0;
    if (navbarHeight > 0) element.style.setProperty("--rgf-navbar-height", `${Math.max(0, navbarHeight - topBorder)}px`);
    else element.style.removeProperty("--rgf-navbar-height");
    const r = element.getBoundingClientRect();
    // Fill the initial visible area, but let host content grow in normal document flow.
    // Document coordinates keep this minimum independent of browser scroll position.
    const documentTop = r.top + window.scrollY;
    element.style.setProperty("--rgf-workspace-min-height", `${Math.max(0, window.innerHeight - Math.max(0, documentTop))}px`);
    const s = currentBounds(element, state);
    element.dataset.mode = modes[s.mode];
    element.dataset.collapsed = String(s.isCollapsed);
    const v = window.visualViewport;
    for (const [key, value] of [["top", v?.offsetTop ?? 0], ["left", v?.offsetLeft ?? 0], ["width", v?.width ?? window.innerWidth], ["height", v?.height ?? window.innerHeight]])
        element.style.setProperty(`--rgf-viewport-${key}`, `${value}px`);
    for (const [key, name] of [["x", "x"], ["y", "y"], ["width", "width"], ["height", "height"], ["dockWidth", "dock-width"], ["dockHeight", "dock-height"]])
        element.style.setProperty(`--rgf-${name}`, `${s[key]}px`);
    const bottom = (v?.offsetTop ?? 0) + (v?.height ?? window.innerHeight);
    if (s.mode === 3 && !s.isCollapsed && !window.matchMedia("(max-width: 767.98px)").matches) {
        const parent = element.parentElement;
        const parentStyle = parent ? getComputedStyle(parent) : null;
        const parentTop = parent ? parent.getBoundingClientRect().top + parent.clientTop + (parseFloat(parentStyle.paddingTop) || 0) : r.top;
        // Percentage max-height resolves only for a definite host height; auto hosts use the viewport.
        element.style.setProperty("--rgf-host-offset", `${Math.max(0, r.top - parentTop)}px`);
        element.style.setProperty("--rgf-bottom-workspace-height", `${Math.max(0, bottom - r.top)}px`);
        const area = element.getBoundingClientRect();
        const reserved = clamp(area.bottom - (bottom - s.dockHeight), 0, area.height);
        element.style.setProperty("--rgf-reserved-height", `${reserved}px`);
        const content = element.querySelector(".rgf-recroby-content");
        if (content) {
            const contentArea = content.getBoundingClientRect();
            for (const dashboard of content.querySelectorAll(".rgf-dashboard-page")) {
                const padding = parseFloat(getComputedStyle(dashboard.parentElement).paddingBottom) || 0;
                const offset = dashboard.getBoundingClientRect().top - contentArea.top + content.scrollTop;
                dashboard.style.setProperty("--rgf-bottom-dashboard-height", `${Math.max(0, content.clientHeight - offset - padding)}px`);
            }
        }
    } else {
        for (const name of ["--rgf-host-offset", "--rgf-bottom-workspace-height", "--rgf-reserved-height"])
            element.style.removeProperty(name);
    }
    // Reserve only the intersection of the viewport-fixed side dock and this root's work area.
    const area = element.getBoundingClientRect();
    const left = v?.offsetLeft ?? 0, top = v?.offsetTop ?? 0;
    const right = left + (v?.width ?? window.innerWidth);
    const reservedWidth = s.mode === 1 ? left + s.dockWidth - area.left
        : s.mode === 2 ? area.right - (right - s.dockWidth) : 0;
    element.style.setProperty("--rgf-reserved-width", `${clamp(reservedWidth, 0, area.width)}px`);
    positionSwitcher(element);
}

function positionSwitcher(element) {
    const popup = element.querySelector(".rgf-recroby-switcher");
    if (!popup?.matches(":popover-open")) return;
    const anchor = element.querySelector("[data-action=switch]").getBoundingClientRect();
    const v = window.visualViewport;
    const left = (v?.offsetLeft ?? 0) + 8, top = (v?.offsetTop ?? 0) + 8;
    const width = (v?.width ?? window.innerWidth) - 16;
    const bottom = top + (v?.height ?? window.innerHeight) - 16;
    const below = Math.max(0, bottom - anchor.bottom - 4);
    const above = Math.max(0, anchor.top - top - 4);
    const down = below >= Math.min(320, above);
    popup.style.width = `${Math.min(anchor.width, width)}px`;
    popup.style.maxHeight = `${Math.min(400, down ? below : above)}px`;
    popup.style.left = `${clamp(anchor.left, left, left + width - Math.min(anchor.width, width))}px`;
    popup.style.top = `${down ? anchor.bottom + 4 : anchor.top - 4 - popup.getBoundingClientRect().height}px`;
}

export function syncSwitcher(element, open, restoreFocus = false) {
    const popup = element.querySelector(".rgf-recroby-switcher");
    if (!popup) return;
    const wasOpen = popup.matches(":popover-open");
    if (open && !wasOpen) popup.showPopover();
    else if (!open && wasOpen) popup.hidePopover();
    positionSwitcher(element);
    if (open && !wasOpen) {
        (popup.querySelector("input") ?? popup.querySelector("[aria-pressed=true]") ?? popup.querySelector("button"))?.focus({ preventScroll: true });
        popup.querySelector("[aria-pressed=true]")?.scrollIntoView({ block: "nearest" });
    } else if (!open && restoreFocus) element.querySelector("[data-action=switch]")?.focus({ preventScroll: true });
    // Removing a focused conversation must leave keyboard navigation in the list.
    if (open && !popup.contains(document.activeElement)) popup.querySelector("input,button")?.focus({ preventScroll: true });
}

export function update(element, settings) {
    const state = instances.get(element);
    if (!state) return;
    state.settings = normalize(settings);
    save(state.settings);
    apply(element, state);
}

function save(settings) {
    try { localStorage.setItem(storageKey, JSON.stringify(normalize(settings))); }
    catch { /* The workspace remains usable without storage. */ }
}

export function dispose(element) {
    const state = instances.get(element);
    if (!state) return;
    state.controller.abort(); state.observer.disconnect(); state.mutations.disconnect(); instances.delete(element);
}
