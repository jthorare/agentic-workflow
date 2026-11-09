#!/usr/bin/env node
// Rasterize a drawn diagram so it can be read as an image (the Read tool renders PNG, not SVG).
//
//   node svg_to_png.mjs <in.svg> <out.png>
//
// Uses the same playwright-core as verify_page.mjs ($PLAYWRIGHT_CORE, else
// $SYSTEM_DESIGN_DEPS/node_modules/playwright-core). Exit 0 written, 1 render error, 2 bad args or no Playwright.
import { writeFileSync, unlinkSync } from "node:fs";
import { resolve, join } from "node:path";
import { homedir } from "node:os";
import { pathToFileURL } from "node:url";

const [svgArg, outArg] = process.argv.slice(2);
if (!svgArg || !outArg) { console.error("usage: svg_to_png.mjs <in.svg> <out.png>"); process.exit(2); }
const svgPath = resolve(svgArg), outPath = resolve(outArg);

const deps = process.env.SYSTEM_DESIGN_DEPS ?? join(homedir(), ".local/share/system-design-skills");
const core = process.env.PLAYWRIGHT_CORE ?? join(deps, "node_modules/playwright-core");
let chromium;
try { ({ chromium } = await import(pathToFileURL(join(core, "index.mjs")).href)); }
catch (e) { console.error(`no playwright-core at ${core} (run scripts/doctor.sh): ${e.message}`); process.exit(2); }

const wrap = `${outPath}.wrap.html`;
writeFileSync(wrap, `<!doctype html><body style="margin:0;background:#fff"><img id="i" src="${pathToFileURL(svgPath).href}"></body>`);
let browser;
try {
  browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1000, height: 900 }, deviceScaleFactor: 2 });
  await page.goto(pathToFileURL(wrap).href);
  await page.waitForFunction(() => { const i = document.getElementById("i"); return i.complete && i.naturalWidth > 0; }, null, { timeout: 15000 });
  const { w, h } = await page.evaluate(() => { const i = document.getElementById("i"); return { w: Math.ceil(i.naturalWidth), h: Math.ceil(i.naturalHeight) }; });
  await page.setViewportSize({ width: w, height: h });
  await page.screenshot({ path: outPath, timeout: 15000 });
  console.log(`${outPath} ${w}x${h}@2x`);
} catch (e) {
  console.error(`render failed: ${e.message}`); process.exitCode = 1;
} finally {
  await browser?.close();
  try { unlinkSync(wrap); } catch {}
}
