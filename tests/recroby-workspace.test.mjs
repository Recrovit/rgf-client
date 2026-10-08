import test from "node:test";
import assert from "node:assert/strict";
import { bounds, normalize } from "../src/RGF.Client.Blazor.UI/Components/AI/RgfRecrobyWorkspace.razor.js";

test("floating window stays visible after viewport shrinks without mutating desktop preferences", () => {
    const original = { x: 1000, y: 800, width: 900, height: 800 };
    const result = bounds(original, { left: 0, top: 0, width: 600, height: 400 }, { width: 500, height: 400 });
    assert.equal(result.x, 0); assert.equal(result.y, 0);
    assert.equal(result.width, 600); assert.equal(result.height, 400);
    assert.equal(original.width, 900);
    const restored = bounds(original, { left: 0, top: 0, width: 2000, height: 1600 }, { width: 2000, height: 1600 });
    assert.equal(restored.width, 900); assert.equal(restored.x, 1000);
});

test("dock width fits the workspace and dock height depends on viewport rather than document position", () => {
    const result = bounds({ dockWidth: 1000, dockHeight: 1000 }, { left: 0, top: 0, width: 2000, height: 1600 }, { width: 600, height: 500 });
    assert.equal(result.dockWidth, 300); assert.equal(result.dockHeight, 800);
    const scrolled = bounds({ dockHeight: 1000 }, { left: 0, top: 0, width: 2000, height: 1600 }, { width: 600, height: 100 });
    assert.equal(scrolled.dockHeight, result.dockHeight);
});

test("invalid browser preferences revert to defaults and cannot carry conversation secrets", () => {
    const result = normalize({ mode: 8, width: NaN, height: -300, isCollapsed: "false", messages: ["private"], conversationToken: "private" });
    assert.equal(result.mode, 0); assert.equal(result.width, 440); assert.equal(result.height, 160);
    assert.equal(result.isCollapsed, true);
    assert.equal(result.messages, undefined); assert.equal(result.conversationToken, undefined);
});
