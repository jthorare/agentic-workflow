#!/usr/bin/env node
// Render a page at 1280 and 390 px; fail on broken images or horizontal scroll; write full-page screenshots.
// Usage: node verify_page.mjs <page.html> <out-dir> [--playwright-root <dir containing node_modules/playwright[-core]>]
// Playwright is $PLAYWRIGHT_CORE (a playwright-core package dir) if set, else looked up in --playwright-root, else
// $SYSTEM_DESIGN_DEPS (default ~/.local/share/system-design-skills, shared with system-design-video; env.sh exports it
// as $DESIGN_DEPS), else the working directory; `playwright` or `playwright-core` both work.
// Open every screenshot afterwards. A passing metric is not a review.
import { createRequire } from "node:module";
import path from "node:path";
import fs from "node:fs";

const args = process.argv.slice(2);
const ri = args.indexOf("--playwright-root");
const deps = process.env.SYSTEM_DESIGN_DEPS ?? path.join(process.env.HOME ?? "", ".local/share/system-design-skills");
const roots = ri > -1 ? [args.splice(ri, 2)[1]] : [deps, process.cwd()];
const [page, outDir] = args;
if (!page || !outDir) { console.error("usage: verify_page.mjs <page.html> <out-dir> [--playwright-root dir]"); process.exit(2); }
let chromium, lastError;
const candidates = [
  ...(process.env.PLAYWRIGHT_CORE ? [[process.cwd(), path.resolve(process.env.PLAYWRIGHT_CORE)]] : []),
  ...roots.flatMap((root) => [[root, "playwright"], [root, "playwright-core"]]),
];
for (const [root, pkg] of candidates) {
  try { ({ chromium } = createRequire(path.join(path.resolve(root), "x.js"))(pkg)); break; }
  catch (error) { if (error.code !== "MODULE_NOT_FOUND") lastError = error; }
}
if (!chromium) {
  console.error(`no usable playwright or playwright-core (PLAYWRIGHT_CORE, ${roots.join(", ")}); run scripts/doctor.sh`);
  if (lastError) console.error(String(lastError));
  process.exit(2);
}
fs.mkdirSync(outDir, { recursive: true });

let browser;
try { browser = await chromium.launch(); }
catch (error) { console.error(`Chromium did not launch (run scripts/doctor.sh): ${String(error).split("\n")[0]}`); process.exit(2); }
let failed = false;
for (const [label, width] of [["desktop", 1280], ["phone", 390]]) {
  const p = await browser.newPage({ viewport: { width, height: 900 } });
  await p.goto("file://" + path.resolve(page));
  await p.waitForLoadState("load");
  const r = await p.evaluate(() => ({
    images: document.images.length,
    broken: [...document.images].filter((i) => !i.complete || i.naturalWidth === 0).map((i) => i.alt || i.src.slice(0, 60)),
    scrollW: document.documentElement.scrollWidth,
    clientW: document.documentElement.clientWidth,
  }));
  const shot = path.join(outDir, `${path.basename(page, ".html")}-${label}.png`);
  await p.screenshot({ path: shot, fullPage: true });
  const hscroll = r.scrollW > r.clientW + 1;
  if (r.broken.length || hscroll) failed = true;
  console.log(label, width, "images", r.images, "broken", r.broken.length, r.broken, "hscroll", hscroll, `(${r.scrollW}/${r.clientW})`, shot);
}
await browser.close();
process.exit(failed ? 1 : 0);
