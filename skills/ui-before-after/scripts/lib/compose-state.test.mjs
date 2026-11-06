import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { homedir, tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { assertBothSidesRan, composeState, figmaPanels, pixelDiff } from "./compose-state.mjs";

const sharp = createRequire(join(process.env.WEB_APP_ROOT ?? join(homedir(), "work/web-app"), "package.json"))("sharp");

const pixelAt = async (path, x, y) => {
  const { data, info } = await sharp(path).raw().toBuffer({ resolveWithObject: true });
  const offset = (y * info.width + x) * info.channels;
  return [data[offset], data[offset + 1], data[offset + 2]];
};

test("a state missing on main gets a grey panel there, not the PR's crop", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-compose-"));
  mkdirSync(join(dir, "pr"));
  await sharp({ create: { width: 50, height: 20, channels: 3, background: "#ff0000" } })
    .png()
    .toFile(join(dir, "pr", "state.png"));

  const out = await composeState(dir, "state", "aaaaaaa", "bbbbbbb");

  assert.deepEqual(await pixelAt(out, 2, 40), [0xe5, 0xe7, 0xeb]);
  assert.deepEqual(await pixelAt(out, 50 + 24 + 2, 40), [0xff, 0x00, 0x00]);
});

test("refuses to pair when one side never reached its screen", () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-compose-"));
  mkdirSync(join(dir, "main"));
  mkdirSync(join(dir, "pr"));
  writeFileSync(join(dir, "main", "ready-timeout.png"), "");
  writeFileSync(join(dir, "pr", "state.png"), "");

  assert.throws(() => assertBothSidesRan(dir), /main/);
});

test("refuses to pair when one side has no output at all", () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-compose-"));
  mkdirSync(join(dir, "main"));
  writeFileSync(join(dir, "main", "state.png"), "");

  assert.throws(() => assertBothSidesRan(dir), /pr/);
});

test("labels only take a hex sha", async () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-compose-"));

  await assert.rejects(composeState(dir, "state", "<script>", "bbbbbbb"), /sha/);
});

const solid = (path, width, height, background) =>
  sharp({ create: { width, height, channels: 3, background } }).png().toFile(path);

const twoSides = () => {
  const dir = mkdtempSync(join(tmpdir(), "ui-before-after-pixel-"));
  mkdirSync(join(dir, "main"));
  mkdirSync(join(dir, "pr"));
  return dir;
};

test("pixel compare calls same-size crops with equal pixels identical", async () => {
  const dir = twoSides();
  await solid(join(dir, "main", "story.png"), 40, 20, "#ffffff");
  await solid(join(dir, "pr", "story.png"), 40, 20, "#ffffff");

  assert.equal(await pixelDiff(dir, "story"), "identical");
});

test("pixel compare counts the pixels that differ", async () => {
  const dir = twoSides();
  await solid(join(dir, "main", "story.png"), 40, 20, "#ffffff");
  await sharp(join(dir, "main", "story.png"))
    .composite([{ input: { create: { width: 2, height: 3, channels: 3, background: "#000000" } }, left: 0, top: 0 }])
    .toFile(join(dir, "pr", "story.png"));

  assert.equal(await pixelDiff(dir, "story"), "6 px differ");
});

test("pixel compare reports crops of different sizes", async () => {
  const dir = twoSides();
  await solid(join(dir, "main", "story.png"), 40, 20, "#ffffff");
  await solid(join(dir, "pr", "story.png"), 41, 20, "#ffffff");

  assert.equal(await pixelDiff(dir, "story"), "size differs: main 40x20, PR 41x20");
});

test("pixel compare reports a state only one side has", async () => {
  const dir = twoSides();
  await solid(join(dir, "pr", "story.png"), 40, 20, "#ffffff");

  assert.equal(await pixelDiff(dir, "story"), "only on PR");
});

test("a saved Figma frame becomes a third panel, scaled to the PR crop's width", async () => {
  const dir = twoSides();
  mkdirSync(join(dir, "figma"));
  await solid(join(dir, "main", "field.png"), 50, 20, "#ff0000");
  await solid(join(dir, "pr", "field.png"), 50, 20, "#00ff00");
  await solid(join(dir, "figma", "field.components.png"), 100, 40, "#0000ff");

  const out = await composeState(dir, "field", "aaaaaaa", "bbbbbbb");

  const { width } = await sharp(out).metadata();
  assert.equal(width, 50 + 24 + 50 + 24 + 50);
  assert.deepEqual(await pixelAt(out, 50 + 24 + 50 + 24 + 2, 40), [0x00, 0x00, 0xff]);
});

test("figmaPanels lists a state's saved frames, components before project", () => {
  const dir = twoSides();
  mkdirSync(join(dir, "figma"));
  writeFileSync(join(dir, "figma", "field.project.png"), "");
  writeFileSync(join(dir, "figma", "field.components.png"), "");
  writeFileSync(join(dir, "figma", "fieldset.components.png"), "");

  assert.deepEqual(
    figmaPanels(dir, "field").map((panel) => panel.label),
    ["components", "project"],
  );
});
