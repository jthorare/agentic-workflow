import assert from "node:assert/strict";
import { test } from "node:test";

import { rowLayout } from "./layout.mjs";

test("puts main left and PR right, top-aligned under the label strip", () => {
  const layout = rowLayout([{ width: 200, height: 100 }, { width: 300, height: 150 }], {});
  assert.equal(layout.width, 524);
  assert.equal(layout.height, 182);
  assert.deepEqual(layout.at, [
    { left: 0, top: 32 },
    { left: 224, top: 32 },
  ]);
});

test("honours a custom gap and label height", () => {
  const layout = rowLayout([{ width: 100, height: 50 }, { width: 100, height: 50 }], { gap: 10, labelHeight: 20 });
  assert.equal(layout.width, 210);
  assert.equal(layout.height, 70);
  assert.deepEqual(layout.at[1], { left: 110, top: 20 });
});

test("places Figma panels after the PR, in order", () => {
  const layout = rowLayout([
    { width: 100, height: 50 },
    { width: 100, height: 50 },
    { width: 100, height: 80 },
  ]);
  assert.equal(layout.width, 348);
  assert.equal(layout.height, 112);
  assert.deepEqual(layout.at[2], { left: 248, top: 32 });
});
