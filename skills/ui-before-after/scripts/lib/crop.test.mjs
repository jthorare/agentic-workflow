import assert from "node:assert/strict";
import { test } from "node:test";

import { paddedClip } from "./crop.mjs";

const viewport = { width: 1440, height: 900 };

test("pads the box and clamps it at the top-left edge", () => {
  assert.deepEqual(paddedClip({ x: 10, y: 10, width: 100, height: 40 }, viewport), {
    x: 0,
    y: 0,
    width: 126,
    height: 66,
  });
});

test("clamps the padded box at the bottom-right edge", () => {
  assert.deepEqual(paddedClip({ x: 1400, y: 880, width: 100, height: 40 }, viewport), {
    x: 1384,
    y: 864,
    width: 56,
    height: 36,
  });
});
