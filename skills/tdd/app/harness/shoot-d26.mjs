// D26 screenshots and checks against a built TDD page (build/tdd.html), not the harness page: the drawer's one-row
// head, the bands of a wide table, the Expand view, and the table cards on the schema diagram.
//
// Usage: node harness/shoot-d26.mjs <tdd.html> <png dir> [--widths 1440,390] [--schemes light,dark]
//          [--table <table>] [--wide <table>] [--diagram <figure id>] [--prefix d26]
// --table defaults to the first schema table, --wide to the table with the most columns, --diagram to the first
// figure.diagram holding a .tnode card.
// Writes <prefix>-drawer-<table>-<w>-<scheme>.png, <prefix>-bands-<wide>-<w>-<scheme>.png, <prefix>-modal-<wide>-<w>-
// <scheme>.png (wide viewports only: phones keep the banded drawer, no Expand), <prefix>-diagram-<w>-<scheme>.png and,
// at the first width, <prefix>-diagram-tip-<w>-<scheme>.png (a sizing tile's tooltip). Prints the checks as JSON; exit 0
// when all pass. playwright-core comes from ~/.local/share/system-design-skills/node_modules.
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
const prefix = opt("--prefix", "d26");
const tableArg = opt("--table", "");
const wideArg = opt("--wide", "");
const diagramArg = opt("--diagram", "");
const [pageArg, outArg] = argv;
if (!pageArg || !outArg) { console.error("usage: node harness/shoot-d26.mjs <tdd.html> <png dir> [options]"); process.exit(1); }
const url = pathToFileURL(resolve(pageArg)).href;
const outDir = resolve(outArg);
mkdirSync(outDir, { recursive: true });

const HEIGHT = { 1440: 900, 390: 844 };
const results = { shots: [], checks: [] };
let failed = 0;
const check = (name, ok, detail = "") => { results.checks.push({ name, ok: !!ok, detail }); if (!ok) failed++; };

const browser = await chromium.launch();
try {
  for (const scheme of schemes) {
    for (const width of widths) {
      const height = HEIGHT[width] || 900;
      const tag = `${width}-${scheme}`;
      const ctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 2, colorScheme: scheme, reducedMotion: "reduce" });
      const page = await ctx.newPage();
      const errors = [];
      page.on("console", (m) => { if (m.type() === "error" && !/fonts\.(googleapis|gstatic)/.test(m.text())) errors.push(m.text()); });
      page.on("pageerror", (e) => errors.push(String(e)));
      await page.goto(url, { waitUntil: "load" });
      await page.evaluate(async () => {
        document.querySelectorAll("img").forEach((i) => { i.loading = "eager"; });
        await Promise.all([...document.images].map((i) => (i.complete ? null : new Promise((r) => { i.onload = r; i.onerror = r; }))));
        await document.fonts?.ready;
      });
      const info = await page.evaluate(() => {
        const c = JSON.parse(document.getElementById("tdd-data").textContent);
        return { tables: c.schema.map((t) => ({ id: t.id, table: t.table, cols: t.columns.length })) };
      });
      const byName = (n) => info.tables.find((t) => t.table === n || t.id === n);
      const first = byName(tableArg) || info.tables[0];
      const wide = byName(wideArg) || [...info.tables].sort((a, b) => b.cols - a.cols)[0];

      const openTable = async (id) => {
        await page.evaluate((i) => { location.hash = ""; location.hash = i; }, id);
        await page.waitForSelector("dialog.drawer[open]");
        await page.waitForTimeout(400);
      };
      const closeAll = async () => {
        for (let i = 0; i < 3 && (await page.evaluate(() => !!document.querySelector("dialog.drawer")?.open)); i++) {
          await page.keyboard.press("Escape");
          await page.waitForTimeout(150);
        }
        await page.evaluate(() => history.replaceState(null, "", location.pathname));
      };
      const head = () => page.evaluate(() => {
        const d = document.querySelector("dialog.drawer");
        const h = d.querySelector(".drawer-head");
        const tops = (sel) => [...h.querySelectorAll(sel)].map((e) => Math.round(e.getBoundingClientRect().top + e.getBoundingClientRect().height / 2));
        const nameMid = tops("h2.drawer-name")[0], tabMids = tops(".dg-tab"), closeMid = tops(".drawer-close")[0];
        const body = d.querySelector(".drawer-body");
        const firstInBody = body.querySelector(".dg-panel:not([hidden]) > *");
        return {
          headH: Math.round(h.getBoundingClientRect().height), nameMid, tabMids, closeMid,
          oneRow: tabMids.every((m) => Math.abs(m - nameMid) <= 6) && Math.abs(closeMid - nameMid) <= 6,
          summaryInBody: !!body.querySelector(":scope > .drawer-summary") || (firstInBody && firstInBody.classList.contains("drawer-summary")),
          summaryInDetails: !!d.querySelector("#dg-panel-details .drawer-summary"),
          bands: d.querySelectorAll(".dg-bands .dg-band").length,
          expand: !!d.querySelector(".drawer-expand") && getComputedStyle(d.querySelector(".drawer-expand")).display !== "none",
          focus: document.activeElement?.className || "",
          width: Math.round(d.getBoundingClientRect().width), height: Math.round(d.getBoundingClientRect().height),
          overflow: document.documentElement.scrollWidth - window.innerWidth,
          bandFits: [...d.querySelectorAll(".dg-bands.multi .dg-grid")].every((g) => g.scrollWidth <= g.clientWidth + 1),
          rowsPerBand: [...d.querySelectorAll(".dg-bands .dg-grid")].map((g) => g.querySelectorAll("tbody tr").length),
          cols: [...d.querySelectorAll(".dg-bands .dg-grid")].map((g) => g.querySelectorAll("tr.dg-head th:not(.rn)").length),
        };
      });

      // 1. the drawer on the first table: one head row, no summary under the tabs
      await openTable(first.id);
      let h = await head();
      check(`drawer ${first.table}: name, tabs and Close in one row (${tag})`, width < 641 ? h.tabMids.length === 3 : h.oneRow, JSON.stringify({ name: h.nameMid, tabs: h.tabMids, close: h.closeMid, headH: h.headH }));
      check(`drawer ${first.table}: no summary under the tabs (${tag})`, !h.summaryInBody && h.summaryInDetails, `in body ${h.summaryInBody}, in Details ${h.summaryInDetails}`);
      check(`drawer ${first.table}: Close has focus on open (${tag})`, /drawer-close/.test(h.focus), h.focus);
      check(`drawer ${first.table}: width (${tag})`, width >= 760 ? h.width === 720 : h.width === width, `${h.width}px`);
      let p = join(outDir, `${prefix}-drawer-${first.table}-${tag}.png`);
      await page.screenshot({ path: p }); results.shots.push(p);
      await closeAll();

      // 2. the widest table: bands in the drawer
      await openTable(wide.id);
      h = await head();
      check(`bands ${wide.table}: ${wide.cols} columns cut into bands (${tag})`, h.bands > 1 && h.cols.reduce((a, b) => a + b, 0) === h.cols.reduce((a, b) => a + b, 0), `${h.bands} bands, columns per band ${h.cols.join("+")}`);
      check(`bands ${wide.table}: every band fits the drawer (${tag})`, h.bandFits, String(h.bandFits));
      check(`bands ${wide.table}: same rows on every band (${tag})`, h.rowsPerBand.every((n) => n === h.rowsPerBand[0]), h.rowsPerBand.join(","));
      check(`bands ${wide.table}: Expand ${width >= 641 ? "shown" : "hidden on phones"} (${tag})`, width >= 641 ? h.expand : !h.expand, String(h.expand));
      check(`bands ${wide.table}: no horizontal page overflow (${tag})`, h.overflow <= 0, `${h.overflow}px`);
      p = join(outDir, `${prefix}-bands-${wide.table}-${tag}.png`);
      await page.screenshot({ path: p }); results.shots.push(p);

      // 3. the Expand view
      if (h.expand) {
        await page.click(".drawer-expand");
        await page.waitForTimeout(400);
        const m = await page.evaluate(() => {
          const d = document.querySelector("dialog.drawer");
          const r = d.getBoundingClientRect();
          const g = d.querySelectorAll(".dg-bands .dg-grid");
          return { expanded: d.classList.contains("expanded"), w: r.width / innerWidth, h: r.height / innerHeight, grids: g.length,
            scrollsX: g[0] ? g[0].scrollWidth > g[0].clientWidth : false, chat: !!d.querySelector(".dchat"), tabs: d.querySelectorAll(".dg-tab").length,
            focus: document.activeElement?.getAttribute("aria-label") || document.activeElement?.className || "" };
        });
        check(`modal ${wide.table}: 95% of the page, one band, tabs and chat (${tag})`,
          m.expanded && Math.abs(m.w - 0.95) < 0.01 && Math.abs(m.h - 0.95) < 0.01 && m.grids === 1 && m.tabs === 3 && m.chat,
          JSON.stringify(m));
        p = join(outDir, `${prefix}-modal-${wide.table}-${tag}.png`);
        await page.screenshot({ path: p }); results.shots.push(p);
        await page.keyboard.press("Escape");
        await page.waitForTimeout(200);
        const back = await page.evaluate(() => { const d = document.querySelector("dialog.drawer"); return { open: d.open, expanded: d.classList.contains("expanded") }; });
        check(`modal ${wide.table}: Esc returns to the drawer (${tag})`, back.open && !back.expanded, JSON.stringify(back));
        await page.click(".drawer-expand");
        await page.waitForTimeout(250);
        await page.click(".drawer-close");
        await page.waitForTimeout(200);
        const back2 = await page.evaluate(() => { const d = document.querySelector("dialog.drawer"); return { open: d.open, expanded: d.classList.contains("expanded") }; });
        check(`modal ${wide.table}: Close returns to the drawer (${tag})`, back2.open && !back2.expanded, JSON.stringify(back2));
        await page.evaluate((i) => { location.hash = ""; }, null);
        await page.evaluate((i) => { location.hash = i; }, first.id);
        await page.waitForTimeout(300);
        const swapped = await page.evaluate(() => document.querySelector("#schema-drawer-title code")?.textContent);
        check(`hash still opens a table while the drawer is open (${tag})`, swapped === first.table, swapped);
      }
      await closeAll();
      check(`drawer closes (${tag})`, !(await page.evaluate(() => document.querySelector("dialog.drawer").open)));

      // 4. the schema diagram's table cards
      const figSel = diagramArg ? `#${diagramArg}` : "figure.diagram:has(.tnode)";
      const fig = page.locator(figSel).first();
      if (await fig.count()) {
        await fig.scrollIntoViewIfNeeded();
        const cards = await page.evaluate((sel) => {
          const f = document.querySelector(sel);
          const ns = [...f.querySelectorAll(".tnode")];
          return ns.map((n) => {
            const r = n.getBoundingClientRect(), tiles = n.querySelectorAll(".tn-tile");
            const clipped = [...n.querySelectorAll(".tn-col, .tn-head, .tn-strip")].some((e) => e.getBoundingClientRect().bottom > r.bottom + 0.5);
            return { table: n.dataset.table, tiles: tiles.length, cols: n.querySelectorAll(".tn-col").length, keys: n.querySelectorAll(".tn-col.key").length, clipped, focusable: n.tabIndex >= 0 };
          });
        }, figSel);
        check(`diagram cards (${tag})`, cards.length > 0 && cards.every((c) => c.tiles === 4 && !c.clipped && c.focusable), JSON.stringify(cards));
        p = join(outDir, `${prefix}-diagram-${tag}.png`);
        await fig.screenshot({ path: p }); results.shots.push(p);
        if (width === widths[0]) {
          // the most telling tile: a measured value with an expected change, then any expected change, then the first
          const pick = await page.evaluate((sel) => {
            const ts = [...document.querySelectorAll(`${sel} .tnode .tn-tile:not(.k-cols)`)];
            const score = (t) => (t.querySelector(".tn-v:not(.none)") ? 2 : 0) + (t.querySelector(".tn-d:not(.none)") ? 1 : 0);
            let best = 0;
            ts.forEach((t, i) => { if (score(t) > score(ts[best])) best = i; });
            return best;
          }, figSel);
          const tile = page.locator(`${figSel} .tnode .tn-tile:not(.k-cols)`).nth(pick);
          await tile.hover();
          await page.waitForTimeout(400);
          const tip = await page.evaluate(() => { const t = document.querySelector(".tn-tip.show"); return t ? t.textContent : ""; });
          check(`tile tooltip on hover (${tag})`, tip.length > 10, tip.slice(0, 120));
          const box = await fig.boundingBox();
          p = join(outDir, `${prefix}-diagram-tip-${tag}.png`);
          await page.screenshot({ path: p, clip: { x: box.x, y: box.y, width: box.width, height: Math.min(box.height + 160, 2000) } }); results.shots.push(p);
          await page.mouse.move(2, 2);
          const card = page.locator(`${figSel} .tnode`).first();
          await card.click({ position: { x: 20, y: 12 } });
          await page.waitForSelector("dialog.drawer[open]", { timeout: 3000 }).catch(() => {});
          const opened = await page.evaluate(() => document.querySelector("#schema-drawer-title code")?.textContent || "");
          check(`card click opens the drawer (${tag})`, !!opened, opened);
          await closeAll();
        }
      } else check(`diagram with table cards (${tag})`, false, `no ${figSel}`);

      check(`console errors (${tag})`, errors.length === 0, errors.slice(0, 3).join(" | "));
      await ctx.close();
    }
  }
} finally {
  await browser.close();
}
console.log(JSON.stringify({ passed: results.checks.length - failed, failed, ...results }, null, 1));
process.exit(failed ? 1 : 0);
