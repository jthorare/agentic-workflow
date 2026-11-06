// Runs one ui-before-after scenario against one local build and saves its crops and, with --record, a video.
//
//   node run-scenario.mjs <scenario.mjs> --base <url> --side main|pr --out <dir> [--db <postgres-url>] [--storybook] [--axe] [--record]
//
// Writes <dir>/<side>/<state>.png, <state>.aria.txt (when the state asks), <state>.axe.json (with
// --axe), <state>.figma.txt (the story's Figma link, with --storybook), <state>.missing.txt (when
// its crop target never shows), and <dir>/<side>/<side>.webm (with --record).
// --storybook: <url> is a Storybook, so there is no sign-in and no app overlays. --db: the slot's
// database, where a scenario's `impersonate` profile is looked up. Exit 1 when auto sign-in or
// impersonation did not happen.

import { execFileSync } from "node:child_process";
import { mkdirSync, rmSync } from "node:fs";
import { createRequire } from "node:module";
import { homedir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

import { impersonate } from "./lib/impersonate.mjs";
import { closeOverlays } from "./lib/overlays.mjs";
import { runState } from "./lib/run-state.mjs";
import { waitForStory } from "./lib/storybook.mjs";

const WEB_APP = process.env.WEB_APP_ROOT ?? join(homedir(), "work/web-app");
const require = createRequire(join(WEB_APP, "package.json"));
const { chromium } = require("./node_modules/playwright/node_modules/playwright-core");

const VIEWPORT = { width: 1440, height: 900 };
const SIGN_IN_TIMEOUT_MS = 60_000;
const READY_TIMEOUT_MS = 120_000;

const parseArgs = (args) => {
  const options = { scenario: args[0] };
  for (let i = 1; i < args.length; i++) {
    if (args[i] === "--base") options.base = args[++i];
    else if (args[i] === "--side") options.side = args[++i];
    else if (args[i] === "--out") options.out = args[++i];
    else if (args[i] === "--db") options.db = args[++i];
    else if (args[i] === "--storybook") options.storybook = true;
    else if (args[i] === "--axe") options.axe = true;
    else if (args[i] === "--record") options.record = true;
  }
  if (!options.scenario || !options.base || !options.side || !options.out) {
    throw new Error(
      "usage: run-scenario.mjs <scenario.mjs> --base <url> --side main|pr --out <dir> [--db <postgres-url>] [--storybook] [--axe] [--record]",
    );
  }
  return options;
};

// Headless Chromium says "HeadlessChrome", which the app's Unsupported Web Browser page rejects.
const headedUserAgent = () => {
  const version = execFileSync(chromium.executablePath(), ["--version"]).toString();
  const major = version.match(/(\d+)\./)?.[1];
  return `Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${major}.0.0.0 Safari/537.36`;
};

const clearFrozenPageEntries = () => {
  for (const key of Object.keys(localStorage)) {
    if (key.startsWith("vitalize.frozen-page.v1.")) localStorage.removeItem(key);
  }
};

const waitForReady = async (page, ready = {}, storybook = false) => {
  if (storybook) await waitForStory(page, READY_TIMEOUT_MS);
  const target = ready.testId
    ? page.getByTestId(ready.testId)
    : ready.text
      ? page.getByText(ready.text, { exact: false }).first()
      : storybook
        ? null
        : page.getByTestId("sidebar");
  await target?.waitFor({ timeout: READY_TIMEOUT_MS });
};

// The story's own `parameters.design.url` from @storybook/addon-designs, if it has one.
const storyFigmaLink = (page) =>
  page.evaluate(() => window.__STORYBOOK_PREVIEW__?.currentRender?.story?.parameters?.design?.url).catch(() => undefined);

/** Signs in through the local stack's auto sign-in and forces the scenario's PostHog flags. */
const signIn = async (page, base, side, flags) => {
  await page.goto(new URL("/login", base).toString());
  const signedIn = await page
    .getByTestId("sidebar")
    .waitFor({ timeout: SIGN_IN_TIMEOUT_MS })
    .then(() => true)
    .catch(() => false);
  if (!signedIn) {
    console.error(`${side}: auto sign-in did not happen (still on ${new URL(page.url()).pathname})`);
    return false;
  }
  if (!flags) return true;
  if (!(await page.evaluate(() => Boolean(window.posthog?.featureFlags)))) {
    console.error(`${side}: PostHog is not loaded, so the scenario's flags cannot be set`);
    return false;
  }
  await page.evaluate((flags) => window.posthog.featureFlags.overrideFeatureFlags({ flags }), flags);
  return true;
};

const run = async ({ scenario: scenarioPath, base, side, out, db, storybook = false, axe = false, record = false }) => {
  const { default: scenario } = await import(pathToFileURL(resolve(scenarioPath)).href);
  const sideDir = join(resolve(out), side);
  rmSync(sideDir, { recursive: true, force: true });
  mkdirSync(sideDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    userAgent: headedUserAgent(),
    viewport: VIEWPORT,
    ...(record && { recordVideo: { dir: sideDir, size: VIEWPORT } }),
  });
  await context.addInitScript(clearFrozenPageEntries);
  const page = await context.newPage();
  const open = async (route, ready = scenario.ready) => {
    await page.goto(new URL(route, base).toString());
    await waitForReady(page, ready, storybook);
  };
  const options = {
    open,
    axeSource: axe ? require("axe-core").source : undefined,
    figmaLink: storybook ? storyFigmaLink : undefined,
  };
  try {
    if (storybook && scenario.flags) {
      console.error(`${side}: Storybook has no PostHog, so the scenario's flags cannot be set`);
      process.exitCode = 1;
      return;
    }
    if (!storybook && !(await signIn(page, base, side, scenario.flags))) {
      process.exitCode = 1;
      return;
    }
    if (scenario.impersonate) {
      const error = await impersonate(page, base, db, scenario.impersonate).catch((cause) => cause);
      if (error) {
        console.error(`${side}: ${error.message}`);
        process.exitCode = 1;
        return;
      }
    }
    const ready = await open(scenario.route)
      .then(() => true)
      .catch(() => false);
    if (!ready) {
      await page.screenshot({ path: join(sideDir, "ready-timeout.png") });
      console.error(`${side}: ${scenario.route} never showed its ready target; see ${sideDir}/ready-timeout.png`);
      process.exitCode = 1;
      return;
    }
    if (!storybook) await closeOverlays(page, { afterLoad: true });
    for (const state of scenario.states) {
      if (!storybook) await closeOverlays(page);
      await runState(page, state, sideDir, VIEWPORT, options);
    }
  } finally {
    const video = page.video();
    await page.close();
    await video?.saveAs(join(sideDir, `${side}.webm`));
    await video?.delete();
    await context.close();
    await browser.close();
  }
};

await run(parseArgs(process.argv.slice(2)));
