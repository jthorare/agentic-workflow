import { test } from "node:test";
import assert from "node:assert/strict";
import { emptyLayout } from "../../src/layout/PaneLayout.js";
import { split, toggleOrientation } from "../../src/layout/SplitPane.js";
import { TabManager } from "../../src/tabs/TabManager.js";

test("switching back to a tab restores its orientation and focus", () => {
  const tabs = new TabManager(80);
  const first = toggleOrientation(split(emptyLayout(), "vertical"));
  tabs.open("tab-1", first);
  tabs.open("tab-2", emptyLayout());
  const second = tabs.switchTo(1, first);
  const back = tabs.switchTo(0, second);
  assert.equal(back.orientation, "horizontal");
  assert.equal(back.focused, first.focused);
});
