// Runs production CSS/modules in Chromium; Firefox runs the Toast geometry subset via WebDriver BiDi.
// dotnet/bUnit covers component lifetimes and transport; this covers browser geometry/events.
import assert from "node:assert/strict";
import { readFile, writeFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

const base = new URL("../src/RGF.Client.Blazor.UI/", import.meta.url);
const appRoot = "/recroby-test";
const moduleRoot = "/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Components/AI/";
const assets = {
    "/styles.css": ["text/css", await readFile(new URL("wwwroot/css/styles.css", base), "utf8")],
    "/icons.css": ["text/css", await readFile(new URL("wwwroot/lib/bootstrap-icons/font/bootstrap-icons.css", base), "utf8")],
    "/fonts/bootstrap-icons.woff2": ["font/woff2", await readFile(new URL("wwwroot/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2", base))],
    "/fonts/bootstrap-icons.woff": ["font/woff", await readFile(new URL("wwwroot/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff", base))],
    "/usage.js": ["text/javascript", await readFile(new URL("Components/AI/RgfAiUsageSummary.razor.js", base), "utf8")],
    "/usage.css": ["text/css", await readFile(new URL("Components/AI/RgfAiUsageSummary.razor.css", base), "utf8")],
    "/selection.js": ["text/javascript", await readFile(new URL("Components/AI/RgfRecrobyComponent.razor.js", base), "utf8")],
    "/selection.css": ["text/css", (await readFile(new URL("Components/AI/RgfRecrobyComponent.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/markdown.css": ["text/css", (await readFile(new URL("Components/Base/RgfMarkdownViewer.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/chat.css": ["text/css", (await readFile(new URL("Components/AI/RgfAiChatComponent.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/module.js": ["text/javascript", await readFile(new URL("Components/AI/RgfRecrobyWorkspace.razor.js", base), "utf8")],
    "/style.css": ["text/css", (await readFile(new URL("Components/AI/RgfRecrobyWorkspace.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/bootstrap.css": ["text/css", await readFile(new URL("wwwroot/lib/bootstrap/dist/css/bootstrap.min.css", base), "utf8")]
};
assets['/bootstrap.js'] = ['text/javascript', await readFile(new URL('wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js', base), 'utf8')];
assets['/jquery.js'] = ['text/javascript', await readFile(new URL('../RGF.Client.Blazor/wwwroot/lib/jquery/jquery.min.js', base), 'utf8')];
assets['/ui.js'] = ['text/javascript', await readFile(new URL('wwwroot/scripts/recrovit-rgf-blazor-ui.js', base), 'utf8')];
assets['/components.css'] = ['text/css', (await Promise.all(['Dashboard/DashboardPageComponent', 'GridComponent', 'NavbarComponent', 'DialogComponent'].map(name => readFile(new URL(`Components/${name}.razor.css`, base), 'utf8')))).join('\n').replaceAll('::deep', '')];
assets[moduleRoot + 'RgfRecrobyComponent.razor.js'] = assets['/selection.js'];
assets[moduleRoot + 'RgfRecrobyWorkspace.razor.js'] = assets['/module.js'];
const html = `<!doctype html><html><head><meta charset="utf-8"><link rel="stylesheet" href="/bootstrap.css"><link rel="stylesheet" href="/style.css"><link rel="stylesheet" href="/styles.css">
<link rel="stylesheet" href="/icons.css"><link rel="stylesheet" href="/usage.css"><link rel="stylesheet" href="/components.css"><link rel="stylesheet" href="/selection.css"><link rel="stylesheet" href="/chat.css"><link rel="stylesheet" href="/markdown.css"><script src="/jquery.js"></script><script src="/bootstrap.js"></script><script src="/ui.js"></script>
<style>body {margin:0} #outside {height:60px} #host {margin-left:120px; width:calc(100% - 160px)}
.chat-box {overflow-y:auto} .message {padding:10px} .navbar {height:56px} main {flex:1;min-height:0}
.grid {width:100%} .modal {z-index:1055}</style></head><body>
<div id="outside">Unrelated application header</div><div id="host"><div class="rgf-recroby-workspace" style="--rgf-toast-anchor: --rgf-toast-fixture;">
<div class="rgf-recroby-content"><nav class="navbar bg-light">Host navbar</nav><main><div class="grid">Grid/Dashboard content</div></main></div>
<button class="rgf-recroby-launcher btn btn-primary">Recroby</button><section class="rgf-recroby-panel card" hidden>
<header class="card-header" data-drag-handle><strong>Recroby</strong><button id="collapse">Collapse</button></header>
<div class="card-header rgf-recroby-conversation-bar">
<button id="conversation-selector" class="btn btn-sm btn-outline-secondary rgf-recroby-selector" data-action="switch" aria-haspopup="dialog" aria-expanded="false" aria-controls="conversation-list" title="New chat"><span class="rgf-recroby-title">New chat</span><i aria-hidden="true">&#x2304;</i></button>
<button class="btn btn-sm btn-outline-secondary" data-action="rename" aria-label="Rename conversation"><i class="bi bi-pencil-square" aria-hidden="true"></i></button>
<button class="btn btn-sm btn-outline-secondary" data-action="new" aria-label="New conversation">+</button>
<div id="conversation-list" class="rgf-recroby-switcher" popover="manual" hidden role="dialog" aria-label="Conversations">
<div class="rgf-recroby-switcher-list"><div role="group" aria-label="Conversations" data-conversation-type="general">
<div class="rgf-recroby-choice active"><button class="rgf-recroby-choice-select" data-action="select" aria-pressed="true"><span class="rgf-recroby-choice-title">Conversation 1</span><i aria-hidden="true">&#x2713;</i></button><button class="btn-close" aria-label="Close Conversation 1"></button></div>
</div></div></div></div>
<div class="rgf-recroby-conversation"><div class="container mt-4">
<div class="chat-box d-flex flex-column">${"<div class='message'>Message</div>".repeat(40)}</div>
<div class="input-group mt-3"><textarea class="form-control"></textarea></div><div class="rgf-ai-actions"><button type="button" class="btn btn-sm btn-success m-1 align-self-start rgf-ai-action-icon" id="send" title="Send" aria-label="Send"><i class="bi bi-send" aria-hidden="true"></i></button>
<div class="rgf-recroby-selectors">
<button id="model-choice" class="btn btn-sm btn-outline-secondary" popovertarget="model-panel" aria-expanded="false"><i class="bi bi-cpu me-1" aria-hidden="true"></i>Provider / Model ▾</button>
<button id="effort-choice" class="btn btn-sm btn-outline-secondary" popovertarget="effort-panel" aria-expanded="false"><i class="bi bi-lightbulb me-1" aria-hidden="true"></i>Default ▾</button>
<div id="model-panel" class="rgf-recroby-selection-panel" popover="auto" role="dialog"><label>Provider</label><select class="form-select"><option>Provider</option></select><label>Model</label><select class="form-select" data-close-popover><option>Model</option></select></div>
<button id="format-choice" class="btn btn-sm btn-outline-secondary" popovertarget="format-panel" aria-expanded="false"><i class="bi bi-markdown me-1" aria-hidden="true"></i>Minimal ▾</button>
<div id="format-panel" class="rgf-recroby-selection-panel" popover="auto" role="dialog"><label>Response format</label><select class="form-select" data-close-popover><option>Plain text</option><option selected>Minimal</option><option>Normal</option><option>Maximum</option></select></div>
<div id="effort-panel" class="rgf-recroby-selection-panel" popover="auto" role="dialog"><label>Reasoning effort</label><select class="form-select" data-close-popover><option>Default</option><option>High</option></select></div>
</div></div></div></div>
<div class="rgf-recroby-resize" data-resize-handle tabindex="0"></div></section></div>
<div class="toast-container rgf-toast-container position-fixed bottom-0 p-3" style="--rgf-toast-anchor: --rgf-toast-fixture;" hidden><div class="toast show" role="alert"><div class="toast-header"><strong class="me-auto">Host toast</strong><button id="toast-close" class="btn-close"></button></div><div class="toast-body">Still working</div></div></div></div>
<div id="form-reference" class="card" style="position:fixed;left:-1000px;width:360px"><div class="dialog-header card-header"><ul class="nav nav-tabs card-header-tabs"><li class="nav-item"><button class="nav-link active">Form tab</button></li></ul></div></div>
<script type="module">import * as workspace from '${appRoot}${moduleRoot}RgfRecrobyWorkspace.razor.js';
import * as selection from '${appRoot}${moduleRoot}RgfRecrobyComponent.razor.js';
import * as usage from '${appRoot}/usage.js';
window.usage = usage;
// Mirror the component's rendered native controls; use production CSS and JS.
const chat = document.querySelector('.chat-box');
window.usageListeners = 0;
const addWindowListener = window.addEventListener.bind(window);
window.addEventListener = (name, handler, options) => {
    if (handler.name === 'position' && options?.signal) {
        window.usageListeners++;
        options.signal.addEventListener('abort', () => window.usageListeners--, {once:true});
    }
    addWindowListener(name, handler, options);
};
for (let index = 0; index < 40; index++) {
    const row = document.createElement('div');
    row.className = 'rgf-ai-usage';
    row.id = 'usage-row-' + index;
    for (const [group, icon] of [['models','cpu'],['cost','coin'],['credit','piggy-bank'],['tokens','bar-chart']]) {
        const id = 'usage-' + index + '-' + group;
        row.insertAdjacentHTML('beforeend', '<button type="button" class="rgf-ai-usage-item" id="' + id + '-button" popovertarget="' + id + '" aria-controls="' + id + '" aria-haspopup="dialog" aria-expanded="false" aria-label="' + group + '"><i class="bi bi-' + icon + '" aria-hidden="true"></i>' + (group === 'models' ? 'actual-model-' + 'long'.repeat(35) : group) + '</button><div class="rgf-ai-usage-panel" id="' + id + '" popover="auto" role="dialog" tabindex="-1" aria-label="' + group + '"><strong>' + group + '</strong><dl><dt>Model</dt><dd>' + 'long'.repeat(100) + '</dd></dl><time datetime="2026-10-14T00:00:00Z" data-local-time></time><time datetime="2026-10-13" data-local-date></time></div>');
    }
    chat.append(row);
    usage.sync(row);
}
const selectors = document.querySelector('.rgf-recroby-selectors');
// First render has format only; catalog controls arrive later.
const catalogNodes = [...selectors.children].filter(node => /^(model|effort)-/.test(node.id));
for (const node of catalogNodes) node.remove();
selection.sync(selectors, false);
for (const node of catalogNodes) selectors.append(node);
window.selectionRegistrations = 0;
for (const panel of selectors.querySelectorAll('[popover]')) {
    const add = panel.addEventListener.bind(panel);
    panel.addEventListener = (...args) => { window.selectionRegistrations++; return add(...args); };
}
selection.sync(selectors, false);
selection.sync(selectors, false);
window.selectionSync = close => selection.sync(selectors, close);
const element = document.querySelector('.rgf-recroby-workspace');
window.settings = {mode:0,isCollapsed:true,x:250,y:100,width:440,height:560,dockWidth:360,dockHeight:240};
window.switcherOpen = false;
window.setSwitcher = open => {window.switcherOpen = open; element.querySelector('.rgf-recroby-switcher').hidden = !open; element.querySelector('[data-action=switch]').setAttribute('aria-expanded', String(open)); workspace.syncSwitcher(element, open, !open);};
window.sync = () => {element.querySelector('section').hidden = settings.isCollapsed; element.querySelector('.rgf-recroby-launcher').hidden = !settings.isCollapsed; if (settings.isCollapsed && switcherOpen) setSwitcher(false);};
element.querySelector('[data-action=switch]').onclick = () => setSwitcher(!switcherOpen);
// Mirror Blazor replacing the selector after rendering the editor.
window.beginRename = () => {
    window.switcherOpen = false;
    element.querySelector('.rgf-recroby-switcher').hidden = true;
    workspace.syncSwitcher(element, false, false);
    const selector = element.querySelector('[data-action=switch]');
    const input = document.createElement('input');
    input.className = 'form-control form-control-sm rgf-recroby-title-editor';
    input.maxLength = 100;
    input.value = selector.querySelector('.rgf-recroby-title').textContent;
    const cancel = () => {if (input.isConnected) input.replaceWith(selector);};
    input.onkeydown = event => {if (event.key === 'Escape') cancel();};
    input.onblur = cancel;
    selector.replaceWith(input);
    workspace.focusTitleEditor(element);
};
element.querySelector('[data-action=rename]').onclick = beginRename;
window.setLayout = patch => {Object.assign(settings, patch); workspace.update(element, settings); sync();};
window.reference = {invokeMethodAsync: async (method, value) => {
    if (method === 'DismissSwitcher') {window.switcherOpen=false; element.querySelector('.rgf-recroby-switcher').hidden=true; element.querySelector('[data-action=switch]').setAttribute('aria-expanded', 'false'); workspace.syncSwitcher(element, false, value);}
    else {window.settings = value; sync();}
}};
window.reinitialize = () => workspace.initialize(element, reference, settings);
element.querySelector('.rgf-recroby-launcher').onclick = () => setLayout({isCollapsed:false});
document.querySelector('#collapse').onclick = () => setLayout({isCollapsed:true});
reinitialize(); window.ready = true;
</script></body></html>`;

const hostedHtml = html.replaceAll('href="/', `href="${appRoot}/`).replaceAll('src="/', `src="${appRoot}/`);
const server = createServer((req, res) => {
    if (!req.url.startsWith(appRoot + '/')) { res.writeHead(404); res.end(); return; }
    const path = req.url.slice(appRoot.length).split('?')[0];
    const asset = assets[path];
    if (!asset && /\.(js|css)$/.test(path)) { res.writeHead(404); res.end(); return; }
    const [type, body] = asset ?? ["text/html", hostedHtml];
    res.setHeader("Content-Type", type); res.end(body);
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const profile = await mkdtemp(join(tmpdir(), "rgf-recroby-edge-"));
const executable = process.env.RGF_TEST_BROWSER ?? "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const firefox = /firefox/i.test(executable);
let firefoxPort;
if (firefox) {
    const reservation = createServer();
    await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
    firefoxPort = reservation.address().port;
    await new Promise(resolve => reservation.close(resolve));
}
const child = spawn(executable, firefox ? ['--headless', '--no-remote', '--profile', profile, '--remote-debugging-port', String(firefoxPort), 'about:blank'] : ["--headless", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
    "--remote-debugging-port=0", `--user-data-dir=${profile}`, "about:blank"], { windowsHide: true, stdio: "ignore" });
let socket;
try {
    let endpoint;
    if (firefox) {
        endpoint = `ws://127.0.0.1:${firefoxPort}/session`;
        for (let i = 0; i < 100; i++) {
            try { await fetch(`http://127.0.0.1:${firefoxPort}/`); break; } catch { await delay(100); }
        }
    } else {
        let port;
        for (let i = 0; i < 100 && !port; i++) {
            try { port = (await readFile(join(profile, "DevToolsActivePort"), "utf8")).split("\n")[0]; } catch { await delay(100); }
        }
        assert.ok(port, "Chromium debugging endpoint started");
        const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
        endpoint = targets.find(t => t.type === "page").webSocketDebuggerUrl;
    }
    socket = new WebSocket(endpoint);
    await new Promise((resolve, reject) => {socket.addEventListener("open", resolve, {once:true}); socket.addEventListener("error", reject, {once:true});});
    let id = 0; const pending = new Map();
    socket.addEventListener("message", event => {
        const message = JSON.parse(event.data);
        if (pending.has(message.id)) { pending.get(message.id)(message); pending.delete(message.id); }
    });
    const protocol = (method, params = {}) => new Promise((resolve, reject) => {
        const next = ++id;
        const timeout = setTimeout(() => { pending.delete(next); reject(new Error(`Protocol timeout: ${method}`)); }, 30000);
        pending.set(next, message => { clearTimeout(timeout); message.error ? reject(new Error(JSON.stringify(message))) : resolve(message.result); });
        socket.send(JSON.stringify({ id: next, method, params }));
    });
    let firefoxContext;
    if (firefox) {
        await protocol('session.new', {capabilities:{}});
        const tree = await protocol('browsingContext.getTree', {});
        firefoxContext = tree.contexts[0].context;
    }
    const fromBidi = remote => remote.type === 'object' ? Object.fromEntries((remote.value ?? []).map(([key, value]) => [key, fromBidi(value)]))
        : remote.type === 'array' ? (remote.value ?? []).map(fromBidi) : remote.value;
    const cdp = async (method, params = {}) => {
        if (!firefox) return protocol(method, params);
        if (method === 'Runtime.evaluate') {
            const result = await protocol('script.evaluate', {expression:params.expression, target:{context:firefoxContext}, awaitPromise:true, resultOwnership:'none', serializationOptions:{maxObjectDepth:10}});
            if (result.type === 'exception') throw new Error(JSON.stringify(result.exceptionDetails));
            return {result:{value:fromBidi(result.result)}};
        }
        if (method === 'Emulation.setDeviceMetricsOverride') return protocol('browsingContext.setViewport', {context:firefoxContext, viewport:{width:params.width,height:params.height},devicePixelRatio:1});
        if (method === 'Page.navigate') return protocol('browsingContext.navigate', {context:firefoxContext,url:params.url,wait:'complete'});
        throw new Error(`Unsupported Firefox command: ${method}`);
    };
    const evaluate = async expression => {
        const result = await cdp("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
        if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
        return result.result.value;
    };
    const viewport = async (width, height) => {
        await cdp("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false });
        await delay(150);
    };
    const layout = async patch => { await evaluate(`setLayout(${JSON.stringify(patch)})`); await delay(100); };
    const rect = selector => evaluate(`(() => {const r=document.querySelector(${JSON.stringify(selector)}).getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height,right:r.right,bottom:r.bottom};})()`);
    const click = async selector => {
        const r = await rect(selector);
        const point = { x: r.x + r.width / 2, y: r.y + r.height / 2, button: 'left', clickCount: 1 };
        await cdp('Input.dispatchMouseEvent', { type: 'mousePressed', ...point });
        await cdp('Input.dispatchMouseEvent', { type: 'mouseReleased', ...point });
        await delay(100);
    };
    await viewport(1200, 800);
    await cdp("Page.navigate", { url: `http://127.0.0.1:${server.address().port}${appRoot}/nested/route` });
    for (let i = 0; i < 100 && !await evaluate("window.ready === true"); i++) await delay(50);
    await evaluate("document.fonts.load('12px bootstrap-icons')");
    assert.equal(await evaluate("document.fonts.check('12px bootstrap-icons')"), true, 'existing Bootstrap icon font is loaded');
    for (const [button, icon] of [['model-choice','bi-cpu'], ['effort-choice','bi-lightbulb']]) {
        assert.equal(await evaluate(`document.querySelector('#${button}').firstElementChild.className`), `bi ${icon} me-1`);
        assert.equal(await evaluate(`document.querySelector('#${button}').firstElementChild.getAttribute('aria-hidden')`), 'true');
        assert.equal(await evaluate(`document.querySelector('#${button}').textContent.trim().endsWith('▾')`), true);
    }
    assert.equal(await evaluate("document.querySelector('#usage-39-credit-button i').className"), 'bi bi-piggy-bank');
    assert.equal(await evaluate("document.querySelector('section').hidden"), true);
    const full = await rect(".rgf-recroby-content");
    assert.equal(full.height, 740, 'short host content fills the visible work area');
    assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-content')).overflow"), 'visible');
    assert.equal(await evaluate('document.documentElement.scrollHeight > window.innerHeight'), false, 'short content needs no browser scrollbar');
    // Real Root siblings, production global CSS, and a host narrower than the viewport.
    const anchorSupported = await evaluate("CSS.supports('anchor-name: --rgf-test') && CSS.supports('position-anchor: --rgf-test') && CSS.supports('right: anchor(right)')");
    // Modern installed engines no longer expose a working anchor disable switch.
    // Simulate a false feature query in CSSOM; keep this distinct from a legacy-browser run.
    const simulateNoAnchors = !!process.env.RGF_TEST_SIMULATE_NO_ANCHORS;
    if (simulateNoAnchors) {
        await evaluate(`(() => {
            const sheet = [...document.styleSheets].find(s => s.href?.endsWith('/styles.css'));
            const media = [...sheet.cssRules].find(r => r.conditionText === '(min-width: 768px)');
            const index = [...media.cssRules].findIndex(r => r instanceof CSSSupportsRule);
            const rule = media.cssRules[index].cssText.replace(/@supports[^\\{]+/, '@supports (rgf-unsupported-feature: unavailable) ');
            media.deleteRule(index); media.insertRule(rule, index);
        })()`);
    }
    console.log(`Browser: ${await evaluate('navigator.userAgent')}; CSS anchors: ${anchorSupported}; simulated unsupported feature query: ${simulateNoAnchors}`);
    const toastRight = async (anchored, selector = '.rgf-toast-container', content = '.rgf-recroby-content') => {
        const box = await rect(selector);
        const expected = anchored && anchorSupported && !simulateNoAnchors ? (await rect(content)).right : await evaluate('document.documentElement.clientWidth');
        assert.ok(Math.abs(box.right - expected) < 1, `Toast right ${box.right} matches ${expected}`);
        assert.equal(box.bottom, await evaluate('innerHeight'), 'Toast stays at viewport bottom');
        assert.ok(box.x >= 0, 'Toast remains inside the viewport');
        assert.equal(await evaluate('document.documentElement.scrollWidth > innerWidth'), false, 'Toast creates no horizontal overflow');
    };
    await evaluate("document.querySelector('.rgf-toast-container').hidden=false");
    assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-toast-container')).zIndex"), '1090', 'production Bootstrap Toast stacking remains unchanged');
    for (const mode of [0, 1, 2, 3]) {
        await layout({mode, isCollapsed:false});
        await toastRight(mode === 2);
        if (mode === 2 && anchorSupported && !simulateNoAnchors) assert.ok((await rect('.rgf-toast-container')).right <= (await rect('section')).x, 'Toast does not cover right-docked Recroby');
        await layout({isCollapsed:true});
        await toastRight(false);
    }
    await layout({mode:2,isCollapsed:false,dockWidth:360});
    await toastRight(true);
    await layout({dockWidth:480});
    await toastRight(true);
    if (firefox) {
        const handle = await rect('.rgf-recroby-resize');
        const x = Math.round(handle.x + handle.width / 2), y = Math.round(handle.y + handle.height / 2);
        await protocol('input.performActions', {context:firefoxContext,actions:[{type:'pointer',id:'mouse',parameters:{pointerType:'mouse'},actions:[
            {type:'pointerMove',x,y},{type:'pointerDown',button:0},{type:'pointerMove',x:x-40,y,duration:100},{type:'pointerUp',button:0}
        ]}]});
        await delay(150);
        assert.equal(await evaluate('settings.dockWidth'), 520, 'Firefox pointer resizing uses the production handler');
        await toastRight(true);
    }
    await viewport(1000,700);
    await toastRight(true);
    await viewport(767,700);
    await toastRight(false);
    await viewport(390,700);
    await toastRight(false);
    await viewport(1200,800);
    await toastRight(true);
    await evaluate("document.querySelector('.rgf-toast-container').style.setProperty('--rgf-toast-anchor','--missing-anchor')");
    await toastRight(false);
    await evaluate("document.querySelector('.rgf-toast-container').style.setProperty('--rgf-toast-anchor','--rgf-toast-fixture')");
    // A second Root has an independent anchor even when both workspaces are open.
    await evaluate(`(() => {
        const root = document.createElement('div'); root.id='second-root';
        root.innerHTML='<div class="rgf-recroby-workspace" data-mode="DockRight" data-collapsed="false" style="width:700px;--rgf-reserved-width:200px;--rgf-toast-anchor:--rgf-toast-second"><div class="rgf-recroby-content" style="height:50px"></div></div><div class="toast-container rgf-toast-container position-fixed bottom-0 p-3" style="--rgf-toast-anchor:--rgf-toast-second"><div class="toast show">Second Root</div></div>';
        document.body.append(root);
    })()`);
    await toastRight(true);
    await toastRight(true, '#second-root .rgf-toast-container', '#second-root .rgf-recroby-content');
    await evaluate("document.querySelector('#second-root').remove()");
    await evaluate("document.querySelector('.rgf-recroby-workspace').hidden=true");
    await toastRight(false);
    await evaluate("document.querySelector('.rgf-recroby-workspace').hidden=false; document.querySelector('.rgf-toast-container').hidden=true");
    await layout({mode:0,isCollapsed:true,dockWidth:360});
    console.log('PASS: Toast anchor geometry, resize, collapse, all modes, mobile, narrow host, multiple Roots, missing anchor, disabled Workspace, overflow and stacking');
    if (!firefox && !process.env.RGF_TEST_TOAST_ONLY && !simulateNoAnchors) {
    for (const mode of [0, 1, 2, 3]) {
        await layout({ mode, isCollapsed: false });
        const preferences = await evaluate('JSON.stringify(settings)');
        for (const [width, height] of [[767, 800], [390, 700], [390, 360]]) {
            await viewport(width, height);
            await click('#collapse');
            assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-content')).overflow"), 'visible', 'mobile never inherits the bottom-dock work-area scroller');
            const tab = await rect('.rgf-recroby-launcher');
            assert.equal(tab.right, width, `mobile tab is on the right from mode ${mode}`);
            assert.ok(tab.width < 30);
            assert.ok(Math.abs(tab.y + tab.height / 2 - height / 2) < 1);
            assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-launcher')).writingMode"), 'vertical-rl');
            assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-launcher')).position"), 'fixed');
            await evaluate("document.body.style.height='1800px'; window.scrollTo(0,250)");
            await delay(100);
            const scrolled = await rect('.rgf-recroby-launcher');
            assert.equal(scrolled.right, await evaluate('visualViewport.width'), 'mobile tab follows the visible right edge including the browser scrollbar');
            assert.equal(scrolled.y, tab.y, 'mobile tab stays vertically centered while scrolling');
            await evaluate("window.scrollTo(0,0); document.body.style.height=''");
            await viewport(768, 800);
            const desktopTab = await rect('.rgf-recroby-launcher');
            if (mode === 1) assert.equal(desktopTab.x, 0);
            else if (mode === 3) assert.equal(desktopTab.bottom, 800);
            else assert.equal(desktopTab.right, 768);
            await viewport(width, height);
            await click('.rgf-recroby-launcher');
            assert.equal((await rect('section')).height, height);
            assert.equal(await evaluate('JSON.stringify(settings)'), preferences, 'mobile collapse/reopen preserves desktop preferences');
            await viewport(1200, 800);
            assert.equal(await evaluate('JSON.stringify(settings)'), preferences);
        }
    }
    for (const mode of [1, 2, 3]) {
        await layout({ mode, isCollapsed: false });
        const content = await rect(".rgf-recroby-content"), panel = await rect("section");
        assert.equal(await evaluate("getComputedStyle(document.querySelector('section')).borderRadius"), '0px', 'docked panel has square corners');
        assert.equal(await evaluate("getComputedStyle(document.querySelector('section')).borderTopWidth"), mode === 3 ? '1px' : '0px', 'side docks have no top border; bottom dock retains its border');
        assert.equal(await evaluate("getComputedStyle(document.querySelector('header')).borderTopLeftRadius"), '0px', 'docked header has square corners');
        if (mode === 1) { assert.equal(panel.x, 0); assert.equal(content.x, panel.right); assert.equal(content.right, full.right); }
        if (mode === 2) { assert.equal(panel.right, 1200); assert.equal(content.right, panel.x); assert.equal(content.x, full.x); }
        if (mode === 3) { assert.equal(content.bottom, panel.y); assert.equal(panel.bottom, 800); assert.equal(panel.width, 1200); }
        else { assert.equal(panel.y, 0); assert.equal(panel.height, 800); }
        assert.equal((await rect("#outside")).height, 60);
        assert.equal((await rect(".navbar")).width, content.width);
        assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-conversation-bar')).display"), 'flex', 'all dock modes use the same single-row switcher');
        if (mode !== 3) assert.equal((await rect('header')).bottom - panel.y, (await rect('.navbar')).height, 'side-docked header including panel border matches host navbar height');
        await layout({ isCollapsed: true });
        assert.equal((await rect(".rgf-recroby-content")).width, full.width);
        const tab = await rect(".rgf-recroby-launcher");
        assert.ok(mode === 3 ? tab.height < 30 : tab.width < 30, 'collapsed tab is thin');
        if (mode === 1) assert.equal(tab.x, 0);
        if (mode === 2) assert.equal(tab.right, 1200);
        if (mode === 3) assert.equal(tab.bottom, 800);
        await layout({ isCollapsed: false });
        assert.equal(await evaluate("settings.mode"), mode);
    }
    await layout({ mode: 0, isCollapsed: false });
    const selectorShape = await rect('[data-action=switch]');
    assert.ok((await rect('[data-action=new]')).right <= (await rect('.rgf-recroby-conversation-bar')).right, 'selector and actions fit the bar');
    assert.equal(await evaluate("document.querySelector('.rgf-recroby-title').scrollWidth > document.querySelector('.rgf-recroby-title').clientWidth"), false, 'short conversation title remains fully visible');
    assert.ok((await rect('[data-action=new]')).x >= selectorShape.right, 'new conversation button follows the flexible selector');
    for (const [width, mode, panelWidth] of [[1200, 1, 400], [1200, 0, 320], [390, 0, 320], [320, 0, 320]]) {
        await viewport(width, 700);
        await layout({mode, isCollapsed:false, width:panelWidth, dockWidth:panelWidth});
        await evaluate("document.querySelector('.rgf-recroby-title').textContent='W'.repeat(100)");
        const bar = await rect('.rgf-recroby-conversation-bar');
        const rename = await rect('[data-action=rename]'), add = await rect('[data-action=new]');
        assert.ok(rename.width > 20 && add.width > 20 && add.right <= bar.right && rename.y === add.y, '100-character title keeps both actions visible in dock, small floating and mobile layouts');
        assert.ok(await evaluate("document.querySelector('.rgf-recroby-conversation-bar').scrollWidth <= document.querySelector('.rgf-recroby-conversation-bar').clientWidth"), 'long title never overflows the bar');
        await click('[data-action=switch]');
        await evaluate('beginRename()');
        assert.equal(await evaluate("document.querySelector('.rgf-recroby-switcher').matches(':popover-open')"), false, 'editing closes the switcher');
        assert.equal(await evaluate("document.activeElement.classList.contains('rgf-recroby-title-editor')"), true, 'post-render editor receives focus without switcher restoration');
        assert.equal(await evaluate("document.activeElement.selectionEnd - document.activeElement.selectionStart"), 100, 'current title is selected');
        assert.ok((await rect('.rgf-recroby-title-editor')).right <= (await rect('[data-action=rename]')).x, 'editor fits beside fixed actions');
        await layout({height:300});
        await cdp('Input.dispatchKeyEvent', {type:'keyDown', key:'Escape'});
        assert.equal(await evaluate("document.querySelector('.rgf-recroby-title-editor') === null"), true, 'Escape cancels editing');
        await click('[data-action=switch]');
        const popup = await rect('.rgf-recroby-switcher');
        assert.ok(popup.x >= 7 && popup.right <= width - 7 && popup.bottom <= 693, 'switcher positioning survives editing and resizing');
        await cdp('Input.dispatchKeyEvent', {type:'keyDown', key:'Escape'});
        assert.equal(await evaluate("document.activeElement === document.querySelector('[data-action=switch]')"), true, 'switcher Escape still restores focus after editing');
        await click('[data-action=rename]');
        await evaluate("document.querySelector('textarea').focus()");
        assert.equal(await evaluate("document.querySelector('.rgf-recroby-title-editor') === null"), true, 'blur cancels editing');
        assert.equal(await evaluate("document.activeElement.tagName"), 'TEXTAREA', 'blur preserves destination focus');
    }
    await viewport(1200,800);
    await evaluate("document.querySelector('.rgf-recroby-title').textContent='New chat'");
    await layout({ mode: 1 });
    await evaluate("document.querySelector('.navbar').style.height='72px'");
    await delay(150);
    assert.equal((await rect('header')).bottom - (await rect('section')).y, 72, 'side header including border tracks navbar resizing');
    await evaluate("document.querySelector('.navbar').outerHTML='<nav class=\"navbar bg-light\" style=\"height:64px\">Replacement navbar</nav>'");
    await delay(150);
    assert.equal((await rect('header')).bottom - (await rect('section')).y, 64, 'side header including border tracks navbar replacement during navigation');
    await evaluate("document.querySelector('section').style.borderTopWidth='3px'");
    await delay(150);
    assert.equal((await rect('header')).bottom - (await rect('section')).y, 64, 'header alignment includes a thicker themed panel border');
    await evaluate("document.querySelector('section').style.borderTopWidth=''");
    const barHeight = (await rect('.rgf-recroby-conversation-bar')).height;
    await evaluate(`(() => {
        const group=document.querySelector('[data-conversation-type=general]');
        const row=group.querySelector('.rgf-recroby-choice');
        for(let i=2;i<=30;i++) {const copy=row.cloneNode(true);copy.classList.remove('active');copy.querySelector('i')?.remove();copy.querySelector('[data-action=select]').setAttribute('aria-pressed','false');copy.querySelector('.rgf-recroby-choice-title').textContent='Conversation '+i+' '+ 'VeryLongConversationTitle'.repeat(5);group.append(copy);}
        document.querySelector('.rgf-recroby-title').textContent='VeryLongConversationTitle'.repeat(20);
        document.querySelector('.rgf-recroby-switcher').insertAdjacentHTML('afterbegin','<input type="search" class="form-control form-control-sm" aria-label="Search conversations">');
    })()`);
    for (const [width,height] of [[1200,800],[800,400],[390,700],[390,260]]) {
        await viewport(width,height);
        for (const mode of [0,1,2,3]) {
            await layout({mode,isCollapsed:false,height:240,dockHeight:200});
            assert.equal((await rect('.rgf-recroby-conversation-bar')).height, barHeight, 'many conversations and long titles keep a single-height bar');
            assert.ok(await evaluate("document.querySelector('.rgf-recroby-title').scrollWidth > document.querySelector('.rgf-recroby-title').clientWidth"), 'active long title is truncated');
            await click('[data-action=switch]');
            const popup=await rect('.rgf-recroby-switcher'), anchor=await rect('[data-action=switch]');
            assert.ok(popup.y >= 7 && popup.bottom <= height-7 && popup.x >= 7 && popup.right <= width-7, 'popover fits the viewport in every layout');
            assert.ok(Math.abs(popup.width-anchor.width)<1, 'popover follows the selector width');
            if (process.env.RGF_TEST_SCREENSHOT && ((width===1200 && mode===0) || (width===800 && mode>0) || (width===390 && mode===0))) {
                const screenshot=await cdp('Page.captureScreenshot',{format:'png'});
                await writeFile(process.env.RGF_TEST_SCREENSHOT.replace(/\.png$/, '-'+mode+'-'+width+'x'+height+'.png'),Buffer.from(screenshot.data,'base64'));
            }
            assert.equal(await evaluate("document.activeElement.type"), 'search', 'opening focuses search when present');
            assert.ok(await evaluate("document.querySelector('.rgf-recroby-switcher-list').scrollHeight > document.querySelector('.rgf-recroby-switcher-list').clientHeight"), 'long list scrolls independently');
            assert.ok(await evaluate("(() => {const popup=document.querySelector('.rgf-recroby-switcher'), r=popup.getBoundingClientRect();return popup.contains(document.elementFromPoint(r.x+10,r.y+10));})()"), 'top layer is visible outside the clipped workspace');
            assert.ok(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-choice-title')).webkitLineClamp === '2'"), 'list titles are capped at two lines');
            await evaluate("document.querySelector('[data-action=select]').focus()");
            await cdp('Input.dispatchKeyEvent',{type:'keyDown',key:'End'});
            assert.equal(await evaluate("document.activeElement === [...document.querySelectorAll('[data-action=select]')].at(-1)"),true, 'End reaches the final conversation');
            await cdp('Input.dispatchKeyEvent',{type:'keyDown',key:'ArrowDown'});
            assert.equal(await evaluate("document.activeElement === document.querySelector('[data-action=select]')"),true, 'arrow navigation wraps');
            await cdp('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape'});
            assert.equal(await evaluate("document.querySelector('.rgf-recroby-switcher').matches(':popover-open')"),false, 'Escape closes the list');
            assert.equal(await evaluate("document.activeElement === document.querySelector('[data-action=switch]')"),true, 'Escape restores selector focus');
        }
    }
    await viewport(1200,800);
    await layout({mode:3,isCollapsed:false});
    await click('[data-action=switch]');
    assert.ok((await rect('.rgf-recroby-switcher')).bottom <= (await rect('[data-action=switch]')).y, 'short bottom dock opens the list upwards');
    await layout({mode:1});
    assert.ok(Math.abs((await rect('.rgf-recroby-switcher')).x-(await rect('[data-action=switch]')).x)<1, 'open list follows docking');
    await viewport(900,500);
    assert.ok((await rect('.rgf-recroby-switcher')).bottom<=493, 'open list follows viewport resize');
    await click('#collapse');
    assert.equal(await evaluate("document.querySelector('.rgf-recroby-switcher').matches(':popover-open')"),false, 'collapse dismisses the top layer');
    await viewport(1200,800);
    await layout({mode:0,isCollapsed:false,height:560});
    await click('[data-action=switch]');
    await click('#outside');
    assert.equal(await evaluate("document.querySelector('.rgf-recroby-switcher').matches(':popover-open')"),false, 'outside click closes the list');
    await click('[data-action=switch]');
    await evaluate("document.querySelector('textarea').focus()");
    assert.equal(await evaluate("document.querySelector('.rgf-recroby-switcher').matches(':popover-open')"),false, 'tabbing out dismisses the list without stealing focus');
    assert.equal(await evaluate("document.activeElement.tagName"),'TEXTAREA');
    if (process.env.RGF_TEST_SCREENSHOT) {
        await click('[data-action=switch]');
        const screenshot = await cdp('Page.captureScreenshot', { format: 'png' });
        await writeFile(process.env.RGF_TEST_SCREENSHOT, Buffer.from(screenshot.data, 'base64'));
        await evaluate('setSwitcher(false)');
    }
    await evaluate("document.querySelector('.rgf-recroby-title').textContent='Conversation 1'");
    await evaluate("document.querySelector('.navbar').style.height='56px'");
    await layout({ mode: 0 });
    assert.ok((await rect('#send')).width < (await rect('.container')).width / 2, 'Send button does not stretch across the panel');
    await evaluate("document.body.style.height='1800px'");
    await delay(150);
    for (const mode of [1, 2, 3]) {
        await evaluate("window.scrollTo(0,0)");
        await layout({ mode, isCollapsed: false });
        const anchored = await rect('section');
        const preferences = await evaluate('JSON.stringify(settings)');
        for (const scrollY of [300, 900, 100]) {
            await evaluate(`window.scrollTo(0,${scrollY})`);
            await delay(100);
            assert.deepEqual(await rect('section'), anchored, 'docked geometry is independent of browser scroll');
            assert.equal(await evaluate('JSON.stringify(settings)'), preferences);
            if (mode !== 3) {
                assert.equal(anchored.y, 0);
                assert.equal(anchored.height, await evaluate('visualViewport.height'));
            } else assert.equal(anchored.bottom, await evaluate('visualViewport.height'));
        }
    }
    await evaluate("window.scrollTo(0,0); document.body.style.height=''");
    await delay(150);
    const movePointer = async (start, end) => {
        await cdp("Input.dispatchMouseEvent", { type: "mousePressed", ...start, button: "left", clickCount: 1 });
        await cdp("Input.dispatchMouseEvent", { type: "mouseMoved", ...end, button: "left", buttons: 1 });
        await cdp("Input.dispatchMouseEvent", { type: "mouseReleased", ...end, button: "left", clickCount: 1 });
        await delay(100);
    };
    for (const [mode, x, y] of [[1, 10, 220], [2, 1190, 220], [3, 600, 790]]) {
        await layout({ mode: 0, x: 250, y: 100 });
        const drag = await rect("header");
        await movePointer({ x: drag.x + 20, y: drag.y + 15 }, { x, y });
        assert.equal(await evaluate("settings.mode"), mode, "drag to each supported screen edge docks");
    }
    for (const mode of [0, 1, 2, 3]) {
        await layout({ mode, x: 250, y: 100, width: 440, height: 400, dockWidth: 360, dockHeight: 240 });
        const handle = await rect(".rgf-recroby-resize");
        const start = { x: handle.x + handle.width / 2, y: handle.y + handle.height / 2 };
        await movePointer(start, { x: start.x + (mode === 2 ? -40 : 40), y: start.y + (mode === 3 ? -40 : 40) });
        assert.equal(await evaluate(`settings.${mode === 0 ? "width" : mode === 3 ? "dockHeight" : "dockWidth"}`), mode === 0 ? 480 : mode === 3 ? 280 : 400);
        if (mode === 2) {
            await evaluate("document.querySelector('.rgf-toast-container').hidden=false");
            await toastRight(true);
            await evaluate("document.querySelector('.rgf-toast-container').hidden=true");
        }
        if (mode === 3) assert.equal((await rect('.rgf-recroby-content')).bottom, (await rect('section')).y, 'pointer resizing immediately updates the work-area boundary');
    }
    await layout({ mode: 3 });
    await evaluate("document.querySelector('#host').style.height='400px'");
    await delay(150);
    const bottomPanel = await rect('section');
    assert.equal((await rect('.rgf-recroby-content')).height, 400, 'a definite host height bounds the work area');
    assert.equal(bottomPanel.bottom, 800, 'bottom dock remains at viewport bottom after host resize');
    await evaluate("document.querySelector('#host').style.padding='20px'; document.querySelector('#host').style.border='2px solid';");
    await delay(150);
    assert.equal((await rect('.rgf-recroby-content')).height, 356, 'host padding and borders are excluded from its available content height');
    await evaluate("document.querySelector('#host').style.padding=''; document.querySelector('#host').style.border='';");
    await evaluate(`document.querySelector('#host').insertAdjacentHTML('afterbegin','<div id="host-prefix" style="height:40px;flex-shrink:0">Host prefix</div>')`);
    await delay(150);
    assert.equal((await rect('.rgf-recroby-content')).height, 360, 'content preceding the root is excluded from a bounded host');
    await evaluate(`document.querySelector('#host').style.display='flex';document.querySelector('#host').style.flexDirection='column';document.querySelector('#host').insertAdjacentHTML('beforeend','<div id="host-footer" style="height:50px;flex-shrink:0">Host footer</div>')`);
    await delay(150);
    assert.equal((await rect('.rgf-recroby-content')).height, 310, 'a bounded flex host keeps its external header and footer space');
    assert.equal((await rect('#host-footer')).height, 50);
    await evaluate("document.querySelector('#host-prefix').remove();document.querySelector('#host-footer').remove();document.querySelector('#host').style.display='';document.querySelector('#host').style.flexDirection=''");
    await evaluate("document.querySelector('#host').style.height=''");
    await evaluate("document.querySelector('#outside').style.height='100px'");
    await delay(150);
    assert.equal((await rect('.rgf-recroby-content')).y, 100, 'external header resizing moves the work area');
    assert.equal((await rect('.rgf-recroby-content')).bottom, (await rect('section')).y);
    await viewport(1000, 600);
    assert.equal((await rect('.rgf-recroby-content')).bottom, (await rect('section')).y);
    await viewport(1200, 800);
    await evaluate("document.querySelector('#outside').style.height=''");
    await delay(150);
    const beforeKeyboardResize = await rect('.rgf-recroby-content');
    await evaluate("document.querySelector('[data-resize-handle]').focus()");
    await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:'ArrowUp',code:'ArrowUp'});
    await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:'ArrowUp',code:'ArrowUp'});
    await delay(100);
    assert.equal((await rect('.rgf-recroby-content')).height, beforeKeyboardResize.height - 16, 'keyboard resizing immediately shrinks the work area');
    await layout({dockHeight:280});
    await evaluate("document.querySelector('.grid').innerHTML='<div id=large-content style=\"height:1500px;width:1800px\">Large page</div>'");
    for (const [mode, isCollapsed] of [[0, true], [0, false], [1, false], [2, false], [3, false]]) {
        await evaluate('window.scrollTo(0,0)');
        await layout({ mode, isCollapsed });
        assert.equal((await rect('#large-content')).height, 1500, 'host content retains its natural height');
        if (mode === 3) {
            assert.equal(await evaluate('document.documentElement.scrollHeight > window.innerHeight'), false, 'bottom docking contains tall content within the work area');
            assert.equal(await evaluate('document.documentElement.scrollWidth > window.innerWidth'), false, 'bottom docking contains wide content within the work area');
            assert.ok(await evaluate("document.querySelector('.rgf-recroby-content').scrollHeight > document.querySelector('.rgf-recroby-content').clientHeight"));
            assert.ok(await evaluate("document.querySelector('.rgf-recroby-content').scrollWidth > document.querySelector('.rgf-recroby-content').clientWidth"));
            const wheelArea = await rect('.rgf-recroby-content');
            await cdp('Input.dispatchMouseEvent', {type:'mouseWheel',x:wheelArea.x+100,y:wheelArea.y+100,deltaX:0,deltaY:200});
            await delay(150);
            assert.ok(await evaluate("document.querySelector('.rgf-recroby-content').scrollTop > 0"), 'mouse wheel scrolls the bounded work area');
            assert.equal(await evaluate('window.scrollY'), 0);
            for (const scrollTop of [0, 500, 2000]) {
                await evaluate(`document.querySelector('.rgf-recroby-content').scrollTop=${scrollTop}`);
                await delay(100);
                assert.equal((await rect('.rgf-recroby-content')).bottom, (await rect('section')).y, 'scrolling never moves the work area behind the panel');
            }
            assert.ok((await rect('#large-content')).bottom <= (await rect('section')).y + 1, 'last content is reachable above the dock');
        } else {
            assert.ok((await rect('.rgf-recroby-content')).height >= 1500);
            assert.equal(await evaluate('document.documentElement.scrollHeight > window.innerHeight'), true, 'other modes keep tall content in browser scrolling');
            assert.equal(await evaluate('document.documentElement.scrollWidth > window.innerWidth'), true, 'other modes keep wide content in browser scrolling');
        }
    }
    await evaluate("window.scrollTo(0,0); document.querySelector('.grid').textContent='Grid/Dashboard content'");
    await layout({ mode:3, isCollapsed:false });
    await evaluate(`document.querySelector('.grid').innerHTML='<table class="recro-grid table table-sm table-bordered table-hover mb-0"><thead><tr><th><div class="quick-filter"><input id="grid-filter" class="form-control"></div></th></tr></thead><tbody>'+ '<tr><td>Grid row</td></tr>'.repeat(80) + '<tr><td id="grid-last">Last row</td></tr></tbody></table>'`);
    await evaluate("document.querySelector('.rgf-recroby-content').scrollTop=10000");
    await delay(150);
    assert.ok((await rect('#grid-last')).bottom <= (await rect('section')).y);
    await evaluate("document.querySelector('#grid-filter').focus()");
    assert.equal(await evaluate('document.activeElement.id'), 'grid-filter');
    assert.ok((await rect('#grid-filter')).y >= (await rect('.rgf-recroby-content')).y);
    await evaluate(`document.querySelector('.grid').innerHTML='<form class="needs-validation"><div class="card"><div class="dialog-header card-header"><ul class="nav nav-tabs card-header-tabs"><li class="nav-item"><button type="button" class="nav-link active">Form</button></li></ul></div><div class="card-body"><div class="tab-content"><div class="tab-pane show active"><div style="height:1200px">Form fields</div><div class="rg-property" name="Last"><input id="form-last" class="rg-cru form-control"></div></div></div></div></div></form>'; document.querySelector('.rgf-recroby-content').scrollTop=0`);
    assert.equal(await evaluate("Recrovit.RGF.Blazor.UI.Base.ensureVisible('#form-last',true,null,0)"), true);
    await delay(100);
    assert.equal(await evaluate('document.activeElement.id'), 'form-last');
    assert.ok((await rect('#form-last')).bottom <= (await rect('section')).y);
    assert.equal(await evaluate('window.scrollY'), 0, 'Form validation navigation uses the work area rather than document scrolling');
    await evaluate(`document.querySelector('.grid').innerHTML='<div class="rgf-dashboard-page"><div class="d-flex mb-3">Dashboard toolbar</div><div class="rgf-dashboard-page-status"><div style="height:1400px">Dashboard content</div><button id="dashboard-last">Last dashboard control</button></div></div>'; document.querySelector('.rgf-recroby-content').scrollTop=0`);
    await delay(150);
    assert.ok((await rect('.rgf-dashboard-page')).bottom <= (await rect('section')).y, 'Dashboard fits the available work area');
    assert.ok(await evaluate("document.querySelector('.rgf-dashboard-page-status').scrollHeight > document.querySelector('.rgf-dashboard-page-status').clientHeight"));
    await evaluate("Recrovit.RGF.Blazor.UI.Base.ensureVisible('#dashboard-last',true,null,0)");
    assert.ok((await rect('#dashboard-last')).bottom <= (await rect('section')).y);
    await click('#collapse');
    assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-content')).overflow"), 'visible');
    await click('.rgf-recroby-launcher');
    assert.ok((await rect('.rgf-dashboard-page')).bottom <= (await rect('section')).y);
    await evaluate("document.querySelector('.grid').textContent='Grid/Dashboard content'; document.querySelector('.rgf-recroby-content').scrollTop=0");
    await layout({ mode: 0, x: 1000, y: 700, width: 900, height: 700 });
    await viewport(800, 600);
    const constrained = await rect("section");
    assert.ok(constrained.right <= 800 && constrained.bottom <= 600);
    await viewport(1200, 800);
    const desktop = await evaluate("JSON.stringify(settings)");
    await viewport(390, 700);
    const mobile = await rect("section");
    assert.equal(mobile.x, 0); assert.equal(mobile.y, 0);
    assert.equal(mobile.width, await evaluate("visualViewport.width")); assert.equal(mobile.height, await evaluate("visualViewport.height"));
    assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-resize')).display"), "none");
    assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-selector')).display"), "flex");
    assert.equal(await evaluate("document.querySelectorAll('.rgf-recroby-conversation-bar select, .rgf-recroby-conversation-bar .nav-tabs').length"), 0);
    assert.ok((await rect('[data-action=new]')).right <= mobile.right, 'mobile new conversation button remains visible beside the selector');
    await viewport(390, 360); // Reduced viewport models the keyboard's available visual viewport.
    assert.ok((await rect("textarea")).bottom <= 360, "keyboard leaves input visible");
    assert.ok(await evaluate("document.querySelector('.chat-box').scrollHeight > document.querySelector('.chat-box').clientHeight"));
    await layout({ isCollapsed: true, mode: 1 });
    assert.equal((await rect(".rgf-recroby-launcher")).right, await evaluate("visualViewport.width"));
    await layout({ isCollapsed: false, mode: JSON.parse(desktop).mode });
    await viewport(1200, 800);
    assert.equal(await evaluate("JSON.stringify(settings)"), desktop, "desktop preferences survived mobile/keyboard transitions");
    await evaluate("reinitialize()");
    assert.equal(await evaluate("JSON.stringify(settings)"), desktop, "browser persistence restored only layout");
    assert.deepEqual(await evaluate("Object.keys(JSON.parse(localStorage.getItem('rgf.recroby.workspace.v1'))).sort()"),
        ['mode', 'isCollapsed', 'x', 'y', 'width', 'height', 'dockWidth', 'dockHeight'].sort());
    await layout({ mode: 0, width: 330, height: 300, x: 50, y: 70 });
    await viewport(390, 700);
    assert.equal((await rect('section')).height, await evaluate('visualViewport.height'), "small floating desktop size still becomes full-screen on mobile");
    await viewport(1200, 800);
    await evaluate("localStorage.setItem('rgf.recroby.workspace.v1', '{corrupt'); reinitialize()");
    assert.equal(await evaluate("settings.width"), 330, "corrupt storage does not break initialization");
    await evaluate("Object.defineProperty(window, 'localStorage', {configurable:true, get() {throw new Error('Storage blocked');}}); reinitialize()");
    await layout({ mode: 2, isCollapsed: true });
    assert.equal(await evaluate("settings.mode"), 2, "blocked storage leaves workspace usable");
    await evaluate("document.body.style.height='1600px'; window.scrollTo(0,300)");
    await delay(100);
    const scrolledTab = await rect('.rgf-recroby-launcher');
    assert.ok(Math.abs(scrolledTab.y + scrolledTab.height / 2 - 400) < 1, "collapsed tab stays centered during scrolling");
    await evaluate("window.scrollTo(0,0)");
    await evaluate("document.body.style.height=''");
    await layout({ mode:3, isCollapsed: false });
    await evaluate(`document.querySelector('.navbar').classList.add('rgf-navbar'); document.querySelector('.navbar').innerHTML='<div class="rgf-navbar-shell"><div class="rgf-navbar-content"><ul class="navbar-nav"><li class="nav-item dropdown"><button id="host-menu" class="nav-link dropdown-toggle" data-bs-toggle="dropdown">Menu</button><ul class="dropdown-menu"><li><button id="host-menu-item" class="dropdown-item">Action</button></li></ul></li></ul></div></div>'; document.querySelector('#host-menu-item').onclick=()=>window.menuClicked=true`);
    await delay(150);
    await click('#host-menu');
    await click('#host-menu-item');
    assert.equal(await evaluate('window.menuClicked'), true, 'Navbar dropdown remains clickable inside the reduced work area');
    await evaluate("document.querySelector('.toast-container').hidden=false; document.querySelector('#toast-close').onclick=()=>document.querySelector('.toast-container').hidden=true");
    assert.ok((await rect('.toast')).bottom <= 800);
    await click('#toast-close');
    assert.equal(await evaluate("document.querySelector('.toast-container').hidden"), true, 'Toast above bottom dock is visible and clickable');
    // Match DialogComponent's explicit backdrop/show markup; its primary button receives focus.
    await evaluate(`document.querySelector('.rgf-recroby-content').insertAdjacentHTML('beforeend','<div class="modal-backdrop show"></div><div id="host-dialog" class="modal fade show" tabindex="-1" role="dialog" style="display:block"><div class="modal-dialog modal-dialog-scrollable"><div class="modal-content"><div class="modal-header">Host dialog</div><div class="modal-body"><input id="dialog-input"></div><div class="modal-footer"><button id="dialog-primary" class="btn btn-primary">OK</button></div></div></div></div>'); document.querySelector('#dialog-primary').onclick=()=>{document.querySelector('#host-dialog').remove();document.querySelector('.modal-backdrop').remove();}; document.querySelector('#dialog-primary').focus()`);
    assert.equal(await evaluate('document.activeElement.id'), 'dialog-primary');
    const dialogInput = await rect('#dialog-input');
    assert.ok(dialogInput.y >= 0 && dialogInput.bottom <= 800);
    await click('#dialog-input');
    assert.equal(await evaluate('document.activeElement.id'), 'dialog-input');
    await click('#dialog-primary');
    assert.equal(await evaluate("document.querySelector('#host-dialog')"), null, 'Dialog above bottom dock remains operable');
    assert.equal(await evaluate('selectionRegistrations'), 6, 'late catalog panels each register three handlers exactly once');
    await evaluate(`(() => {
        const message = document.createElement('div');
        message.id = 'markdown-message';
        message.className = 'message text-bg-secondary align-self-start';
        message.innerHTML = '<div class="rgf-markdown-viewer"><div class="rgf-markdown-toolbar"><button type="button" class="btn btn-sm rgf-markdown-copy" title="Copy" aria-label="Copy"><i class="bi bi-copy" aria-hidden="true"></i></button><button type="button" class="btn btn-sm rgf-markdown-toggle" aria-label="View plain text"><i class="bi bi-file-text"></i></button></div><div class="rgf-markdown-content"><h2>Long answer</h2><p>' + 'long-link-text'.repeat(60) + '</p><pre><code>' + 'wide code '.repeat(150) + '</code></pre><table><tr>' + '<td>Wide table cell</td>'.repeat(30) + '</tr></table></div></div>';
        document.querySelector('.chat-box').append(message);
    })()`);
    await viewport(1200, 800);
    await layout({mode:0, isCollapsed:false, width:560, height:560});
    await evaluate(`(() => {
        const light = document.querySelector('#markdown-message').cloneNode(true);
        light.id = 'markdown-light';
        light.className = 'message text-bg-light align-self-start';
        light.querySelector('.rgf-markdown-content').textContent = 'Light background';
        document.querySelector('.chat-box').append(light);
        window.markdownToggleStyles = selector => {
            const button = document.querySelector(selector), style = getComputedStyle(button);
            const rgb = value => value.match(/[\\d.]+/g).map(Number);
            const foreground = rgb(style.color), border = rgb(style.borderTopColor), outline = rgb(style.outlineColor);
            let background = [255,255,255], opacity = 1;
            for (let element = button; element; element = element.parentElement) {
                opacity *= Number(getComputedStyle(element).opacity);
            }
            // Composite transparent ancestor backgrounds in paint order.
            const ancestors = [];
            for (let element = button; element; element = element.parentElement) ancestors.unshift(element);
            const buttonBounds = button.getBoundingClientRect();
            const center = {x:buttonBounds.x + buttonBounds.width/2, y:buttonBounds.y + buttonBounds.height/2};
            for (const element of ancestors) {
                const bounds = element.getBoundingClientRect();
                // An absolutely positioned control can sit outside an ancestor's painted background.
                if (center.x < bounds.x || center.x > bounds.right || center.y < bounds.y || center.y > bounds.bottom) continue;
                const color = rgb(getComputedStyle(element).backgroundColor), alpha = color[3] ?? 1;
                background = background.map((value,index) => color[index]*alpha + value*(1-alpha));
            }
            const luminance = color => color.slice(0,3).map(value => {
                value /= 255;
                return value <= .04045 ? value/12.92 : ((value+.055)/1.055)**2.4;
            }).reduce((sum,value,index) => sum + value*[.2126,.7152,.0722][index], 0);
            const contrast = color => {
                const a = luminance(color), b = luminance(background);
                return (Math.max(a,b)+.05)/(Math.min(a,b)+.05);
            };
            return {color:style.color, border:style.borderTopColor,
                parentColor:getComputedStyle(button.closest('.message')).color,
                iconColor:getComputedStyle(button.querySelector('i')).color,
                contrast:contrast(foreground), borderContrast:contrast(border), outlineContrast:contrast(outline),
                outlineWidth:parseFloat(style.outlineWidth), outlineStyle:style.outlineStyle,
                visible:style.display !== 'none' && style.visibility === 'visible' && opacity === 1
                    && button.getBoundingClientRect().width > 0 && button.getBoundingClientRect().height > 0,
                hover:button.matches(':hover'), active:button.matches(':active'), focusVisible:button.matches(':focus-visible')};
        };
    })()`);
    for (const id of ['markdown-message', 'markdown-light']) {
        for (const control of ['copy', 'toggle']) {
            const selector = '#' + id + ' .rgf-markdown-' + control;
            const checkContrast = async state => {
                const style = await evaluate(`markdownToggleStyles(${JSON.stringify(selector)})`);
                assert.equal(style.visible, true, state + ': toggle is fully visible');
                assert.equal(style.color, style.parentColor, state + ': inherits surrounding text color');
                assert.equal(style.iconColor, style.color, state + ': icon follows text color');
                assert.equal(style.border, style.color, state + ': border follows text color');
                assert.ok(style.borderContrast >= 3, state + ': border contrast is at least 3:1');
                assert.ok(style.contrast >= 4.5, state + ': icon contrast is at least 4.5:1');
                return style;
            };
            await cdp('Input.dispatchMouseEvent', {type:'mouseMoved', x:0, y:0});
            await evaluate(`document.activeElement.blur(); document.querySelector(${JSON.stringify(selector)}).scrollIntoView({block:'center'})`);
            assert.equal((await checkContrast(id + ' normal')).hover, false, 'normal visibility does not require hover');
            const button = await rect(selector);
            const point = {x:button.x + button.width/2, y:button.y + button.height/2};
            await cdp('Input.dispatchMouseEvent', {type:'mouseMoved', ...point});
            assert.equal((await checkContrast(id + ' hover')).hover, true);
            await cdp('Input.dispatchMouseEvent', {type:'mousePressed', button:'left', clickCount:1, ...point});
            assert.equal((await checkContrast(id + ' active')).active, true);
            await cdp('Input.dispatchMouseEvent', {type:'mouseReleased', button:'left', clickCount:1, ...point});
            await checkContrast(id + ' focus');
            await cdp('Input.dispatchMouseEvent', {type:'mouseMoved', x:0, y:0});
            await cdp('Input.dispatchKeyEvent', {type:'keyDown', key:'Tab'});
            await cdp('Input.dispatchKeyEvent', {type:'keyUp', key:'Tab'});
            await evaluate(`document.querySelector(${JSON.stringify(selector)}).focus()`);
            const focused = await checkContrast(id + ' keyboard focus');
            assert.equal(focused.focusVisible, true);
            assert.ok(focused.outlineWidth >= 2 && focused.outlineStyle === 'solid' && focused.outlineContrast >= 3,
                'keyboard focus has a contrasting outline');
        }
    }
    await evaluate("document.activeElement.blur(); document.querySelector('#markdown-light').remove()");
    console.log('PASS: Copy and Markdown toggle computed colors, contrast, normal/hover/active/focus-visible on light and dark bubbles');
    await evaluate("document.querySelector('#markdown-message').classList.add('rgf-ai-copy', 'rgf-ai-markdown')");
    // Mirror actual keyed viewer markup; bUnit verifies the component interop and feedback.
    await evaluate(`(() => {
        const chat = document.querySelector('.chat-box');
        for (const [id, markdown] of [['short-markdown', true], ['plain-message', false]]) {
            const message = document.querySelector('#markdown-message').cloneNode(true);
            message.id = id;
            message.querySelector('.rgf-markdown-content').outerHTML = '<div class="rgf-markdown-plain">Short reply</div>';
            if (!markdown) {
                message.classList.remove('rgf-ai-markdown');
                message.querySelector('.rgf-markdown-toggle').remove();
            }
            chat.append(message);
            const usage = document.createElement('div');
            usage.className = 'rgf-ai-usage';
            usage.textContent = 'Usage / Credit';
            chat.append(usage);
        }
        const usage = document.createElement('div');
        usage.className = 'rgf-ai-usage';
        usage.textContent = 'Usage / Credit';
        document.querySelector('#markdown-message').after(usage);
    })()`);
    for (const [width, height] of [[1200, 800], [800, 400], [390, 700]]) {
        await viewport(width, height);
        for (const mode of [0, 1, 2, 3]) {
            await layout({mode, isCollapsed:false, width:360, dockWidth:300});
            for (const id of ['markdown-message', 'short-markdown', 'plain-message']) {
                await evaluate(`document.querySelector('#${id}').scrollIntoView({block:'start'})`);
                assert.equal(await evaluate(`(() => {
                    const panel = document.querySelector('section'), message = document.querySelector('#${id}');
                    const buttons = [...message.querySelectorAll('.rgf-markdown-toolbar button')];
                    const copy = buttons[0].getBoundingClientRect();
                    const bubble = message.getBoundingClientRect(), chat = message.closest('.chat-box').getBoundingClientRect();
                    const next = message.nextElementSibling.getBoundingClientRect();
                    const toggle = buttons[1]?.getBoundingClientRect();
                    return buttons[0].classList.contains('rgf-markdown-copy')
                        && buttons.length === (message.classList.contains('rgf-ai-markdown') ? 2 : 1)
                        && Math.abs(copy.y - bubble.y) < 1
                        && (!toggle || (Math.abs(toggle.x - copy.x) < 1 && Math.abs(toggle.y - copy.bottom - 4) < 1))
                        && buttons.every(button => {
                            const r = button.getBoundingClientRect();
                            return r.x >= bubble.right && r.right <= chat.right - 4 && r.bottom <= bubble.bottom + 1 && r.bottom <= next.y + 1;
                        })
                        && panel.scrollWidth <= panel.clientWidth + 1 && bubble.right <= panel.getBoundingClientRect().right;
                })()`), true, id + ': Copy is first at bubble top; toggle is below; no text/Usage overlap in mode ' + mode);
                for (const control of id === 'plain-message' ? ['copy'] : ['copy', 'toggle']) {
                    const style = await evaluate(`markdownToggleStyles('#${id} .rgf-markdown-${control}')`);
                    assert.ok(style.visible && style.contrast >= 4.5, 'outside controls stay visible against the chat background');
                }
            }
            assert.equal(await evaluate(`(() => {
                const content = document.querySelector('#markdown-message .rgf-markdown-content');
                return content.querySelector('pre').scrollWidth > content.querySelector('pre').clientWidth
                    && content.querySelector('table').scrollWidth > content.querySelector('table').clientWidth;
            })()`), true, 'wide Markdown retains internal scrolling');
        }
    }
    await cdp('Browser.grantPermissions', {origin:await evaluate('location.origin'), permissions:['clipboardReadWrite', 'clipboardSanitizedWrite']});
    await evaluate(`(() => {
        window.copySource = '  **original**' + String.fromCharCode(13,10,13,10,9) + 'text  ';
        const message = document.querySelector('#plain-message');
        const status = document.createElement('div');
        status.setAttribute('role', 'status');
        status.className = 'rgf-markdown-copy-status visually-hidden';
        message.querySelector('.rgf-markdown-viewer').append(status);
        // Exercise native browser permission behavior; component handling is covered in bUnit.
        message.querySelector('.rgf-markdown-copy').onmouseleave = () => {
            message.querySelector('.rgf-markdown-copy i').className = 'bi bi-copy';
            status.textContent = '';
        };
        message.querySelector('.rgf-markdown-copy').onclick = () => {
            status.textContent = '';
            const icon = message.querySelector('.rgf-markdown-copy i');
            icon.className = 'bi bi-copy';
            window.copyOperation = navigator.clipboard.writeText(copySource).then(() => {
                icon.className = 'bi bi-check-lg';
                status.textContent = 'Copied'; status.classList.add('visually-hidden'); status.setAttribute('role', 'status');
            }, () => {status.textContent = 'Copy failed'; status.classList.remove('visually-hidden'); status.setAttribute('role', 'alert');});
        };
        message.scrollIntoView({block:'center'});
    })()`);
    await click('#plain-message .rgf-markdown-copy');
    await evaluate('copyOperation');
    assert.equal(await evaluate("document.querySelector('#plain-message [role=status]').textContent"), 'Copied');
    assert.equal(await evaluate('navigator.clipboard.readText()'), await evaluate('copySource'), 'native clipboard receives original whitespace and Markdown');
    await evaluate(`window.originalWriteText = navigator.clipboard.writeText; navigator.clipboard.writeText = () => Promise.reject(new DOMException('Denied', 'NotAllowedError'))`);
    await click('#plain-message .rgf-markdown-copy');
    await evaluate('copyOperation');
    assert.equal(await evaluate("document.querySelector('#plain-message [role=alert]').textContent"), 'Copy failed');
    await evaluate(`navigator.clipboard.writeText = originalWriteText; document.querySelector('#short-markdown').nextElementSibling.remove(); document.querySelector('#short-markdown').remove(); document.querySelector('#plain-message').nextElementSibling.remove(); document.querySelector('#plain-message').remove(); document.querySelector('#markdown-message').nextElementSibling.remove()`);
    console.log('PASS: Copy-first toolbar geometry for short/long/plain replies, Usage, all docking/mobile layouts, native clipboard success/rejection');
    await evaluate("document.querySelector('#markdown-message').remove()");
    await evaluate("document.querySelector('#model-choice').lastChild.textContent=' provider / ' + 'long-model-name-'.repeat(20) + ' ▾'");
    for (const [width, height] of [[1200, 800], [800, 400], [390, 700], [390, 260]]) {
        await viewport(width, height);
        for (const mode of [0, 1, 2, 3]) {
            await layout({ mode, isCollapsed: false, height: 360, dockHeight: 300 });
            const before = await rect('section');
            for (const id of ['model', 'effort', 'format']) {
                await evaluate(`document.querySelector('#${id}-choice').scrollIntoView({block:'nearest'})`);
                await click(`#${id}-choice`);
                const popup = await rect(`#${id}-panel`), anchor = await rect(`#${id}-choice`);
                const selectors = await rect('.rgf-recroby-selectors');
                assert.ok(anchor.x >= selectors.x && anchor.right <= selectors.right + 1, 'selector with icon stays within its container');
                assert.equal(await evaluate("getComputedStyle(document.querySelector('#model-choice')).textOverflow"), 'ellipsis');
                assert.equal(await evaluate(`document.querySelector('#${id}-panel').matches(':popover-open')`), true);
                assert.ok(popup.y >= 7 && popup.bottom <= height - 7 && popup.x >= 7 && popup.right <= width - 7, `AI ${id} panel fits ${mode} at ${width}x${height}`);
                assert.ok(popup.bottom <= anchor.y, 'AI selection opens upwards');
                assert.deepEqual(await rect('section'), before, 'AI popover does not resize the workspace');
                assert.ok(await evaluate(`(() => {const p=document.querySelector('#${id}-panel'),r=p.getBoundingClientRect();return p.contains(document.elementFromPoint(r.x+10,r.y+10));})()`), 'AI top layer is visible outside overflow hidden');
                assert.equal(await evaluate(`document.querySelector('#${id}-panel').contains(document.activeElement)`), true);
                await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape' });
                await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape' });
                await delay(50);
                assert.equal(await evaluate(`document.querySelector('#${id}-panel').matches(':popover-open')`), false);
            }
            await click('#model-choice');
            await layout({ isCollapsed: true });
            assert.equal(await evaluate("document.querySelector('#model-panel').matches(':popover-open')"), false, 'collapse closes AI top layer');
        }
    }
    await viewport(1200, 800);
    await evaluate("document.querySelector('#model-choice').lastChild.textContent='Provider / Model ▾'");
    await layout({ mode: 0, isCollapsed: false, height: 560 });
    for (const id of ['model', 'effort', 'format']) {
        await click('#' + id + '-choice');
        if (id === 'model') {
            await evaluate("document.querySelector('#model-panel select').dispatchEvent(new Event('change', {bubbles:true}))");
            assert.equal(await evaluate("document.querySelector('#model-panel').matches(':popover-open')"), true,
                'provider selection leaves the model selector available');
        }
        await evaluate(`document.querySelector('#${id}-panel select[data-close-popover]').dispatchEvent(new Event('change', {bubbles:true}))`);
        assert.equal(await evaluate(`document.querySelector('#${id}-panel').matches(':popover-open')`), false,
            'committing a model or effort selection immediately closes its Popover');
        assert.equal(await evaluate('document.activeElement.id'), id + '-choice');
    }
    await layout({ width: 560 });
    const input = await rect('textarea'), send = await rect('#send'), selectors = await rect('.rgf-recroby-selectors');
    assert.ok(send.y >= input.bottom, 'Send is below the message input');
    assert.ok(selectors.x >= send.right && selectors.y < send.bottom, 'selectors are beside Send when space permits');
    await evaluate("document.querySelector('.rgf-ai-actions').style.width='180px'");
    await delay(100);
    assert.ok((await rect('.rgf-recroby-selectors')).y >= (await rect('#send')).bottom, 'selectors wrap below Send when space is limited');
    await evaluate("document.querySelector('.rgf-ai-actions').style.width=''");
    await click('#model-choice');
    await evaluate("document.querySelector('.rgf-recroby-conversation').hidden=true");
    await delay(50);
    assert.equal(await evaluate("document.querySelector('#model-panel').matches(':popover-open')"), false, 'conversation switch closes AI top layer');
    await evaluate("document.querySelector('.rgf-recroby-conversation').hidden=false");
    await click('#effort-choice');
    await evaluate('selectionSync(true)');
    assert.equal(await evaluate("document.querySelector('#effort-panel').matches(':popover-open')"), false, 'processing closes AI top layer');
    assert.equal(await evaluate('usageListeners'), 0, 'closed usage history has no global listeners');
    await cdp('Emulation.setTimezoneOverride', {timezoneId:'America/New_York'});
    await evaluate("document.querySelectorAll('.rgf-ai-usage').forEach(usage.sync)");
    assert.equal(await evaluate("document.querySelector('[data-local-time]').textContent"), await evaluate("new Date('2026-10-14T00:00:00Z').toLocaleString()"));
    assert.equal(await evaluate("document.querySelector('[data-local-date]').textContent"), await evaluate("new Date('2026-10-13T00:00:00').toLocaleDateString()"));
    for (const [width, height] of [[1200,800], [390,700], [390,260]]) {
        await viewport(width, height);
        for (const mode of [0,1,2,3]) {
            await layout({mode, isCollapsed:false, height:560, dockHeight:300});
            for (const group of ['models','cost','credit','tokens']) {
                const button = '#usage-39-' + group + '-button', panel = '#usage-39-' + group;
                await evaluate(`document.querySelector('${button}').scrollIntoView({block:'nearest'})`);
                const before = await rect('section');
                await click(button);
                assert.equal(await evaluate(`document.querySelector('${panel}').matches(':popover-open')`), true, `usage ${group} opens in mode ${mode} at ${width}x${height}`);
                const popup = await rect(panel), anchor = await rect(button);
                const fieldName = await rect(panel + ' dt'), fieldValue = await rect(panel + ' dd');
                assert.ok(fieldName.right <= fieldValue.x && Math.abs(fieldName.y - fieldValue.y) < 1,
                    'Popover field names and values are adjacent on the same row');
                assert.ok(popup.x >= 7 && popup.right <= width - 7 && popup.y >= 7 && popup.bottom <= height - 7, 'usage panel fits viewport in every layout');
                assert.ok(Math.min(Math.abs(popup.bottom - anchor.y), Math.abs(popup.y - anchor.bottom)) <= 5, 'usage panel stays near trigger');
                assert.deepEqual(await rect('section'), before, 'usage panel does not resize workspace');
                assert.ok(await evaluate(`(() => {const p=document.querySelector('${panel}'), r=p.getBoundingClientRect(); return p.contains(document.elementFromPoint(r.x+10,r.y+10));})()`), 'usage panel is in visible top layer');
                assert.equal(await evaluate(`document.activeElement.id`), panel.slice(1));
                assert.equal(await evaluate(`document.querySelector('${button}').getAttribute('aria-expanded')`), 'true');
                assert.equal(await evaluate("document.querySelector('.chat-box').scrollWidth <= document.querySelector('.chat-box').clientWidth"), true, 'long model identifiers wrap');
                await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:'Escape'});
                await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:'Escape'});
                await delay(50);
                assert.equal(await evaluate(`document.querySelector('${panel}').matches(':popover-open')`), false);
                assert.equal(await evaluate('document.activeElement.id'), button.slice(1));
                assert.equal(await evaluate('usageListeners'), 0, 'Escape releases active resources');
            }
            await click('#usage-39-cost-button');
            await layout({isCollapsed:true});
            assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), false, 'collapse closes usage panel');
            assert.equal(await evaluate('usageListeners'), 0);
        }
    }
    await viewport(1200,800);
    await layout({mode:0,isCollapsed:false,height:560});
    await evaluate("document.querySelector('#usage-39-cost-button').scrollIntoView({block:'nearest'})");
    await click('#usage-39-cost-button');
    await evaluate("document.querySelector('.rgf-recroby-conversation').hidden=true");
    await delay(50);
    assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), false, 'conversation switch closes usage panel');
    assert.equal(await evaluate('usageListeners'), 0);
    await evaluate("document.querySelector('.rgf-recroby-conversation').hidden=false");
    await click('#usage-39-cost-button');
    await cdp('Input.dispatchMouseEvent', {type:'mousePressed',x:5,y:5,button:'left',clickCount:1});
    await cdp('Input.dispatchMouseEvent', {type:'mouseReleased',x:5,y:5,button:'left',clickCount:1});
    await delay(50);
    assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), false, 'outside click dismisses usage');
    await click('#usage-39-cost-button');
    await click('#effort-choice');
    assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), false, 'existing selectors replace usage popovers');
    assert.equal(await evaluate("document.querySelector('#effort-panel').matches(':popover-open')"), true);
    await evaluate('selectionSync(true)');
    await evaluate(`(() => {
        const region = document.createElement('div');
        region.id = 'other-conversation'; region.className = 'rgf-recroby-conversation'; region.hidden = true;
        const row = document.querySelector('#usage-row-39').cloneNode(true);
        for (const node of [row, ...row.querySelectorAll('[id]')]) node.id += '-other';
        for (const button of row.querySelectorAll('[popovertarget]')) {
            button.setAttribute('popovertarget', button.getAttribute('popovertarget') + '-other');
            button.setAttribute('aria-controls', button.getAttribute('popovertarget'));
        }
        region.append(row); document.querySelector('section').append(region); usage.sync(row);
    })()`);
    await click('#usage-39-cost-button');
    await evaluate("document.querySelector('.rgf-recroby-conversation').hidden=true; document.querySelector('#other-conversation').hidden=false");
    await delay(50);
    assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), false);
    await click('#usage-39-cost-button-other');
    assert.equal(await evaluate("document.querySelector('#usage-39-cost-other').matches(':popover-open')"), true, 'another conversation opens its own independent panel');
    assert.equal(await evaluate('usageListeners'), 2, 'only the active panel holds global listeners');
    await evaluate("document.querySelector('#other-conversation').hidden=true; document.querySelector('.rgf-recroby-conversation').hidden=false");
    await delay(50);
    assert.equal(await evaluate("document.querySelector('#usage-39-cost-other').matches(':popover-open')"), false);
    await evaluate("document.querySelector('#other-conversation').remove(); usage.dispose('usage-row-39-other')");
    assert.equal(await evaluate('usageListeners'), 0);
    await evaluate("document.querySelector('#usage-39-cost-button').focus()");
    assert.equal(await evaluate('document.activeElement.id'), 'usage-39-cost-button');
    await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:'Enter',code:'Enter',windowsVirtualKeyCode:13,text:'\r',unmodifiedText:'\r'});
    await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});
    await delay(50);
    assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), true, 'keyboard activates native usage button');
    await evaluate("document.querySelector('#usage-39-cost').hidePopover()");
    await delay(50);
    await click('#usage-39-cost-button');
    await evaluate("usage.dispose('usage-row-39')");
    assert.equal(await evaluate("document.querySelector('#usage-39-cost').matches(':popover-open')"), false, 'dispose closes top layer');
    assert.equal(await evaluate('usageListeners'), 0, 'dispose releases all global listeners');
    await click('#usage-39-cost-button');
    await delay(50);
    assert.equal(await evaluate('usageListeners'), 0, 'dispose removes panel toggle handlers');
    await evaluate("document.querySelector('#usage-39-cost').hidePopover()");
    await evaluate("document.querySelector('#usage-0-cost-button').scrollIntoView({block:'nearest'})");
    await click('#usage-0-cost-button');
    assert.equal(await evaluate('usageListeners'), 2);
    await evaluate("document.querySelector('#usage-row-0').remove(); usage.dispose('usage-row-0')");
    assert.equal(await evaluate('usageListeners'), 0, 'disposal by stable ID also works after Blazor removes the DOM node');
    await evaluate(`document.querySelector('.chat-box').innerHTML = '<div class="message text-bg-secondary align-self-start">Choose a response</div><fieldset class="rgf-ai-choices align-self-start mt-2"><legend class="fs-6">Choose a response</legend>' + [0,1].map(i => '<label class="rgf-ai-choice card p-2 mb-2"><span class="d-flex align-items-start gap-2"><input id="reply-'+i+'" type="radio" class="form-check-input flex-shrink-0" name="replies"><span>'+('LongLocalizedChoice'.repeat(20))+'</span></span></label>').join('') + '<button type="button" class="btn btn-success" id="confirm-reply">Confirm response</button></fieldset>'`);
    for (const [width, height] of [[1200,800], [390,700], [390,260]]) {
        await viewport(width, height);
        for (const mode of [0,1,2,3]) {
            await layout({mode, isCollapsed:false, height:560, dockHeight:300});
            await evaluate("document.querySelector('.rgf-ai-choices').scrollIntoView({block:'nearest'})");
            const cards = await rect('.rgf-ai-choices'), panel = await rect('section');
            assert.ok(cards.x >= panel.x && cards.right <= panel.right, 'continuation cards fit narrow dock/mobile panels');
            assert.equal(await evaluate("document.querySelector('.rgf-ai-choices').scrollWidth <= document.querySelector('.rgf-ai-choices').clientWidth"), true, 'long localized choices wrap without horizontal overflow');
        }
    }
    await viewport(1200,800);
    await layout({mode:0, isCollapsed:false, height:560});
    await evaluate("document.querySelector('#reply-0').focus()");
    await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:' ',code:'Space',windowsVirtualKeyCode:32,text:' '});
    await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:' ',code:'Space',windowsVirtualKeyCode:32});
    assert.equal(await evaluate("document.querySelector('#reply-0').checked"), true, 'Space selects a native continuation radio');
    await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:'ArrowDown',code:'ArrowDown',windowsVirtualKeyCode:40});
    await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:'ArrowDown',code:'ArrowDown',windowsVirtualKeyCode:40});
    assert.equal(await evaluate("document.querySelector('#reply-1').checked && !document.querySelector('#reply-0').checked"), true, 'arrow keys change the single selected response');
    assert.equal(await evaluate("getComputedStyle(document.querySelector('#reply-1').closest('label')).outlineStyle"), 'solid', 'focused continuation card has a visible outline');
    await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
    await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
    assert.equal(await evaluate('document.activeElement.id'), 'confirm-reply', 'Tab reaches separate confirmation after the radio group');
    await evaluate("document.querySelector('#confirm-reply').onclick=()=>window.replyConfirmed=true");
    await cdp('Input.dispatchKeyEvent', {type:'keyDown',key:'Enter',code:'Enter',windowsVirtualKeyCode:13,text:'\r'});
    await cdp('Input.dispatchKeyEvent', {type:'keyUp',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});
    assert.equal(await evaluate('window.replyConfirmed'), true, 'Enter activates confirmation');
    await evaluate("document.querySelector('.rgf-ai-choices').disabled=true");
    assert.equal(await evaluate("[...document.querySelectorAll('.rgf-ai-choices input, .rgf-ai-choices button')].every(e=>e.matches(':disabled'))"), true, 'processing fieldset disables every native reply control');
    console.log("PASS: continuation cards/keyboard, usage/selector top layer, viewport bounds, focus, Escape, outside click, collapse/switch/dispose, long history resources, local dates, all dock/mobile modes, host isolation, persistence, dialog/toast stacking");
    }
} finally {
    socket?.close(); child.kill();
    await new Promise(resolve => server.close(resolve));
    await delay(500);
    await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}
