import assert from "node:assert/strict";
import { test } from "node:test";

import { waitForStory } from "./storybook.mjs";

// Stands in for a Playwright page where only the selectors in `shown` are on screen.
const pageShowing = (shown, errorText = "") => {
  const locator = (selectors) => ({
    first: () => locator(selectors),
    or: (other) => locator([...selectors, ...other.selectors]),
    selectors,
    waitFor: async () => {
      if (!selectors.some((selector) => shown.includes(selector))) throw new Error("timeout");
    },
    isVisible: async () => selectors.some((selector) => shown.includes(selector)),
    textContent: async () => errorText,
  });
  return { locator: (selector) => locator([selector]) };
};

test("a rendered story is ready", async () => {
  await waitForStory(pageShowing(["#storybook-root > :not(template)"]), 1000);
});

test("a rendered docs page is ready", async () => {
  await waitForStory(pageShowing(["#storybook-docs > :not(template)"]), 1000);
});

test("a story missing from this build fails at once with Storybook's error", async () => {
  const page = pageShowing([".sb-show-errordisplay #error-message"], "Couldn't find story matching 'x'.");

  await assert.rejects(waitForStory(page, 1000), /Couldn't find story matching 'x'/);
});
