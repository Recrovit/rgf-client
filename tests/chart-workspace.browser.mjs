// Production workspace sizing module and real ApexCharts in Edge. Blazor lifetimes are covered by bUnit.
import assert from 'node:assert/strict';
import { readFile, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';

const moduleSource = await readFile(new URL('../src/RGF.Client.Blazor.UI/wwwroot/Components/ChartWorkspace.js', import.meta.url));
const apexSource = await readFile(process.env.RGF_APEXCHARTS_ESM ?? join(process.env.USERPROFILE,
    '.nuget/packages/blazor-apexcharts/7.0.0/staticwebassets/js/apexcharts.esm.js'));
const html = `<!doctype html><html><head><meta charset="utf-8"><style>
body{margin:0} #entity{margin:20px;width:1000px} .toolbar{height:40px}
.rgf-apexchart-settings{height:120px} .rgf-apexchart-header{height:40px}
.footer{height:40px} .rgf-entity-chart{width:100%;min-height:400px}
</style></head><body><main id="entity"><div class="toolbar"><button id="toggle">Chart</button></div>
<div id="grid"><input id="selection" value="retained selection"></div>
<div class="rgf-entity-chart" style="display:none"><div class="rgf-chart-workspace">
<div class="rgf-apexchart-settings">Settings</div><div id="chart">
<div class="rgf-apexchart-header">Chart / DataSet</div><div id="apex"></div></div>
<div class="footer">Create / DataSet / Save / Delete</div></div></div></main>
<script type="module">
import * as sizing from '/module.js'; import ApexCharts from '/apex.js';
window.sizing=sizing;window.ApexCharts=ApexCharts;window.calls=[];window.creations=0;window.errors=[];
window.addEventListener('unhandledrejection',e=>errors.push(String(e.reason)));
window.chartRef={invokeMethodAsync:async(method,width,height)=>{
 calls.push({method,width,height});
 if(!window.chartInstance){window.creations++;window.chartInstance=new ApexCharts(document.querySelector('#apex'),{
 chart:{id:'persistent-chart',type:'bar',width:width-2,height:height-60,animations:{enabled:false},redrawOnParentResize:false,redrawOnWindowResize:false},
 series:[{name:'Amount',data:[10,20,30]}],xaxis:{categories:['A','B','C']}});await chartInstance.render();}
 else await sizing.resize('persistent-chart',width-2,height-60);
}};
window.show=async()=>{document.querySelector('#grid').style.display='none';document.querySelector('.rgf-entity-chart').style.display='';await sizing.activate('chart',chartRef);};
window.hide=()=>{document.querySelector('.rgf-entity-chart').style.display='none';document.querySelector('#grid').style.display='';};
window.ready=true;
</script></body></html>`;
const server = createServer((req, res) => {
    res.setHeader('Content-Type', req.url === '/' ? 'text/html' : 'text/javascript');
    res.end(req.url === '/module.js' ? moduleSource : req.url === '/apex.js' ? apexSource : html);
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
const profile = await mkdtemp(join(tmpdir(), 'rgf-chart-edge-'));
const child = spawn(process.env.RGF_TEST_BROWSER ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    ['--headless', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'],
    { windowsHide: true, stdio: 'ignore' });
let socket;
try {
    let port;
    for (let i = 0; i < 100 && !port; i++) {
        try { port = (await readFile(join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]; } catch { await delay(100); }
    }
    assert.ok(port, 'Edge debugging endpoint started');
    const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
    socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
    await new Promise((done, reject) => { socket.addEventListener('open', done, { once: true }); socket.addEventListener('error', reject, { once: true }); });
    let id = 0; const pending = new Map();
    socket.addEventListener('message', event => {
        const message = JSON.parse(event.data);
        pending.get(message.id)?.(message); pending.delete(message.id);
    });
    const cdp = (method, params = {}) => new Promise((done, reject) => {
        const next = ++id;
        const timeout = setTimeout(() => { pending.delete(next); reject(new Error(`Protocol timeout: ${method}`)); }, 30000);
        pending.set(next, message => { clearTimeout(timeout); message.error ? reject(new Error(JSON.stringify(message.error))) : done(message.result); });
        socket.send(JSON.stringify({ id: next, method, params }));
    });
    const evaluate = async expression => {
        const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
        if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
        return result.result.value;
    };
    await cdp('Emulation.setDeviceMetricsOverride', { width: 1200, height: 900, deviceScaleFactor: 1, mobile: false });
    await cdp('Page.navigate', { url: `http://127.0.0.1:${server.address().port}/` });
    for (let i = 0; i < 100 && !await evaluate('window.ready === true'); i++) await delay(50);
    await evaluate("sizing.activate('chart',chartRef)");
    await delay(100);
    assert.equal(await evaluate('calls.length'), 0, 'hidden chart is never measured');
    await evaluate('show()'); await delay(400);
    assert.equal(await evaluate('creations'), 1, JSON.stringify(await evaluate('({calls,errors,style:getComputedStyle(document.querySelector(".rgf-entity-chart")).display})')));
    assert.ok(await evaluate("document.querySelector('#apex svg').getBoundingClientRect().width > 900"));
    const firstCalls = await evaluate('calls.length');
    await evaluate('hide()');
    await cdp('Emulation.setDeviceMetricsOverride', { width: 900, height: 700, deviceScaleFactor: 1, mobile: false });
    await evaluate("document.querySelector('#entity').style.width='700px'"); await delay(200);
    assert.equal(await evaluate('calls.length'), firstCalls, 'hiding and resizing do not measure zero dimensions');
    await evaluate('show()'); await delay(400);
    assert.equal(await evaluate('creations'), 1, 'activation retains ApexCharts instance');
    assert.equal(await evaluate('calls.at(-1).width'), 700);
    assert.deepEqual(await evaluate('chartInstance.w.config.series[0].data'), [10, 20, 30]);
    assert.ok(await evaluate("document.querySelector('#apex svg').getBoundingClientRect().width > 600"));
    assert.equal(await evaluate("document.querySelector('#selection').value"), 'retained selection');
    assert.equal(await evaluate("document.querySelectorAll('.modal,.modal-backdrop').length"), 0);
    await evaluate("document.querySelector('#entity').style.width='450px'"); await delay(400);
    assert.equal(await evaluate('calls.at(-1).width'), 450, 'ResizeObserver follows workspace width');
    assert.ok(await evaluate('calls.every(call=>call.width>0&&call.height>0)'));
    const beforeDestroy = await evaluate('calls.length');
    await evaluate("sizing.destroy('chart');document.querySelector('#entity').style.width='600px'"); await delay(200);
    assert.equal(await evaluate('calls.length'), beforeDestroy, 'disposal disconnects observer');
    assert.deepEqual(await evaluate('errors'), []);
    console.log('PASS: visible initial sizing, hidden zero-size guard, retained ApexCharts instance/series, reactivation, viewport/host resize and observer disposal');
} finally {
    socket?.close(); child.kill();
    await new Promise(done => server.close(done));
    await delay(500);
    const absoluteProfile = resolve(profile);
    assert.ok(absoluteProfile.startsWith(resolve(tmpdir()) + '\\') && absoluteProfile.includes('rgf-chart-edge-'));
    await rm(absoluteProfile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}
