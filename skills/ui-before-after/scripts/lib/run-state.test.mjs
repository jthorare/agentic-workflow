import assert from "node:assert/strict";
import { existsSync, mkdtempSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { runState } from "./run-state.mjs";

// Stands in for a Playwright page: runState only screenshots it and presses keys on it.
const pageThatRecordsShots = () => {
  const shots = [];
  const keys = [];
  return {
    shots,
    keys,
    screenshot: async ({ path }) => shots.push(path),
    keyboard: { press: async (key) => keys.push(key) },
  };
};

// Stands in for a Playwright locator that is visible with the given box.
const locatorWithBox = (box) => ({
  first: () => locatorWithBox(box),
  waitFor: async () => {},
  boundingBox: async () => box,
});

test("a crop target with no box on screen is recorded as missing, not a crash", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();

  await runState(page, { name: "offscreen", claim: "Off screen", crop: () => locatorWithBox(null) }, dir, {
    width: 1440,
    height: 900,
  });

  assert.equal(readFileSync(join(dir, "offscreen.missing.txt"), "utf8"), "Off screen\ncrop: no box on screen\n");
  assert.deepEqual(page.shots, [join(dir, "missing", "offscreen.png")]);
});

test("a crop target partly off screen is recorded as missing, not saved as a sliver", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();

  await runState(
    page,
    { name: "card", claim: "Add paycode card", crop: () => locatorWithBox({ x: -175, y: 300, width: 217, height: 68 }) },
    dir,
    { width: 1440, height: 900 },
  );

  assert.equal(
    readFileSync(join(dir, "card.missing.txt"), "utf8"),
    "Add paycode card\ncrop: cut off at the screen edge (box x=-175 y=300 217x68, screen 1440x900)\n",
  );
  assert.deepEqual(page.shots, [join(dir, "missing", "card.png")]);
});

test("a crop matching two elements on a build uses the first", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const both = {
    first: () => locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }),
    waitFor: async () => {
      throw new Error("strict mode violation: resolved to 2 elements");
    },
  };

  await runState(page, { name: "field", claim: "Field", crop: () => both }, dir, { width: 1440, height: 900 });

  assert.deepEqual(page.shots, [join(dir, "field.png")]);
});

test("a failed click presses Escape so the next state starts with menus closed", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const state = {
    name: "menu-open",
    claim: "Menu open",
    act: async () => {
      throw new Error("button not on this build");
    },
    crop: () => locatorWithBox(null),
  };

  await runState(page, state, dir, { width: 1440, height: 900 });

  assert.deepEqual(page.keys, ["Escape"]);
});

test("a state whose clicks fail on this build is recorded as missing, and the run goes on", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const state = {
    name: "menu-open",
    claim: "Staff search menu open",
    act: async () => {
      throw new Error("button not on this build");
    },
    crop: () => {
      throw new Error("crop should not run after a failed act");
    },
  };

  await runState(page, state, dir, { width: 1440, height: 900 });

  assert.equal(
    readFileSync(join(dir, "menu-open.missing.txt"), "utf8"),
    "Staff search menu open\nact: button not on this build\n",
  );
  assert.deepEqual(page.shots, [join(dir, "missing", "menu-open.png")]);
  assert.equal(existsSync(join(dir, "menu-open.png")), false);
});

test("a state with its own route opens it before its clicks", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const steps = [];
  const state = {
    name: "sm-multi",
    claim: "Small multi Select",
    route: "/iframe.html?id=components-select--sm-multi",
    act: async () => steps.push("act"),
    crop: () => locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }),
  };

  await runState(page, state, dir, { width: 1440, height: 900 }, { open: async (route) => steps.push(route) });

  assert.deepEqual(steps, ["/iframe.html?id=components-select--sm-multi", "act"]);
  assert.deepEqual(page.shots, [join(dir, "sm-multi.png")]);
});

test("a state's clicks get this build's output folder, so measurements land beside its crops", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  let given;
  const state = {
    name: "caret",
    claim: "Caret size",
    act: async (_page, context) => {
      given = context;
    },
    crop: () => locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }),
  };

  await runState(page, state, dir, { width: 1440, height: 900 });

  assert.deepEqual(given, { sideDir: dir });
});

test("a state's own ready target is used when its route opens", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const opened = [];
  const state = {
    name: "unit-settings",
    claim: "Unit settings",
    route: "/settings/unit",
    ready: { testId: "unit-settings" },
    crop: () => locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }),
  };

  await runState(page, state, dir, { width: 1440, height: 900 }, { open: async (...args) => opened.push(args) });

  assert.deepEqual(opened, [["/settings/unit", { testId: "unit-settings" }]]);
});

test("a state whose route never gets ready is recorded as missing", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const state = {
    name: "new-story",
    claim: "Story only on the PR",
    route: "/iframe.html?id=components-select--new",
    crop: () => locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }),
  };
  const open = async () => {
    throw new Error("ready target never showed");
  };

  await runState(page, state, dir, { width: 1440, height: 900 }, { open });

  assert.equal(
    readFileSync(join(dir, "new-story.missing.txt"), "utf8"),
    "Story only on the PR\nroute: ready target never showed\n",
  );
  assert.deepEqual(page.shots, [join(dir, "missing", "new-story.png")]);
});

test("with axe source, each state's crop target is checked and its violations saved", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = { ...pageThatRecordsShots(), addScriptTag: async ({ content }) => (page.injected = content) };
  const violations = [{ id: "button-name", impact: "critical", nodes: 1, targets: ["button"] }];
  const target = { ...locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }), evaluate: async () => violations };
  target.first = () => target;

  await runState(page, { name: "field", claim: "Field", crop: () => target }, dir, { width: 1440, height: 900 }, {
    axeSource: "window.axe = {}",
  });

  assert.equal(page.injected, "window.axe = {}");
  assert.deepEqual(JSON.parse(readFileSync(join(dir, "field.axe.json"), "utf8")), violations);
});

test("axe waits for a run already in progress, such as Storybook's a11y addon, instead of failing", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = { ...pageThatRecordsShots(), addScriptTag: async () => {} };
  const element = {};
  let calls = 0;
  globalThis.window = {
    axe: {
      run: async (target) => {
        calls += 1;
        if (calls === 1) throw new Error("Axe is already running. Use `await axe.run()` to wait for the previous run to finish");
        assert.equal(target, element);
        return { violations: [{ id: "button-name", impact: "critical", nodes: [{ target: ["button"] }] }] };
      },
    },
  };
  const target = { ...locatorWithBox({ x: 10, y: 10, width: 50, height: 20 }), evaluate: async (fn) => fn(element) };
  target.first = () => target;

  try {
    await runState(page, { name: "field", claim: "Field", crop: () => target }, dir, { width: 1440, height: 900 }, {
      axeSource: "window.axe = {}",
    });
  } finally {
    delete globalThis.window;
  }

  assert.equal(calls, 2);
  assert.deepEqual(JSON.parse(readFileSync(join(dir, "field.axe.json"), "utf8")), [
    { id: "button-name", impact: "critical", nodes: 1, targets: ["button"] },
  ]);
});

test("without axe source, no axe file is written", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();

  await runState(page, { name: "field", claim: "Field", crop: () => locatorWithBox({ x: 1, y: 1, width: 5, height: 5 }) }, dir, {
    width: 1440,
    height: 900,
  });

  assert.equal(existsSync(join(dir, "field.axe.json")), false);
});

test("a story's Figma link is saved next to its crop", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();
  const figmaLink = async () => "https://www.figma.com/design/PVmS7pZqcyyisBOM0OASW7/Components?node-id=698-9058";

  await runState(page, { name: "field", claim: "Field", crop: () => locatorWithBox({ x: 1, y: 1, width: 5, height: 5 }) }, dir, {
    width: 1440,
    height: 900,
  }, { figmaLink });

  assert.equal(
    readFileSync(join(dir, "field.figma.txt"), "utf8"),
    "https://www.figma.com/design/PVmS7pZqcyyisBOM0OASW7/Components?node-id=698-9058\n",
  );
});

test("a story without a Figma link writes no link file", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-state-"));
  const page = pageThatRecordsShots();

  await runState(page, { name: "field", claim: "Field", crop: () => locatorWithBox({ x: 1, y: 1, width: 5, height: 5 }) }, dir, {
    width: 1440,
    height: 900,
  }, { figmaLink: async () => undefined });

  assert.equal(existsSync(join(dir, "field.figma.txt")), false);
});
