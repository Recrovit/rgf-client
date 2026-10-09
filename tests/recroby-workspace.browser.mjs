// Runs the production workspace CSS/module in Edge, with a host confined to a subregion.
// dotnet/bUnit covers component lifetimes and transport; this covers browser geometry/events.
import assert from "node:assert/strict";
import { readFile, writeFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

const base = new URL("../src/RGF.Client.Blazor.UI/", import.meta.url);
const assets = {
    "/selection.js": ["text/javascript", await readFile(new URL("Components/AI/RgfRecrobyComponent.razor.js", base), "utf8")],
    "/selection.css": ["text/css", (await readFile(new URL("Components/AI/RgfRecrobyComponent.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/chat.css": ["text/css", (await readFile(new URL("Components/AI/RgfAiChatComponent.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/module.js": ["text/javascript", await readFile(new URL("Components/AI/RgfRecrobyWorkspace.razor.js", base), "utf8")],
    "/style.css": ["text/css", (await readFile(new URL("Components/AI/RgfRecrobyWorkspace.razor.css", base), "utf8")).replaceAll("::deep", "")],
    "/bootstrap.css": ["text/css", await readFile(new URL("wwwroot/lib/bootstrap/dist/css/bootstrap.min.css", base), "utf8")]
};
assets['/bootstrap.js'] = ['text/javascript', await readFile(new URL('wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js', base), 'utf8')];
assets['/jquery.js'] = ['text/javascript', await readFile(new URL('../RGF.Client.Blazor/wwwroot/lib/jquery/jquery.min.js', base), 'utf8')];
assets['/ui.js'] = ['text/javascript', await readFile(new URL('wwwroot/scripts/recrovit-rgf-blazor-ui.js', base), 'utf8')];
assets['/components.css'] = ['text/css', (await Promise.all(['Dashboard/DashboardPageComponent', 'GridComponent', 'NavbarComponent', 'DialogComponent'].map(name => readFile(new URL(`Components/${name}.razor.css`, base), 'utf8')))).join('\n').replaceAll('::deep', '')];
const html = `<!doctype html><html><head><meta charset="utf-8"><link rel="stylesheet" href="/bootstrap.css"><link rel="stylesheet" href="/style.css">
<link rel="stylesheet" href="/components.css"><link rel="stylesheet" href="/selection.css"><link rel="stylesheet" href="/chat.css"><script src="/jquery.js"></script><script src="/bootstrap.js"></script><script src="/ui.js"></script>
<style>body {margin:0} #outside {height:60px} #host {margin-left:120px; width:calc(100% - 160px)}
.chat-box {overflow-y:auto} .message {padding:10px} .navbar {height:56px} main {flex:1;min-height:0}
.grid {width:100%} .modal {z-index:1055} .toast-container {z-index:1080}</style></head><body>
<div id="outside">Unrelated application header</div><div id="host"><div class="rgf-recroby-workspace">
<div class="rgf-recroby-content"><nav class="navbar bg-light">Host navbar</nav><main><div class="grid">Grid/Dashboard content</div></main></div>
<button class="rgf-recroby-launcher btn btn-primary">Recroby</button><section class="rgf-recroby-panel card" hidden>
<header class="card-header" data-drag-handle><strong>Recroby</strong><button id="collapse">Collapse</button></header>
<div class="card-header rgf-recroby-conversation-bar">
<button id="conversation-selector" class="btn btn-sm btn-outline-secondary rgf-recroby-selector" data-action="switch" aria-haspopup="dialog" aria-expanded="false" aria-controls="conversation-list" title="general: Conversation 1"><span class="rgf-recroby-type">general</span><span class="rgf-recroby-title">Conversation 1</span><i aria-hidden="true">&#x2304;</i></button>
<button class="btn btn-sm btn-outline-secondary" data-action="new" aria-label="New conversation">+</button>
<div id="conversation-list" class="rgf-recroby-switcher" popover="manual" hidden role="dialog" aria-label="Conversations">
<div class="rgf-recroby-switcher-list"><div role="group" aria-label="general" data-conversation-type="general"><div class="rgf-recroby-group-title">general</div>
<div class="rgf-recroby-choice active"><button class="rgf-recroby-choice-select" data-action="select" aria-pressed="true"><span class="rgf-recroby-choice-title">Conversation 1</span><i aria-hidden="true">&#x2713;</i></button><button class="btn-close" aria-label="Close Conversation 1"></button></div>
</div></div></div></div>
<div class="rgf-recroby-conversation"><div class="container mt-4">
<div class="chat-box d-flex flex-column">${"<div class='message'>Message</div>".repeat(40)}</div>
<div class="input-group mt-3"><textarea class="form-control"></textarea></div><div class="rgf-ai-actions"><button class="btn align-self-start" id="send">Send</button>
<div class="rgf-recroby-selectors">
<button id="model-choice" class="btn btn-sm btn-outline-secondary" popovertarget="model-panel" aria-expanded="false">Provider / Model ▾</button>
<button id="effort-choice" class="btn btn-sm btn-outline-secondary" popovertarget="effort-panel" aria-expanded="false">Default ▾</button>
<div id="model-panel" class="rgf-recroby-selection-panel" popover="auto" role="dialog"><label>Provider</label><select class="form-select"><option>Provider</option></select><label>Model</label><select class="form-select"><option>Model</option></select></div>
<div id="effort-panel" class="rgf-recroby-selection-panel" popover="auto" role="dialog"><label>Reasoning effort</label><select class="form-select"><option>Default</option><option>High</option></select></div>
</div></div></div></div>
<div class="rgf-recroby-resize" data-resize-handle tabindex="0"></div></section></div></div>
<div class="toast-container position-fixed bottom-0 end-0 p-3" hidden><div class="toast show" role="alert"><div class="toast-header"><strong class="me-auto">Host toast</strong><button id="toast-close" class="btn-close"></button></div><div class="toast-body">Still working</div></div></div>
<div id="form-reference" class="card" style="position:fixed;left:-1000px;width:360px"><div class="dialog-header card-header"><ul class="nav nav-tabs card-header-tabs"><li class="nav-item"><button class="nav-link active">Form tab</button></li></ul></div></div>
<script type="module">import * as workspace from '/module.js';
import * as selection from '/selection.js';
const selectors = document.querySelector('.rgf-recroby-selectors');
selection.sync(selectors, false);
window.selectionSync = close => selection.sync(selectors, close);
const element = document.querySelector('.rgf-recroby-workspace');
window.settings = {mode:0,isCollapsed:true,x:250,y:100,width:440,height:560,dockWidth:360,dockHeight:240};
window.switcherOpen = false;
window.setSwitcher = open => {window.switcherOpen = open; element.querySelector('.rgf-recroby-switcher').hidden = !open; element.querySelector('[data-action=switch]').setAttribute('aria-expanded', String(open)); workspace.syncSwitcher(element, open, !open);};
window.sync = () => {element.querySelector('section').hidden = settings.isCollapsed; element.querySelector('.rgf-recroby-launcher').hidden = !settings.isCollapsed; if (settings.isCollapsed && switcherOpen) setSwitcher(false);};
element.querySelector('[data-action=switch]').onclick = () => setSwitcher(!switcherOpen);
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

const server = createServer((req, res) => {
    const [type, body] = assets[req.url] ?? ["text/html", html];
    res.setHeader("Content-Type", type); res.end(body);
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const profile = await mkdtemp(join(tmpdir(), "rgf-recroby-edge-"));
const executable = process.env.RGF_TEST_BROWSER ?? "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const child = spawn(executable, ["--headless", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
    "--remote-debugging-port=0", `--user-data-dir=${profile}`, "about:blank"], { windowsHide: true, stdio: "ignore" });
let socket;
try {
    let port;
    for (let i = 0; i < 100 && !port; i++) {
        try { port = (await readFile(join(profile, "DevToolsActivePort"), "utf8")).split("\n")[0]; } catch { await delay(100); }
    }
    assert.ok(port, "Edge debugging endpoint started");
    const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
    socket = new WebSocket(targets.find(t => t.type === "page").webSocketDebuggerUrl);
    await new Promise(resolve => socket.addEventListener("open", resolve, { once: true }));
    let id = 0; const pending = new Map();
    socket.addEventListener("message", event => {
        const message = JSON.parse(event.data);
        if (pending.has(message.id)) { pending.get(message.id)(message); pending.delete(message.id); }
    });
    const cdp = (method, params = {}) => new Promise((resolve, reject) => {
        const next = ++id;
        pending.set(next, message => message.error ? reject(new Error(JSON.stringify(message.error))) : resolve(message.result));
        socket.send(JSON.stringify({ id: next, method, params }));
    });
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
    await cdp("Page.navigate", { url: `http://127.0.0.1:${server.address().port}/` });
    for (let i = 0; i < 100 && !await evaluate("window.ready === true"); i++) await delay(50);
    assert.equal(await evaluate("document.querySelector('section').hidden"), true);
    const full = await rect(".rgf-recroby-content");
    assert.equal(full.height, 740, 'short host content fills the visible work area');
    assert.equal(await evaluate("getComputedStyle(document.querySelector('.rgf-recroby-content')).overflow"), 'visible');
    assert.equal(await evaluate('document.documentElement.scrollHeight > window.innerHeight'), false, 'short content needs no browser scrollbar');
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
    assert.ok((await rect('[data-action=new]')).right < (await rect('.rgf-recroby-conversation-bar')).right - 20, 'short conversation title keeps the selector at content width');
    assert.equal(await evaluate("document.querySelector('.rgf-recroby-title').scrollWidth > document.querySelector('.rgf-recroby-title').clientWidth"), false, 'short conversation title remains fully visible');
    assert.ok((await rect('[data-action=new]')).x >= selectorShape.right, 'new conversation button follows the flexible selector');
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
    for (const [width, height] of [[1200, 800], [800, 400], [390, 700], [390, 260]]) {
        await viewport(width, height);
        for (const mode of [0, 1, 2, 3]) {
            await layout({ mode, isCollapsed: false, height: 360, dockHeight: 300 });
            const before = await rect('section');
            for (const id of ['model', 'effort']) {
                await evaluate(`document.querySelector('#${id}-choice').scrollIntoView({block:'nearest'})`);
                await click(`#${id}-choice`);
                const popup = await rect(`#${id}-panel`), anchor = await rect(`#${id}-choice`);
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
    await layout({ mode: 0, isCollapsed: false, height: 560 });
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
    console.log("PASS: AI selectors and conversation switcher, top layer, viewport bounds, focus, Escape, collapse, all dock/mobile modes, host isolation, persistence, dialog/toast stacking");
} finally {
    socket?.close(); child.kill();
    await new Promise(resolve => server.close(resolve));
    await delay(500);
    await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}
