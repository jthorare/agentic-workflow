import { existsSync, readdirSync } from "node:fs";
import { createRequire } from "node:module";
import { homedir } from "node:os";
import { join } from "node:path";

import { rowLayout } from "./layout.mjs";

const WEB_APP = process.env.WEB_APP_ROOT ?? join(homedir(), "work/web-app");
const sharp = createRequire(join(WEB_APP, "package.json"))("sharp");

const STATE_FILE = /^(?!ready-timeout)(.+?)\.(png|missing\.txt)$/;
const EMPTY_PANEL = { width: 300, height: 80 };

/** Throws when a side never reached its screen, so a failed run can't pass as "not on this build". */
export const assertBothSidesRan = (dir) => {
  for (const side of ["main", "pr"]) {
    const sideDir = join(dir, side);
    if (existsSync(join(sideDir, "ready-timeout.png"))) {
      throw new Error(`${side} never reached its screen; see ${sideDir}/ready-timeout.png`);
    }
    if (!existsSync(sideDir) || !readdirSync(sideDir).some((file) => STATE_FILE.test(file))) {
      throw new Error(`${side} has no output; run run-scenario.mjs for it first`);
    }
  }
};

const SHA = /^[0-9a-f]{7,40}$/;

/** Every state either side recorded, as a crop or a missing note. */
export const stateNames = (dir) => {
  const names = new Set();
  for (const side of ["main", "pr"]) {
    const sideDir = join(dir, side);
    if (!existsSync(sideDir)) continue;
    for (const file of readdirSync(sideDir)) {
      const match = file.match(STATE_FILE);
      if (match) names.add(match[1]);
    }
  }
  return [...names].sort();
};

const loadCrop = async (dir, side, state) => {
  const path = join(dir, side, `${state}.png`);
  if (!existsSync(path)) return null;
  const { width, height } = await sharp(path).metadata();
  return { input: path, width, height };
};

const textSvg = (width, height, text, background) =>
  Buffer.from(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">` +
      `<rect width="100%" height="100%" fill="${background}"/>` +
      `<text x="8" y="${Math.round(height / 2) + 5}" font-family="Helvetica, Arial, sans-serif" font-size="14" fill="#374151">${text}</text>` +
      `</svg>`,
  );

const missingPanel = ({ width, height }) => ({
  input: textSvg(width, height, "not on this build", "#e5e7eb"),
  width,
  height,
});

const FIGMA_LABELS = ["components", "project"];

/** The Figma frames saved for a state as <dir>/figma/<state>.<components|project>.png, in that order. */
export const figmaPanels = (dir, state) =>
  FIGMA_LABELS.map((label) => ({ label, path: join(dir, "figma", `${state}.${label}.png`) })).filter(({ path }) =>
    existsSync(path),
  );

const loadFigma = async ({ label, path }, width) => {
  const { data, info } = await sharp(path).resize({ width }).png().toBuffer({ resolveWithObject: true });
  return { input: data, width: info.width, height: info.height, label: `Figma ${label}` };
};

/** Writes <dir>/<state>.png: main, PR, then any saved Figma frames, each under its label. */
export const composeState = async (dir, state, mainSha, prSha) => {
  if (!SHA.test(mainSha) || !SHA.test(prSha)) throw new Error(`labels need hex shas, got ${mainSha} / ${prSha}`);
  const mainCrop = await loadCrop(dir, "main", state);
  const prCrop = await loadCrop(dir, "pr", state);
  const main = { ...(mainCrop ?? missingPanel(prCrop ?? EMPTY_PANEL)), label: `main @${mainSha}` };
  const pr = { ...(prCrop ?? missingPanel(mainCrop ?? EMPTY_PANEL)), label: `PR @${prSha}` };
  const figma = await Promise.all(figmaPanels(dir, state).map((panel) => loadFigma(panel, pr.width)));
  const panels = [main, pr, ...figma];
  const layout = rowLayout(panels);
  const out = join(dir, `${state}.png`);
  await sharp({ create: { width: layout.width, height: layout.height, channels: 4, background: "#ffffff" } })
    .composite(
      panels.flatMap((panel, i) => [
        { input: panel.input, ...layout.at[i] },
        { input: textSvg(panel.width, layout.labelHeight, panel.label, "#f3f4f6"), left: layout.at[i].left, top: 0 },
      ]),
    )
    .png()
    .toFile(out);
  return out;
};

const rawPixels = (path) => sharp(path).ensureAlpha().raw().toBuffer({ resolveWithObject: true });

/** Compares a state's main and PR crops pixel by pixel, for "unchanged" claims. */
export const pixelDiff = async (dir, state) => {
  const mainPath = join(dir, "main", `${state}.png`);
  const prPath = join(dir, "pr", `${state}.png`);
  if (!existsSync(mainPath)) return existsSync(prPath) ? "only on PR" : "on neither build";
  if (!existsSync(prPath)) return "only on main";
  const main = await rawPixels(mainPath);
  const pr = await rawPixels(prPath);
  if (main.info.width !== pr.info.width || main.info.height !== pr.info.height) {
    return `size differs: main ${main.info.width}x${main.info.height}, PR ${pr.info.width}x${pr.info.height}`;
  }
  let differing = 0;
  for (let offset = 0; offset < main.data.length; offset += 4) {
    if (main.data.compare(pr.data, offset, offset + 4, offset, offset + 4) !== 0) differing++;
  }
  return differing === 0 ? "identical" : `${differing} px differ`;
};
