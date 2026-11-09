// Screenshots and interaction checks for the schema drawer harness page (harness/render.tsx writes it).
//
// Usage: node harness/shoot.mjs <page.html> <png dir> <table> [<table> ...] [--widths 1440,390] [--schemes light,dark]
//          [--prefix schema-drawer] [--tabs preview,columns,details] [--chat "question"]
// For each table (its name, e.g. hold_ranking, or its schema id), width and colour scheme: click the diagram
// hotspot that carries the table (or emit the bus event when no box does), wait for the open drawer, then for each tab
// grow the viewport to the drawer's full height and write <png dir>/<prefix>-<table>-<width>-<scheme>-<tab>.png. With
// --chat (needs a page rendered with --stub-claude) it also asks the docked chat the question and writes
// <prefix>-chat-<table>-<width>-<scheme>.png once the stub's streamed reply is done. Also writes
// <prefix>-hotspots-<width>-<scheme>.png (the diagram with its table cards or "rows" badges, drawer closed) for the
// first width, and
// prints the checks as JSON: opened by click, tabs, sticky typed header, stats row, sort, filter, expand a long value,
// Close focused on open, bands or a sideways-scrolling grid on phones, chat dock, Esc
// closes, backdrop click closes, #tbl- hash opens, drawer width, no horizontal page overflow, hydration and console
// errors. playwright-core comes from ~/.local/share/system-design-skills/node_modules. Exit 0 when every check passes.
import { createRequire } from "node:module";
import { mkdirSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const require = createRequire(process.env.HOME + "/.local/share/system-design-skills/");
const { chromium } = require("playwright-core");

const argv = process.argv.slice(2);
const opt = (name, dflt) => { const i = argv.indexOf(name); if (i < 0) return dflt; const v = argv[i + 1]; argv.splice(i, 2); return v; };
const widths = opt("--widths", "1440,390").split(",").map(Number);
const schemes = opt("--schemes", "light,dark").split(",");
const prefix = opt("--prefix", "schema-drawer");
const tabs = opt("--tabs", "preview").split(",").filter(Boolean);
const question = opt("--chat", "");
const [pageArg, outArg, ...tables] = argv;
if (!pageArg || !outArg || !tables.length) { console.error("usage: node harness/shoot.mjs <page.html> <png dir> <table> [...]"); process.exit(1); }
const url = pathToFileURL(resolve(pageArg)).href;
const outDir = resolve(outArg);
mkdirSync(outDir, { recursive: true });

const HEIGHT = { 1440: 900, 390: 844 };
const results = { shots: [], checks: [] };
let failed = 0;
const check = (name, ok, detail = "") => { results.checks.push({ name, ok, detail }); if (!ok) failed++; };

/** Height the open drawer needs to show everything: fixed parts plus the scrolling body's full content. */
const needed = (page) => page.evaluate(() => {
  const d = document.querySelector("dialog.drawer");
  return [...d.children].reduce((a, c) => a + (c.classList.contains("drawer-body") ? c.scrollHeight : c.offsetHeight), 0) + 2;
});
/** Grow the viewport until the drawer's content fits (the chat log's max-height follows the viewport, so twice). */
async function growToFit(page, width, height) {
  for (let i = 0; i < 3; i++) {
    const need = await needed(page);
    const cur = page.viewportSize().height;
    if (need <= cur && i) break;
    await page.setViewportSize({ width, height: Math.max(height, need) });
    await page.waitForTimeout(120);
  }
}

const browser = await chromium.launch();
try {
  for (const scheme of schemes) {
    for (const width of widths) {
      const height = HEIGHT[width] || 900;
      const ctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1, colorScheme: scheme });
      const page = await ctx.newPage();
      const consoleErrors = [];
      page.on("console", (m) => { if (m.type() === "error") consoleErrors.push(m.text()); });
      page.on("pageerror", (e) => consoleErrors.push(String(e)));
      await page.goto(url, { waitUntil: "load" });
      await page.waitForFunction(() => window.__harness?.ready === true);
      await page.evaluate(() => document.fonts?.ready);
      const tag = `${width}-${scheme}`;

      if (width === widths[0]) {
        const fig = page.locator("figure.diagram").first();
        if (await fig.count()) {
          const p = join(outDir, `${prefix}-hotspots-${tag}.png`);
          await fig.screenshot({ path: p });
          results.shots.push(p);
          const badges = await page.evaluate(() => [...document.querySelectorAll(".hs.has-table")].filter((a) => a.classList.contains("tnode") || getComputedStyle(a, "::after").content.includes("rows")).length);
          check(`table boxes marked: cards or "rows" badges (${tag})`, badges > 0, `${badges} boxes`);
        }
      }

      for (const name of tables) {
        const id = await page.evaluate((n) => {
          const c = JSON.parse(document.getElementById("tdd-data").textContent);
          return (c.schema.find((t) => t.table === n || t.id === n) || {}).id || null;
        }, name);
        if (!id) { check(`table ${name} exists`, false); continue; }
        const hs = page.locator(`[data-hs][data-table="${id}"]`).first();
        const viaClick = (await hs.count()) > 0;
        const openIt = async () => {
          if (viaClick) await hs.click();
          else await page.evaluate((i) => window.__harness.emit(i), id);
          await page.waitForSelector("dialog.drawer[open]");
          await page.waitForTimeout(350);
        };
        const isOpen = () => page.evaluate(() => !!document.querySelector("dialog.drawer")?.open);

        await openIt();
        const title = await page.textContent("#schema-drawer-title code");
        check(`${name} opens by ${viaClick ? "hotspot click" : "bus event"} (${tag})`, title === name, `title ${title}`);
        const dw = await page.evaluate(() => Math.round(document.querySelector("dialog.drawer").getBoundingClientRect().width));
        check(`${name} drawer width (${tag})`, width >= 760 ? dw === 720 : dw === width, `${dw}px`);
        const over = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
        check(`${name} no horizontal page overflow (${tag})`, over <= 0, `${over}px`);
        // the Details brief's measures table (D28): absent cells read as words or a dash, never a zero
        const nm = await page.evaluate(() => [...document.querySelectorAll("dialog.drawer .tb-measures .none")].map((e) => e.textContent));
        check(`${name} absent measures read as words (${tag})`, nm.length > 0 && nm.every((t) => ["not measured", "not estimated", "–", "none (new table)"].includes(t)), `${nm.length} absent`);
        const g = await page.evaluate(() => {
          const d = document.querySelector("dialog.drawer");
          const th = d.querySelector(".dg-grid tr.dg-head th:not(.rn)");
          const grid = d.querySelector(".dg-grid");
          return {
            tabs: [...d.querySelectorAll('.dg-tabs [role="tab"]')].map((b) => b.textContent),
            sticky: th ? getComputedStyle(th).position : "none",
            glyphs: d.querySelectorAll(".dg-grid .dg-head .dg-glyph").length,
            cols: d.querySelectorAll(".dg-grid .dg-head th:not(.rn)").length,
            stats: d.querySelectorAll(".dg-grid .dg-head .dg-sb").length,
            statsRow: d.querySelectorAll(".dg-grid .dg-hstats, .dg-actions .dg-btn[aria-pressed]").length,
            rows: d.querySelectorAll(".dg-grid tbody tr").length,
            cap: d.querySelector(".dg-preview .dg-cap b")?.textContent || "",
            gridScrolls: grid ? grid.scrollWidth > grid.clientWidth : false,
            dock: d.querySelector(".dchat")?.textContent || "",
            focus: document.activeElement?.textContent || "",
            bands: d.querySelectorAll(".dg-bands .dg-band").length,
          };
        });
        check(`${name} tabs (${tag})`, g.tabs.length === 3 && /^Data preview/.test(g.tabs[0]), g.tabs.join(" | "));
        check(`${name} sticky typed header (${tag})`, g.sticky === "sticky" && g.glyphs === g.cols && g.cols > 0, `${g.cols} columns, ${g.glyphs} glyphs, ${g.sticky}`);
        // D30: no stats row and no toggle; one badge per header; hover shows the popover, a click keeps it open as a
        // dropdown (aria-expanded), Esc closes it and leaves the drawer open
        const badge = page.locator("dialog.drawer .dg-grid .dg-head .dg-sb").first();
        await badge.hover();
        await page.waitForTimeout(350);
        const hov = await page.evaluate(() => document.querySelector("dialog.drawer .dg-spop.show")?.textContent || "");
        await badge.click();
        await page.waitForTimeout(200);
        const exp = await badge.getAttribute("aria-expanded");
        await page.keyboard.press("Escape");
        await page.waitForTimeout(200);
        const after = await page.evaluate(() => ({ pop: !!document.querySelector("dialog.drawer .dg-spop.show"), drawer: !!document.querySelector("dialog.drawer")?.open }));
        await page.mouse.move(2, 2);
        check(`${name} stats row from sample rows (${tag})`, g.statsRow === 0 && g.stats === g.cols && /% null/.test(hov) && /sample rows?, not the database/.test(hov) && exp === "true" && !after.pop && after.drawer,
          `badges ${g.stats} of ${g.cols} columns, no row; hover "${hov.slice(0, 60)}"; click expanded ${exp}; Esc closed it ${!after.pop}, drawer open ${after.drawer}`);
        check(`${name} row caption (${tag})`, /^\d+ sample rows? · /.test(g.cap), g.cap);
        check(`${name} chat dock present (${tag})`, /Ask about this table/.test(g.dock), g.dock.slice(0, 120));
        check(`${name} Close has focus on open (${tag})`, g.focus === "Close", g.focus);
        if (width < 760) check(`${name} grid is banded or scrolls sideways inside its box (${tag})`, g.bands > 1 || g.gridScrolls, `${g.bands} bands, scrolls ${g.gridScrolls}`);

        // interactions: sort, filter to nothing and back, expand a long value
        const firstH = page.locator(".dg-grid tr.dg-head th:not(.rn) .dg-h").first();
        await firstH.click();
        const sorted = await page.evaluate(() => document.querySelector(".dg-grid tr.dg-head th:not(.rn)")?.getAttribute("aria-sort"));
        await firstH.click(); await firstH.click();
        check(`${name} header click sorts (${tag})`, sorted === "ascending", String(sorted));
        await page.fill(".dg-filter input", "zz-no-such-value-zz");
        const empty = await page.locator(".dg-grid .dg-empty").count();
        await page.fill(".dg-filter input", "");
        const back = await page.evaluate(() => document.querySelectorAll(".dg-grid tbody tr").length);
        check(`${name} filter narrows and restores (${tag})`, empty === 1 && back === g.rows, `empty ${empty}, rows ${back}/${g.rows}`);
        const more = page.locator(".dg-grid .dg-more").first();
        if (await more.count()) {
          await more.click();
          const ex = await more.getAttribute("aria-expanded");
          await more.click();
          check(`${name} long value expands (${tag})`, ex === "true", String(ex));
        }

        await page.evaluate(() => { const gr = document.querySelector(".dg-grid"); if (gr) { gr.scrollLeft = 0; gr.scrollTop = 0; } });
        for (const tb of tabs) {
          await page.click(`#dg-tab-${tb}`);
          await page.waitForTimeout(120);
          const shown = await page.evaluate((k) => !document.getElementById(`dg-panel-${k}`)?.hidden, tb);
          check(`${name} tab ${tb} shows its panel (${tag})`, shown);
          await growToFit(page, width, height);
          const p = join(outDir, `${prefix}-${name}-${tag}-${tb}.png`);
          await page.screenshot({ path: p });
          results.shots.push(p);
          await page.setViewportSize({ width, height });
        }
        await page.click("#dg-tab-preview");

        if (question) {
          const ta = page.locator(".dchat-form textarea");
          if (!(await ta.count())) check(`${name} chat input (${tag})`, false, "no textarea: render the page with --stub-claude");
          else {
            await page.waitForFunction(() => !document.querySelector(".dchat-form textarea")?.disabled, null, { timeout: 5000 });
            await ta.fill(question);
            await page.click(".dchat-form .dchat-btn");
            await page.waitForSelector(".dchat-msg.assistant.done", { timeout: 15000 }).catch(() => {});
            const reply = await page.evaluate(() => {
              const m = document.querySelector(".dchat-msg.assistant");
              return { cls: m?.className || "", text: m?.querySelector(".dchat-text")?.textContent || "", steps: m?.querySelectorAll(".dchat-steps li").length || 0, src: document.querySelector(".dchat-src")?.textContent || "" };
            });
            check(`${name} chat streams a reply with tool steps (${tag})`, /done/.test(reply.cls) && /Harness stub/.test(reply.text) && reply.steps > 0, `${reply.steps} steps; ${reply.src.slice(0, 80)}`);
            await growToFit(page, width, height);
            const p = join(outDir, `${prefix}-chat-${name}-${tag}.png`);
            await page.screenshot({ path: p });
            results.shots.push(p);
            await page.setViewportSize({ width, height });
          }
        }

        await page.keyboard.press("Escape");
        await page.waitForTimeout(100);
        if (await isOpen()) { await page.keyboard.press("Escape"); await page.waitForTimeout(100); }
        check(`${name} Esc closes (${tag})`, !(await isOpen()));

        if (width >= 760) {
          await openIt();
          await page.mouse.click(40, Math.round(height / 2));
          await page.waitForTimeout(100);
          check(`${name} backdrop click closes (${tag})`, !(await isOpen()));
        }
      }

      // deep link: #tbl-… opens the drawer on load
      const first = await page.evaluate((n) => {
        const c = JSON.parse(document.getElementById("tdd-data").textContent);
        return (c.schema.find((t) => t.table === n || t.id === n) || {}).id;
      }, tables[0]);
      await page.goto(url + "#" + first, { waitUntil: "load" });
      await page.waitForFunction(() => window.__harness?.ready === true);
      await page.waitForTimeout(200);
      check(`hash #${first} opens (${tag})`, await page.evaluate(() => !!document.querySelector("dialog.drawer")?.open));

      const herr = await page.evaluate(() => window.__harness.errors.length);
      check(`hydration errors (${tag})`, herr === 0, `${herr}`);
      check(`console errors (${tag})`, consoleErrors.filter((e) => !/fonts\.(googleapis|gstatic)/.test(e)).length === 0, consoleErrors.slice(0, 3).join(" | "));
      await ctx.close();
    }
  }
} finally {
  await browser.close();
}
console.log(JSON.stringify({ passed: results.checks.length - failed, failed, ...results }, null, 1));
process.exit(failed ? 1 : 0);
