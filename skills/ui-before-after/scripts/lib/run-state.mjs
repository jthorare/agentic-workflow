import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

import { paddedClip } from "./crop.mjs";

// A build's first page load after `start` is still compiling chunks; 10s missed menus then.
const CROP_TIMEOUT_MS = 30_000;

// The page shot goes in missing/ because compose reads every top-level .png as a state.
const recordMissing = async (page, sideDir, state, step, reason) => {
  writeFileSync(join(sideDir, `${state.name}.missing.txt`), `${state.claim}\n${step}: ${reason}\n`);
  mkdirSync(join(sideDir, "missing"), { recursive: true });
  await page.screenshot({ path: join(sideDir, "missing", `${state.name}.png`) }).catch(() => {});
};

/** The first line of the error the promise rejects with, or undefined when it resolves. */
const failure = (promise) =>
  promise.then(
    () => undefined,
    (error) => String(error?.message ?? error).split("\n")[0],
  );

const axeViolations = async (page, target, axeSource) => {
  await page.addScriptTag({ content: axeSource });
  return target.evaluate(async (element) => {
    // Storybook's a11y addon runs axe in the same page, and axe refuses a second run until it ends.
    for (let attempt = 1; ; attempt++) {
      try {
        const { violations } = await window.axe.run(element);
        return violations.map((violation) => ({
          id: violation.id,
          impact: violation.impact,
          nodes: violation.nodes.length,
          targets: violation.nodes.slice(0, 3).map((node) => node.target.join(" ")),
        }));
      } catch (error) {
        if (attempt === 40 || !String(error).includes("already running")) throw error;
        await new Promise((resolve) => setTimeout(resolve, 250));
      }
    }
  });
};

/**
 * Runs one scenario state on one build: its own route (and ready target) if it has one, its clicks, then its crop,
 * optional aria text, axe violations when `axeSource` is given, and the Figma link `figmaLink` finds.
 */
export const runState = async (page, state, sideDir, viewport, { open, axeSource, figmaLink } = {}) => {
  const routeFailure = state.route && (await failure(open(state.route, state.ready)));
  if (routeFailure) {
    await recordMissing(page, sideDir, state, "route", routeFailure);
    return;
  }
  const actFailure = state.act && (await failure(state.act(page, { sideDir })));
  if (actFailure) {
    await recordMissing(page, sideDir, state, "act", actFailure);
    // Close whatever the failed clicks left open, so the next state starts clean.
    await page.keyboard.press("Escape");
    return;
  }
  // An `.or()` locator can match both the old and the new control on one build.
  const target = state.crop(page).first();
  const cropFailure = await failure(target.waitFor({ state: "visible", timeout: CROP_TIMEOUT_MS }));
  if (cropFailure) {
    await recordMissing(page, sideDir, state, "crop", cropFailure);
    return;
  }
  const box = await target.boundingBox();
  if (box && (box.x < 0 || box.y < 0 || box.x + box.width > viewport.width || box.y + box.height > viewport.height)) {
    const where = `box x=${box.x} y=${box.y} ${box.width}x${box.height}, screen ${viewport.width}x${viewport.height}`;
    await recordMissing(page, sideDir, state, "crop", `cut off at the screen edge (${where})`);
    return;
  }
  const clip = box && paddedClip(box, viewport);
  if (!clip || clip.width <= 0 || clip.height <= 0) {
    await recordMissing(page, sideDir, state, "crop", "no box on screen");
    return;
  }
  await page.screenshot({
    path: join(sideDir, `${state.name}.png`),
    clip,
    // Without this, a menu that just opened is caught mid fade-in.
    animations: "disabled",
  });
  if (state.aria) {
    writeFileSync(join(sideDir, `${state.name}.aria.txt`), `${await target.ariaSnapshot()}\n`);
  }
  if (axeSource) {
    const violations = await axeViolations(page, target, axeSource);
    writeFileSync(join(sideDir, `${state.name}.axe.json`), `${JSON.stringify(violations, null, 2)}\n`);
  }
  const link = figmaLink && (await figmaLink(page));
  if (link) writeFileSync(join(sideDir, `${state.name}.figma.txt`), `${link}\n`);
};
