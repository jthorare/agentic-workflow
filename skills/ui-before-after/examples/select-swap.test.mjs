import assert from "node:assert/strict";
import { test } from "node:test";

import scenario from "./select-swap.mjs";

test("each picker gets a field, menu-open and picked state with unique names", () => {
  assert.deepEqual(
    scenario.states.map((state) => state.name),
    ["staff-search-field", "staff-search-menu-open", "staff-search-picked"],
  );
  for (const state of scenario.states) {
    assert.equal(typeof state.crop, "function");
    assert.ok(state.claim.length > 0);
  }
});
