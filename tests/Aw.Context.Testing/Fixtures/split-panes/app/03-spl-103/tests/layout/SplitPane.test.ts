import { test } from "node:test";
import assert from "node:assert/strict";
import { emptyLayout } from "../../src/layout/PaneLayout.js";
import { split, toggleOrientation } from "../../src/layout/SplitPane.js";

test("splitting adds a pane, focuses it and shares the width evenly", () => {
  const layout = split(emptyLayout(), "vertical");
  assert.equal(layout.panes.length, 2);
  assert.equal(layout.focused, layout.panes[1]?.id);
  assert.deepEqual(layout.panes.map((p) => p.columns), [40, 40]);
});

test("toggling orientation keeps the focused pane first", () => {
  const layout = toggleOrientation(split(emptyLayout(), "vertical"));
  assert.equal(layout.orientation, "horizontal");
  assert.equal(layout.panes[0]?.id, layout.focused);
});
