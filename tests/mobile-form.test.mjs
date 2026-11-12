import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { runInNewContext } from 'node:vm';

const source = readFileSync(new URL('../src/RGF.Client.Blazor.UI/wwwroot/scripts/recrovit-rgf-blazor-ui.js', import.meta.url), 'utf8');
const chrome = 'AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0';
const safari = 'AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0';

const cases = [
    ['Android Chrome phone', `(Linux; Android 10; K) ${chrome} Mobile Safari/537.36`, { mobile: true, platform: 'Android' }, true],
    ['Android Chrome tablet', `(Linux; Android 10; K) ${chrome} Safari/537.36`, { mobile: false, platform: 'Android' }, true],
    ['Android tablet without hints', `(Linux; Android 10; K) ${chrome} Safari/537.36`, undefined, true],
    ['Chrome Android desktop site', `(X11; Linux x86_64) ${chrome} Safari/537.36`, { mobile: false, platform: 'Linux' }, false],
    ['Desktop UA with conflicting hints', `(X11; Linux x86_64) ${chrome} Safari/537.36`, { mobile: true, platform: 'Android' }, false],
    ['Desktop hints with conflicting UA', `(Linux; Android 10; K) ${chrome} Mobile Safari/537.36`, { mobile: false, platform: 'Linux' }, false],
    ['iPhone Safari', `(iPhone; CPU iPhone OS 18_0 like Mac OS X) ${safari} Mobile/15E148 Safari/604.1`, undefined, true],
    ['iPad Safari mobile site', `(iPad; CPU OS 18_0 like Mac OS X) ${safari} Mobile/15E148 Safari/604.1`, undefined, true],
    ['Safari desktop site on iPhone or iPad', `(Macintosh; Intel Mac OS X 10_15_7) ${safari} Safari/605.1.15`, undefined, false],
    ['Desktop Chrome', `(Windows NT 10.0; Win64; x64) ${chrome} Safari/537.36`, { mobile: false, platform: 'Windows' }, false],
    ['Desktop Edge', `(Windows NT 10.0; Win64; x64) ${chrome} Safari/537.36 Edg/130.0.0.0`, { mobile: false, platform: 'Windows' }, false],
    ['Desktop Firefox', '(X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0', undefined, false],
    ['Reduced UA with mobile hint', '', { mobile: true, platform: 'Android' }, true],
    ['Unknown UA with Android platform alone', '', { mobile: false, platform: 'Android' }, false],
    ['Unknown UA without hints', '', undefined, false],
];

for (const [name, identity, userAgentData, expected] of cases) {
    test(name, () => {
        const navigator = { userAgent: `Mozilla/5.0 ${identity}`, userAgentData, maxTouchPoints: 5 };
        const window = { navigator, innerWidth: 320 };
        runInNewContext(source, { window });
        const ui = window.Recrovit.RGF.Blazor.UI;

        assert.equal(ui.Base.isMobileBrowserMode(), expected);

        window.innerWidth = 1400;
        navigator.userAgent = '';
        navigator.userAgentData = undefined;
        assert.equal(ui.Base.isMobileBrowserMode(), expected);
    });
}

test('fullscreen skips desktop setup and persistence, tracks the visual viewport and removes listeners', () => {
    const classes = new Set();
    const styles = new Map();
    const observers = new Set();
    const viewport = Object.assign(new EventTarget(), { width: 390, height: 700, offsetLeft: 0, offsetTop: 0 });
    let focusOptions;
    const dialog = {
        isConnected: true,
        classList: { add: name => classes.add(name) },
        matches: selector => selector === '[data-rgf-fullscreen-on-mobile="true"]',
        closest: () => null,
        style: { setProperty: (name, value) => styles.set(name, value) },
        querySelector: () => ({ querySelector: () => ({ focus: options => { focusOptions = options; } }) }),
    };
    const window = { navigator: { userAgent: 'Android Mobile' }, visualViewport: viewport };
    const document = { getElementById: () => dialog, body: {} };
    class MutationObserver {
        constructor(callback) { this.callback = callback; }
        observe() { observers.add(this); }
        disconnect() { observers.delete(this); }
    }
    runInNewContext(source, { window, document, MutationObserver });
    const ui = window.Recrovit.RGF.Blazor.UI;

    // Desktop setup and persistence would access jQuery or localStorage, which are absent here.
    ui.Dialog.initialize('form', true, 'entity', null, false, true);
    ui.Dialog.loadDialogPos('entity', 'form', true);
    ui.Dialog.saveDialogPos('entity', 'form');
    assert.ok(classes.has('rgf-mobile-fullscreen'));
    assert.equal(focusOptions.preventScroll, true);
    assert.equal(styles.get('--rgf-viewport-height'), '700px');

    Object.assign(viewport, { height: 360, offsetTop: 40 });
    viewport.dispatchEvent(new Event('resize'));
    assert.equal(styles.get('--rgf-viewport-height'), '360px');
    assert.equal(styles.get('--rgf-viewport-top'), '40px');

    dialog.isConnected = false;
    for (const observer of observers) observer.callback();
    assert.equal(observers.size, 0);
    viewport.height = 600;
    viewport.dispatchEvent(new Event('resize'));
    assert.equal(styles.get('--rgf-viewport-height'), '360px');
});
