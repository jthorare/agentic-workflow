// Render a whiteboard storyboard into silent footage, frame by frame.
//
// Exit: 0 done, 1 bad arguments; a bad storyboard, token or missing playwright-core throws (non-zero).
//   bun render.ts <storyboard.json> <out-dir> --tokens <design-tokens.json>
//                 [--fps 30] [--still t1,t2,...] [--range a..b]
//
// The storyboard's `inks` map each ink name to a design-token path (for example
// "color.brand.primary") or a literal hex; --tokens is the project's
// design-tokens.json (W3C DTCG). Colors come from the project; a token it lacks falls back to a neutral
// default for the standard ink names (board, chalk, primary, secondary, tertiary, warning, danger) and is reported.
//
// Writes <out-dir>/footage.mp4 (board.html drawn by headless Chromium at each frame
// time), <out-dir>/layout.json (each beat's start, land and end seconds, for review)
// and <out-dir>/resolved-inks.json (the hex each ink resolved to).
// --still renders PNGs only (<out-dir>/stills/t<sec>.png) and skips the encode.
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const flag = (name: string) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};
const positional = args.filter((a, i) => !a.startsWith("--") && !args[i - 1]?.startsWith("--"));
const [storyPath, outArg] = positional;
if (!storyPath || !outArg) {
  console.error("usage: bun render.ts <storyboard.json> <out-dir> --tokens <design-tokens.json> [--fps 30] [--still t1,t2] [--range a..b]");
  process.exit(1);
}
const out = resolve(outArg);
mkdirSync(out, { recursive: true });
const fps = Number(flag("--fps") ?? 30);
const story = JSON.parse(readFileSync(resolve(storyPath), "utf8"));

// ---- inks: token path or literal hex -> hex --------------------------------
const tokensPath = flag("--tokens");
let tokens: any;
if (tokensPath) {
  try {
    tokens = JSON.parse(readFileSync(resolve(tokensPath), "utf8"));
  } catch (error) {
    console.warn(`cannot read tokens ${tokensPath}: ${(error as Error).message}`);
  }
}

const toHex = (value: unknown): string | undefined => {
  if (typeof value === "string" && /^#[0-9a-f]{6}$/i.test(value)) return value;
  if (value && typeof value === "object" && typeof (value as { hex?: unknown }).hex === "string") return toHex((value as { hex: string }).hex);
  return undefined;
};

/** Walk a dotted path through a DTCG tree; follow `{a.b}` aliases. */
const tokenHex = (path: string, depth = 0): string => {
  if (depth > 8) throw new Error(`token alias loop at ${path}`);
  let node: any = tokens;
  for (const part of path.split(".")) node = node?.[part];
  const value = node?.$value ?? node?.value;
  if (value === undefined) throw new Error(`token not found: ${path} (in ${tokensPath})`);
  const alias = typeof value === "string" ? value.match(/^\{(.+)\}$/) : null;
  if (alias) return tokenHex(alias[1], depth + 1);
  const hex = toHex(value);
  if (!hex) throw new Error(`token ${path} is not a #rrggbb color (got ${JSON.stringify(value)})`);
  return hex;
};

// Neutral fallbacks for inks the project's tokens cannot supply (same idea as a stylesheet's neutral defaults):
// used only when the token is missing, and always reported so the run can ask the user or run /design-language.
const NEUTRAL: Record<string, string> = {
  board: "#22252B", chalk: "#F2EFE6", primary: "#4C8DFF", secondary: "#8A7BE8", tertiary: "#34A27A", warning: "#D9A441", danger: "#E5645F",
};
const fellBack: string[] = [];
const resolveInk = (name: string, ink: string): string => {
  if (ink.startsWith("#")) {
    const hex = toHex(ink);
    if (!hex) throw new Error(`bad hex ink ${ink}`);
    return hex;
  }
  try {
    if (!tokens) throw new Error("no --tokens file");
    return tokenHex(ink);
  } catch (error) {
    const fallback = NEUTRAL[name];
    if (!fallback) throw error;
    fellBack.push(`${name} (${ink}: ${(error as Error).message})`);
    return fallback;
  }
};

const inks: Record<string, string> = story.inks ?? {};
for (const need of ["board", "chalk"]) {
  if (!inks[need]) throw new Error(`storyboard.inks needs "${need}" (the board surface and the neutral ink)`);
}
const resolved: Record<string, string> = {};
for (const [name, ink] of Object.entries(inks)) resolved[name] = resolveInk(name, ink);
if (fellBack.length) console.warn(`neutral fallback used for: ${fellBack.join("; ")}. Ask the user for the token, or run /design-language.`);
story.colors = resolved;
story.board = resolved.board;
writeFileSync(join(out, "resolved-inks.json"), JSON.stringify(resolved, null, 2));

// ---- browser ---------------------------------------------------------------
// playwright-core: $PLAYWRIGHT_CORE, the toolkit's own install under $AW_STATE_DIR, or the working directory's.
const candidates = [
  process.env.PLAYWRIGHT_CORE,
  join(process.env.AW_STATE_DIR ?? join(homedir(), ".agentic-workflow"), "system-design-video/node_modules/playwright-core"),
  join(process.cwd(), "node_modules/playwright-core"),
].filter((p): p is string => !!p);
const core = candidates.find((p) => existsSync(join(p, "package.json")));
if (!core) {
  throw new Error("playwright-core not found; run scripts/doctor.sh (it prints the install command), or set PLAYWRIGHT_CORE");
}
const playwright = await import(pathToFileURL(join(core, "index.js")).href);
const chromium = (playwright.default ?? playwright).chromium;

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
await page.addInitScript((s: unknown) => { (window as any).STORY = s; }, story);
await page.goto(pathToFileURL(join(here, "board.html")).href);
await page.evaluate(() => document.fonts.ready);
const layout = await page.evaluate(() => (window as any).LAYOUT);
writeFileSync(join(out, "layout.json"), JSON.stringify(layout, null, 2));

const shot = async (t: number) => {
  await page.evaluate((x: number) => (window as any).setT(x), t);
  return page.screenshot({ type: "png" });
};

const stills = flag("--still");
if (stills) {
  mkdirSync(join(out, "stills"), { recursive: true });
  for (const t of stills.split(",").map(Number)) {
    writeFileSync(join(out, "stills", `t${t.toFixed(1)}.png`), await shot(t));
  }
  await browser.close();
  console.log(`stills in ${join(out, "stills")}; total ${layout.total.toFixed(2)}s`);
  process.exit(0);
}

const tail = 0.5; // a beat of rest after the last frame of drawing
const [from, to] = (flag("--range") ?? `0..${layout.total + tail}`).split("..").map(Number);
const ff = spawn("ffmpeg", ["-v", "error", "-y", "-f", "image2pipe", "-framerate", String(fps), "-c:v", "png", "-i", "-",
  "-vf", "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p", "-c:v", "libx264", "-crf", "12", "-preset", "slow", "-profile:v", "high", "-level", "4.1", "-bf", "0", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
  "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv", join(out, "footage.mp4")], { stdio: ["pipe", "inherit", "inherit"] });
const done = new Promise((r) => ff.on("close", r));
const frames = Math.round((to - from) * fps);
for (let i = 0; i < frames; i++) {
  const buf = await shot(from + i / fps);
  if (!ff.stdin.write(buf)) await new Promise((r) => ff.stdin.once("drain", r));
}
ff.stdin.end();
await done;
await browser.close();
console.log(`footage.mp4: ${frames} frames @${fps}fps, ${(frames / fps).toFixed(2)}s; beats: ${layout.beats.length}`);
