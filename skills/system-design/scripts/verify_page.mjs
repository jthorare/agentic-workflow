#!/usr/bin/env node
// Render a page at 1280 and 390 px; fail on broken images, horizontal scroll or (a deck) a slide whose content overflows; write
// full-page screenshots.
// Usage: node verify_page.mjs <page.html> <out-dir> [--playwright-root <dir containing node_modules/playwright>]
// Open every screenshot afterwards. A passing metric is not a review.
import { createRequire } from "node:module";
import path from "node:path";
import fs from "node:fs";

const args = process.argv.slice(2);
const ri = args.indexOf("--playwright-root");
const root = ri > -1 ? args.splice(ri, 2)[1] : process.cwd();
const [page, outDir] = args;
if (!page || !outDir) { console.error("usage: verify_page.mjs <page.html> <out-dir> [--playwright-root dir]"); process.exit(2); }
const { chromium } = createRequire(path.join(path.resolve(root), "x.js"))("playwright");
fs.mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch();
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
  // A deck (build_share.py's deck.html): every 1280x720 slide must hold its content; overflow is cut off when presenting.
  const overflow = await p.evaluate(() => [...document.querySelectorAll(".stagebox .slide")].map((s, i) => {
    const was = s.classList.contains("on"); s.classList.add("on");
    const over = s.scrollHeight > s.clientHeight + 1 || s.scrollWidth > s.clientWidth + 1;
    if (!was) s.classList.remove("on");
    return over ? i + 1 : 0;
  }).filter(Boolean));
  if (r.broken.length || hscroll || overflow.length) failed = true;
  console.log(label, width, "images", r.images, "broken", r.broken.length, r.broken, "hscroll", hscroll, `(${r.scrollW}/${r.clientW})`,
    ...(overflow.length ? ["overflowing slides", overflow] : []), shot);
}
await browser.close();
process.exit(failed ? 1 : 0);
