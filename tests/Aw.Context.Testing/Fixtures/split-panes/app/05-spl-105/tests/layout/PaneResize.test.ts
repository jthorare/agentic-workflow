import { test } from "node:test";
import assert from "node:assert/strict";
import { emptyLayout, withPanes } from "../../src/layout/PaneLayout.js";
import { MIN_PANE_COLUMNS, resizeFocused } from "../../src/layout/PaneResize.js";

const two = withPanes(emptyLayout(), [{ id: "a", columns: 40 }, { id: "b", columns: 40 }], "a");

test("resizing right grows the focused pane by the step", () => {
  assert.deepEqual(resizeFocused(two, "right", 2).panes.map((p) => p.columns), [42, 38]);
});

test("a pane never shrinks below the minimum width", () => {
  const narrow = withPanes(two, [{ id: "a", columns: MIN_PANE_COLUMNS }, { id: "b", columns: 70 }], "a");
  assert.equal(resizeFocused(narrow, "left", 2), narrow);
});
