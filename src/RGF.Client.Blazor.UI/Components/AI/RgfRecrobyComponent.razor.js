const instances = new WeakMap();

function position(element) {
    const viewport = window.visualViewport;
    const left = (viewport?.offsetLeft ?? 0) + 8;
    const top = (viewport?.offsetTop ?? 0) + 8;
    const width = Math.max(0, (viewport?.width ?? window.innerWidth) - 16);
    for (const button of element.querySelectorAll("[popovertarget]")) {
        const panel = document.getElementById(button.getAttribute("popovertarget"));
        if (!panel?.matches(":popover-open")) continue;
        const anchor = button.getBoundingClientRect();
        panel.style.width = `${Math.min(280, width)}px`;
        panel.style.maxHeight = `${Math.max(0, anchor.top - top - 4)}px`;
        panel.style.left = `${Math.max(left, Math.min(anchor.right - panel.offsetWidth, left + width - panel.offsetWidth))}px`;
        panel.style.top = `${Math.max(top, anchor.top - panel.offsetHeight - 4)}px`;
    }
}

export function sync(element, close) {
    if (!instances.has(element)) {
        const controller = new AbortController();
        const options = { signal: controller.signal };
        for (const button of element.querySelectorAll("[popovertarget]")) {
            const panel = document.getElementById(button.getAttribute("popovertarget"));
            panel.addEventListener("toggle", () => {
                const open = panel.matches(":popover-open");
                button.setAttribute("aria-expanded", String(open));
                position(element);
                if (open) panel.querySelector("select")?.focus({ preventScroll: true });
            }, options);
            panel.addEventListener("keydown", event => {
                if (event.key !== "Escape") return;
                event.preventDefault();
                event.stopPropagation();
                panel.hidePopover();
                button.focus({ preventScroll: true });
            }, options);
        }
        const observer = new ResizeObserver(() => position(element));
        observer.observe(element);
        for (const panel of element.querySelectorAll("[popover]")) observer.observe(panel);
        const mutations = new MutationObserver(() => {
            if (element.closest("[hidden]")) closePanels(element);
            else position(element);
        });
        for (let ancestor = element.parentElement; ancestor; ancestor = ancestor.parentElement)
            mutations.observe(ancestor, { attributes: true, attributeFilter: ["hidden", "style", "data-mode"] });
        window.addEventListener("resize", () => position(element), options);
        window.addEventListener("scroll", () => position(element), { ...options, capture: true });
        window.visualViewport?.addEventListener("resize", () => position(element), options);
        window.visualViewport?.addEventListener("scroll", () => position(element), options);
        instances.set(element, { controller, observer, mutations });
    }
    if (close || element.closest("[hidden]")) closePanels(element);
    position(element);
}

function closePanels(element) {
    for (const panel of element.querySelectorAll("[popover]"))
        if (panel.matches(":popover-open")) panel.hidePopover();
}

export function dispose(element) {
    const state = instances.get(element);
    if (!state) return;
    state.controller.abort();
    state.observer.disconnect();
    state.mutations.disconnect();
    instances.delete(element);
}
