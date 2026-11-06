import assert from "node:assert/strict";
import { test } from "node:test";

import { closeFeatureUpdate } from "./overlays.mjs";

// Stands in for a Playwright page whose feature-update "Got it" button shows up after
// `appearsAfterMs`, or never when it is null.
const pageWithFeatureUpdate = (appearsAfterMs) => {
  const clicks = [];
  return {
    clicks,
    getByTestId: (testId) => ({
      waitFor: ({ timeout }) =>
        appearsAfterMs !== null && appearsAfterMs <= timeout && testId === "feature-update-dialog-got-it-button"
          ? new Promise((resolve) => setTimeout(resolve, appearsAfterMs))
          : Promise.reject(new Error("timed out")),
      click: async () => clicks.push(testId),
    }),
  };
};

test("closes the feature-update dialog when it is showing", async () => {
  const page = pageWithFeatureUpdate(0);
  await closeFeatureUpdate(page);
  assert.deepEqual(page.clicks, ["feature-update-dialog-got-it-button"]);
});

test("closes the feature-update dialog when it shows up a few seconds after the page loads", async () => {
  const page = pageWithFeatureUpdate(3_000);
  await closeFeatureUpdate(page);
  assert.deepEqual(page.clicks, ["feature-update-dialog-got-it-button"]);
});

test("does nothing when no feature-update dialog shows up", async () => {
  const page = pageWithFeatureUpdate(null);
  await closeFeatureUpdate(page);
  assert.deepEqual(page.clicks, []);
});
