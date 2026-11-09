// D26/D28/D30 screenshots and checks against a built TDD page (build/tdd.html), not the harness page: the drawer's
// one-row head, the bands of a wide table, the Expand view, the column stats badge on each header of the data preview
// (D30), the table brief in the Details tab and in a card's hover card (D28), and the table cards on the schema
// diagram with their one-row measure strip (D28).
//
// Usage: node harness/shoot-d26.mjs <tdd.html> <png dir> [--widths 1440,390] [--schemes light,dark]
//          [--table <table>] [--wide <table>] [--brief <table>] [--diagram <figure id>] [--prefix d26]
// --table defaults to the first schema table, --wide to the table with the most columns, --brief to the first table
// with trade-offs (else --table), --diagram to the first figure.diagram holding a .tnode card.
// Writes <prefix>-drawer-<table>-<w>-<scheme>.png, <prefix>-bands-<wide>-<w>-<scheme>.png, <prefix>-modal-<wide>-<w>-
// <scheme>.png (wide viewports only: phones keep the banded drawer, no Expand), <prefix>-diagram-<w>-<scheme>.png and,
// at the first width: <prefix>-diagram-tip-<w>-<scheme>.png (a measure's tooltip under the pointer),
// <prefix>-diagram-focus-<w>-<scheme>.png (a focused card's hover card), <prefix>-diagram-touch-<w>-<scheme>.png (a tapped
// measure on a touch screen), d28-strip-<w>-<scheme>.png (one card and its strip close up, with a measure's tooltip),
// d28-detail-pop-<w>-<scheme>.png (the hover card's brief), d28-detail-drawer-<w>-<scheme>.png (the Details tab's
// brief), d30-stats-hover-<w>-<scheme>.png and d30-stats-open-<w>-<scheme>.png (a column's stats on hover and opened by
// a click); at phone widths, for the first scheme only, d30-stats-open-<w>-<scheme>.png (opened by a tap).
// The card checks: four measures in one row no taller than a third of the three-row tiles it replaced (4.40 em), or
// the compact card (no strip); nothing clipped; no name, column name, type, value or delta cut; at widths of 1000 px
// and up type of at least 11 px for column names and values and 10 px for every label (at phone widths the drawing is
// scaled to fit the column). The brief checks: four measure rows with "After this design", the mini-diagram with one
// edge per neighbour box and at least one where foreign keys exist, a writes arrow where the card states writes, and
// trade-offs where the card has them. The stats checks: no stats row or toggle, one badge per column, headers in line
// with cells, hover shows and click pins one popover (aria-expanded, aria-controls), a second click, an outside click
// and Esc close it, Esc keeps the drawer open, a tap opens it on a touch screen.
// Prints the checks as JSON; exit 0 when all pass. playwright-core comes from ~/.local/share/system-design-skills/node_modules.
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
const briefArg = opt("--brief", "");
const OLD_STRIP_EM = 4.4; // the three-row sizing tiles (D26) the one-row strip replaced, measured on the reference packet
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
        // foreign keys the way TableNode.tsx keyCols reads them: REFERENCES x in the type, "→ x" in the note or type,
        // or a FOREIGN KEY (...) → x / REFERENCES x line in indexes[]
        const ARROW = /(?:^|[\s(;:,])→\s*([A-Za-z_][\w.]*)/, REF = /\bREFERENCES\s+([A-Za-z_][\w.]*)/i;
        const FK = /FOREIGN\s+KEY\b[^(]*\(([^)]*)\)\s*(?:→|REFERENCES)\s*([A-Za-z_][\w.]*)/i;
        const base = (n) => (n.split(".").pop() || n).replace(/"/g, "");
        const fks = (t) => [...(t.columns || []).map((col) => {
          const ty = col.type || "", no = col.note || "";
          const r = REF.exec(ty)?.[1] || ARROW.exec(no)?.[1] || ARROW.exec(ty)?.[1] || "";
          return r ? base(r) : "";
        }), ...(t.indexes || []).map((l) => FK.exec(l)?.[2] || "").map((r) => (r ? base(r) : ""))].filter(Boolean);
        const has = (m) => !!m && m.value !== undefined && m.value !== null && String(m.value).trim() !== "";
        return { tables: c.schema.map((t) => ({ id: t.id, table: t.table, cols: t.columns.length,
          out: new Set(fks(t)).size, inn: c.schema.filter((u) => u !== t && fks(u).includes(t.table)).length,
          writes: has(t.expected?.writes) || has(t.measured?.writes), trade: (t.tradeoffs || []).length })) };
      });
      const byName = (n) => info.tables.find((t) => t.table === n || t.id === n);
      const first = byName(tableArg) || info.tables[0];
      const wide = byName(wideArg) || [...info.tables].sort((a, b) => b.cols - a.cols)[0];
      const briefT = byName(briefArg) || info.tables.find((t) => t.trade > 0) || first;
      const boxesFor = (t) => Math.min(t.out, 5) + Math.min(t.inn, 5);

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

      // the table brief (D28) inside a root: the hover card or the Details panel
      const readBrief = (pg, sel) => pg.evaluate((q) => {
        const b = document.querySelector(q)?.querySelector(".tbrief");
        if (!b) return null;
        const mm = b.querySelector("svg.mm");
        return {
          title: b.querySelector(".tb-title code")?.textContent || "",
          rows: b.querySelectorAll(".tb-measures tbody tr").length,
          head: [...b.querySelectorAll(".tb-measures thead th")].map((e) => e.textContent.trim()),
          first: [...b.querySelectorAll(".tb-measures tbody tr")].slice(0, 4).map((tr) => tr.querySelector("th").textContent.trim()),
          trade: b.querySelectorAll(".tb-trade li").length,
          map: !!mm, edges: mm ? mm.querySelectorAll(".mm-edge:not(.w)").length : 0, boxes: mm ? mm.querySelectorAll(".mm-node:not(.mid)").length : 0,
          writes: mm ? mm.querySelectorAll(".mm-edge.w").length : 0, wlabel: mm?.querySelector(".mm-wlabel")?.textContent || "",
          lone: /no foreign key in or out/.test(mm?.textContent || ""), label: mm?.getAttribute("aria-label") || "",
        };
      }, sel);
      const briefOk = (b, t) => !!b && b.rows >= 4 && b.head.includes("After this design")
        && b.first.join() === "Rows,Columns,Writes,Cost" && b.map && b.edges === b.boxes && b.boxes === boxesFor(t)
        && (boxesFor(t) ? b.edges >= 1 : b.lone) && (t.writes ? b.writes === 1 && /^writes /.test(b.wlabel) : b.writes === 0)
        && (t.trade ? b.trade >= t.trade : b.trade === 0);
      const briefSay = (b) => (b ? JSON.stringify({ rows: b.rows, first: b.first, trade: b.trade, edges: b.edges, boxes: b.boxes, writes: b.writes, wlabel: b.wlabel, lone: b.lone }) : "no brief");

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

      // 1b. the table brief (D28) in the Details tab: purpose, measures, trade-offs, the mini-diagram
      await openTable(briefT.id);
      await page.click("#dg-tab-details");
      await page.waitForTimeout(300);
      let b = await readBrief(page, "#dg-panel-details");
      check(`Details ${briefT.table}: the brief with measures, trade-offs and the mini-diagram (${tag})`, briefOk(b, briefT), briefSay(b));
      const dOver = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
      check(`Details ${briefT.table}: no horizontal page overflow (${tag})`, dOver <= 0, `${dOver}px`);
      if (width === widths[0]) {
        p = join(outDir, `d28-detail-drawer-${tag}.png`);
        await page.locator("dialog.drawer").screenshot({ path: p }); results.shots.push(p);
      }
      await page.click("#dg-tab-preview"); // the drawer keeps its tab while the same table reopens
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

      // 3b. the column stats badge (D30): one per header, hover shows, click pins, Esc / outside / second click close
      const stats = (pg) => pg.evaluate(() => {
        const d = document.querySelector("dialog.drawer");
        const pop = d.querySelector(".dg-spop");
        const sb = [...d.querySelectorAll(".dg-grid button.dg-sb")].filter((x) => !x.closest(".dg-ghost-box")); // the ghost grid's badges are spans
        const ex = sb.filter((x) => x.getAttribute("aria-expanded") === "true");
        return {
          drawer: d.open, badges: sb.length, nulls: sb.findIndex((x) => x.classList.contains("nulls")),
          labelled: sb.every((x) => x.tagName === "BUTTON" && !!x.getAttribute("aria-label") && x.getAttribute("aria-controls") === pop?.id),
          hstats: !!d.querySelector(".dg-hstats, tr.dg-stats"), toggle: !!d.querySelector(".dg-panel [aria-pressed]"),
          show: !!pop?.classList.contains("show") && !pop.hidden, open: !!pop?.classList.contains("open"), text: pop && !pop.hidden ? pop.textContent : "",
          expanded: ex.map((x) => sb.indexOf(x)), focus: document.activeElement?.classList.contains("dg-sb") ? sb.indexOf(document.activeElement) : -1,
          aligned: [...d.querySelectorAll(".dg-grid")].filter((g) => !g.closest(".dg-ghost-box")).every((g) => {
            const h = [...g.querySelectorAll("tr.dg-head > th")].map((x) => Math.round(x.getBoundingClientRect().left));
            const c = [...g.querySelectorAll("tbody tr:first-child > *")].map((x) => Math.round(x.getBoundingClientRect().left));
            return h.length > 0 && JSON.stringify(h) === JSON.stringify(c);
          }),
        };
      });
      const statText = (s) => /\d+% null/.test(s.text) && /distinct/.test(s.text) && /sample rows?, not the database/i.test(s.text);
      if (width >= 641) {
        await openTable(wide.id);
        await page.click("#dg-tab-preview");
        const s0 = await stats(page);
        check(`stats ${wide.table}: no stats row or toggle, one labelled badge per column (${tag})`,
          !s0.hstats && !s0.toggle && s0.badges === wide.cols && s0.labelled, JSON.stringify({ badges: s0.badges, cols: wide.cols, hstats: s0.hstats, toggle: s0.toggle, labelled: s0.labelled }));
        check(`stats ${wide.table}: headers line up with the cells (${tag})`, s0.aligned, String(s0.aligned));
        const bi = Math.max(0, s0.nulls);
        const badge = page.locator("dialog.drawer .dg-panel .dg-grid button.dg-sb").nth(bi);
        await badge.scrollIntoViewIfNeeded();
        await badge.hover();
        await page.waitForTimeout(400);
        const sh = await stats(page);
        check(`stats ${wide.table}: hover shows the popover, not pinned (${tag})`, sh.show && !sh.open && !sh.expanded.length && statText(sh), sh.text.slice(0, 140));
        if (width === widths[0]) { p = join(outDir, `d30-stats-hover-${tag}.png`); await page.locator("dialog.drawer").screenshot({ path: p }); results.shots.push(p); }
        await badge.click();
        await page.mouse.move(4, 4);
        await page.waitForTimeout(400);
        const so = await stats(page);
        check(`stats ${wide.table}: click pins it as a dropdown that stays when the pointer leaves (${tag})`,
          so.show && so.open && so.expanded.join() === String(bi) && statText(so), JSON.stringify({ show: so.show, open: so.open, expanded: so.expanded }));
        if (width === widths[0]) { p = join(outDir, `d30-stats-open-${tag}.png`); await page.locator("dialog.drawer").screenshot({ path: p }); results.shots.push(p); }
        await badge.click();
        await page.waitForTimeout(300);
        const s2 = await stats(page);
        await badge.click();
        await page.waitForTimeout(200);
        await page.locator("dialog.drawer .drawer-name").click();
        await page.waitForTimeout(300);
        const s3 = await stats(page);
        check(`stats ${wide.table}: a second click or a click outside closes it (${tag})`, !s2.show && !s2.expanded.length && !s3.show && !s3.expanded.length && s3.drawer,
          JSON.stringify({ second: s2.show, outside: s3.show, drawer: s3.drawer }));
        await badge.focus();
        await page.keyboard.press("Enter");
        await page.waitForTimeout(200);
        const s4 = await stats(page);
        const other = bi === 0 ? 1 : 0;
        await page.locator("dialog.drawer .dg-panel .dg-grid button.dg-sb").nth(other).focus();
        await page.keyboard.press(" ");
        await page.waitForTimeout(200);
        const s5 = await stats(page);
        await page.keyboard.press("Escape");
        await page.waitForTimeout(200);
        const s6 = await stats(page);
        check(`stats ${wide.table}: Enter opens, Space on another badge moves it (one open), Esc closes it, keeps the drawer and the focus (${tag})`,
          s4.open && s4.expanded.join() === String(bi) && s5.open && s5.expanded.join() === String(other) && !s6.show && !s6.expanded.length && s6.drawer && s6.focus === other,
          JSON.stringify({ enter: s4.expanded, space: s5.expanded, esc: { show: s6.show, drawer: s6.drawer, focus: s6.focus } }));
        await closeAll();
      } else {
        // touch: a tap opens the dropdown, a second tap closes it
        const tctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 2, colorScheme: scheme, reducedMotion: "reduce", hasTouch: true, isMobile: true });
        const tp = await tctx.newPage();
        tp.on("pageerror", (e) => errors.push(String(e)));
        await tp.goto(url, { waitUntil: "load" });
        await tp.evaluate((i) => { location.hash = i; }, wide.id);
        await tp.waitForSelector("dialog.drawer[open]");
        await tp.waitForTimeout(400);
        const t0 = await stats(tp);
        const tb = tp.locator("dialog.drawer .dg-panel .dg-grid button.dg-sb").nth(Math.max(0, t0.nulls));
        await tb.scrollIntoViewIfNeeded();
        await tb.tap();
        await tp.waitForTimeout(300);
        const t1 = await stats(tp);
        if (scheme === schemes[0]) { p = join(outDir, `d30-stats-open-${tag}.png`); await tp.screenshot({ path: p }); results.shots.push(p); }
        await tb.tap();
        await tp.waitForTimeout(300);
        const t2 = await stats(tp);
        check(`stats ${wide.table}: a tap opens the dropdown, a second tap closes it, headers line up (${tag})`,
          t0.badges === wide.cols && t0.aligned && t1.open && t1.expanded.length === 1 && statText(t1) && !t2.show && t2.drawer,
          JSON.stringify({ badges: t0.badges, aligned: t0.aligned, open: t1.open, text: t1.text.slice(0, 60), closed: !t2.show }));
        await tctx.close();
      }

      // 4. the schema diagram's table cards
      const figSel = diagramArg ? `#${diagramArg}` : "figure.diagram:has(.tnode)";
      const fig = page.locator(figSel).first();
      if (await fig.count()) {
        await fig.scrollIntoViewIfNeeded();
        const cards = await page.evaluate((sel) => {
          const f = document.querySelector(sel);
          const ns = [...f.querySelectorAll(".tnode")];
          const px = (e) => parseFloat(getComputedStyle(e).fontSize);
          const min = (n, q) => Math.min(...[...n.querySelectorAll(q)].map(px), 99);
          return ns.map((n) => {
            const r = n.getBoundingClientRect(), inn = n.querySelector(".tn-in"), strip = n.querySelector(".tn-strip");
            const ms = [...n.querySelectorAll(".tn-strip .tn-m")];
            const sr = strip?.getBoundingClientRect();
            const clipped = [...n.querySelectorAll(".tn-col, .tn-head, .tn-strip, .tn-more")].some((e) => e.getBoundingClientRect().bottom > r.bottom + 0.5)
              || inn.scrollHeight > inn.clientHeight + 1;
            // natural width: the element laid out alone at max-content (an ellipsis cut by a fraction of a pixel counts)
            const natural = (e) => { const s = e.getAttribute("style"); e.style.cssText += ";flex:none;max-width:none;width:max-content"; const w = e.getBoundingClientRect().width; if (s === null) e.removeAttribute("style"); else e.setAttribute("style", s); return w; };
            const cut = [...n.querySelectorAll(".tn-name, .tn-d, .tn-v, .tn-cn, .tn-ty")].filter((e) => natural(e) > e.getBoundingClientRect().width + 0.25
              || e.getBoundingClientRect().right > (e.closest(".tn-m") || inn).getBoundingClientRect().right + 0.5).map((e) => e.textContent);
            const mids = ms.map((m) => { const q = m.getBoundingClientRect(); return q.top + q.height / 2; });
            return {
              table: n.dataset.table, compact: n.classList.contains("compact"), ms: ms.length, cols: n.querySelectorAll(".tn-col").length,
              keys: n.querySelectorAll(".tn-col.tn-key").length, clipped, cut, focusable: n.tabIndex >= 0,
              described: !!document.getElementById(n.getAttribute("aria-describedby") || "-")?.textContent,
              // the strip in its own ems (the card's em), one row: every measure on one centre line, nothing past its right edge
              stripEm: strip ? +(sr.height / parseFloat(getComputedStyle(strip).fontSize)).toFixed(3) : 0,
              oneRow: !strip || (mids.every((y) => Math.abs(y - mids[0]) <= 1) && ms.every((m) => m.getBoundingClientRect().right <= sr.right + 0.5)
                && strip.scrollWidth <= strip.clientWidth + 1),
              tips: ms.every((m) => (m.dataset.tip || "").length > 10),
              text: +min(n, ".tn-cn, .tn-v:not(.none)").toFixed(2),
              label: +min(n, ".tn-name, .tn-chip, .tn-desc, .tn-hidden, .tn-k, .tn-ref, .tn-ty, .tn-tag, .tn-more, .tn-v.none, .tn-d").toFixed(2),
            };
          });
        }, figSel);
        const shapeOk = (c) => (c.compact ? c.ms === 0 : c.ms === 4 && c.stripEm <= OLD_STRIP_EM / 3 && c.oneRow && c.tips) && !c.clipped && !c.cut.length && c.focusable && c.described;
        check(`diagram cards: four measures in one row a third the old height, or compact; nothing clipped or cut; focusable, described (${tag})`,
          cards.length > 0 && cards.every(shapeOk), JSON.stringify(cards.map(({ table, compact, ms, stripEm, oneRow, cols, clipped, cut }) => ({ table, compact, ms, stripEm, oneRow, cols, clipped, cut }))));
        if (width >= 1000) {
          check(`diagram cards: names and numbers at least 11 px, labels at least 10 px (${tag})`,
            cards.every((c) => c.text >= 11 - 0.01 && c.label >= 10 - 0.01), JSON.stringify(cards.map(({ table, text, label }) => ({ table, text, label }))));
        }
        p = join(outDir, `${prefix}-diagram-${tag}.png`);
        await fig.screenshot({ path: p }); results.shots.push(p);
        if (width === widths[0]) {
          // the most telling measure: a measured value with an expected change, then any expected change, then the first
          const pick = await page.evaluate((sel) => {
            const ts = [...document.querySelector(sel).querySelectorAll(".tnode .tn-m:not(.k-cols)")]; // the first such figure only
            const score = (t) => (t.querySelector(".tn-v:not(.none)") ? 2 : 0) + (t.querySelector(".tn-d") ? 1 : 0);
            let best = 0;
            ts.forEach((t, i) => { if (score(t) > score(ts[best])) best = i; });
            return best;
          }, figSel);
          const tile = fig.locator(".tnode .tn-m:not(.k-cols)").nth(pick);
          await tile.hover();
          await page.waitForTimeout(400);
          const tip = await page.evaluate(() => { const t = document.querySelector(".tn-tip.show"); return t ? t.textContent : ""; });
          check(`measure tooltip on hover (${tag})`, tip.length > 10, tip.slice(0, 120));
          const box = await fig.boundingBox();
          p = join(outDir, `${prefix}-diagram-tip-${tag}.png`);
          await page.screenshot({ path: p, clip: { x: box.x, y: box.y, width: box.width, height: Math.min(box.height + 160, 2000) } }); results.shots.push(p);
          // close up: the hovered measure's card and its tooltip
          const near = await page.evaluate(([i, sel]) => {
            const m = document.querySelector(sel).querySelectorAll(".tnode .tn-m:not(.k-cols)")[i], c = m.closest(".tnode").getBoundingClientRect();
            const t = document.querySelector(".tn-tip.show")?.getBoundingClientRect() || c;
            const x = Math.max(0, Math.min(c.left, t.left) - 16), y = Math.max(0, Math.min(c.top, t.top) - 16);
            return { x, y, width: Math.min(innerWidth - x, Math.max(c.right, t.right) + 16 - x), height: Math.max(c.bottom, t.bottom) + 16 - y };
          }, [pick, figSel]);
          p = join(outDir, `d28-strip-${tag}.png`);
          await page.screenshot({ path: p, clip: near }); results.shots.push(p);
          await page.mouse.move(2, 2);
          await page.waitForTimeout(200);

          // keyboard: every focused card's hover card holds its brief (D28): measures, trade-offs, the mini-diagram
          const pops = [];
          const cardTables = await page.evaluate((sel) => [...document.querySelector(sel).querySelectorAll(".tnode")].map((n) => n.dataset.table), figSel);
          for (const [i, name] of cardTables.entries()) {
            const t = byName(name);
            await fig.locator(".tnode").nth(i).focus();
            await page.waitForFunction((n) => document.querySelector(".hs-pop.show.has-brief .tb-title code")?.textContent === n, t?.table || name, { timeout: 3000 }).catch(() => {});
            await page.waitForTimeout(150);
            const has = await page.evaluate(() => !!document.querySelector(".hs-pop.show.has-brief"));
            const bf = has ? await readBrief(page, ".hs-pop.show.has-brief") : null;
            pops.push({ table: name, ok: !!t && !!bf && bf.title === t.table && briefOk(bf, t), say: `${bf?.title} ${briefSay(bf)}` });
            if (t && t.table === briefT.table) {
              p = join(outDir, `d28-detail-pop-${tag}.png`);
              await page.locator(".hs-pop.show").screenshot({ path: p }); results.shots.push(p);
            }
          }
          check(`focused cards show the brief in the hover card: measures, trade-offs, the mini-diagram with its edges (${tag})`,
            pops.length > 0 && pops.every((x) => x.ok), JSON.stringify(pops.filter((x) => !x.ok).map((x) => `${x.table}: ${x.say}`)) + ` (${pops.length} cards)`);
          await fig.locator(".tnode:not(.compact)").first().focus();
          await page.waitForTimeout(300);
          p = join(outDir, `${prefix}-diagram-focus-${tag}.png`);
          await page.screenshot({ path: p, clip: { x: box.x, y: box.y, width: box.width, height: Math.min(box.height + 200, 2000) } }); results.shots.push(p);
          await page.evaluate(() => document.activeElement?.blur());
          await page.mouse.move(2, 2);
          await page.waitForTimeout(200);

          // touch: a tap on a measure toggles its tooltip and does not open the drawer; a tap elsewhere hides it
          const tctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 2, colorScheme: scheme, reducedMotion: "reduce", hasTouch: true });
          const tp = await tctx.newPage();
          tp.on("pageerror", (e) => errors.push(String(e)));
          await tp.goto(url, { waitUntil: "load" });
          await tp.evaluate(async () => { document.querySelectorAll("img").forEach((i) => { i.loading = "eager"; }); await document.fonts?.ready; });
          const tfig = tp.locator(figSel).first();
          await tfig.scrollIntoViewIfNeeded();
          const ttile = tfig.locator(".tnode .tn-m:not(.k-cols)").nth(pick);
          await ttile.tap();
          await tp.waitForTimeout(300);
          const tapped = await tp.evaluate(() => ({ tip: document.querySelector(".tn-tip.show")?.textContent || "", drawer: !!document.querySelector("dialog.drawer[open]") }));
          const tbox = await tfig.boundingBox();
          p = join(outDir, `${prefix}-diagram-touch-${tag}.png`);
          await tp.screenshot({ path: p, clip: { x: tbox.x, y: tbox.y, width: tbox.width, height: Math.min(tbox.height + 160, 2000) } }); results.shots.push(p);
          await ttile.tap();
          await tp.waitForTimeout(300);
          const again = await tp.evaluate(() => !!document.querySelector(".tn-tip.show"));
          await ttile.tap();
          await tp.waitForTimeout(200);
          await tp.locator("body").tap({ position: { x: 4, y: 4 } });
          await tp.waitForTimeout(300);
          const away = await tp.evaluate(() => !!document.querySelector(".tn-tip.show"));
          check(`touch: a tap on a measure shows its tooltip, a second tap or a tap elsewhere hides it, the drawer stays shut (${tag})`,
            tapped.tip.length > 10 && !tapped.drawer && !again && !away, JSON.stringify({ tip: tapped.tip.slice(0, 80), drawer: tapped.drawer, again, away }));
          await tctx.close();

          await page.mouse.move(2, 2);
          const card = fig.locator(".tnode").first();
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
