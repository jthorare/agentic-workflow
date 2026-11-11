#!/usr/bin/env bun
/**
 * Hydrate a built TDD page in jsdom and exercise its interactions.
 *
 * Usage: bun run check_hydrate.ts <build-dir>/tdd.html
 *
 * Wraps the page in a minimal document (as the artifact host does), runs the inline client bundle, waits for React to
 * hydrate, then: toggles a diagram's zoom (proves handlers attached), clicks a reference token (the panel dialog opens
 * with a copy of the anchor), hovers one (the hover card shows, Esc hides it), hovers a diagram hotspot (the hotspot
 * card shows; a synthetic probe is added when the page has no hotspots, and the output says so), clicks a shot (the
 * lightbox scrim shows, Esc closes it), runs Expand all / Collapse all, and follows a hash into a closed block.
 * jsdom lacks HTMLDialogElement.showModal/close, so a minimal shim adds them before the page runs.
 * Exit 1 on any console.error, any jsdom error, any hydration warning, or any failed step; prints counts either way.
 */
import { JSDOM, VirtualConsole } from "jsdom";

const file = process.argv[2];
if (!file) { console.error("usage: bun run check_hydrate.ts <page.html>"); process.exit(2); }
const path = file.startsWith("/") ? file : `${process.cwd()}/${file}`;
const page = await Bun.file(path).text().catch(() => "");
if (!page) { console.error(`check_hydrate: cannot read ${path}`); process.exit(2); }

const errors: string[] = [], warnings: string[] = [], failures: string[] = [], notes: string[] = [];
const say = (a: unknown[]) => a.map((x) => (x instanceof Error ? x.stack || x.message : String(x))).join(" ").slice(0, 800);
const vc = new VirtualConsole();
vc.on("error", (...a: unknown[]) => errors.push(say(a)));
vc.on("warn", (...a: unknown[]) => warnings.push(say(a)));
vc.on("jsdomError", (e: Error) => errors.push(`jsdomError: ${e.message}`));

// jsdom has HTMLDialogElement but not show/showModal/close (every evergreen browser has them): add the minimal
// behaviour before the page's scripts run, so components exercise the same code path they take in a browser.
const dialogShim = (win: Window & typeof globalThis) => {
  const P = win.HTMLDialogElement.prototype as any;
  if (typeof P.showModal !== "function") P.showModal = function (this: HTMLDialogElement) { this.setAttribute("open", ""); };
  if (typeof P.show !== "function") P.show = function (this: HTMLDialogElement) { this.setAttribute("open", ""); };
  if (typeof P.close !== "function") P.close = function (this: HTMLDialogElement, v?: string) {
    if (!this.hasAttribute("open")) return;
    this.removeAttribute("open");
    if (v !== undefined) this.returnValue = v;
    this.dispatchEvent(new win.Event("close"));
  };
};
const dom = new JSDOM(`<!doctype html><html lang="en"><head><meta charset="utf-8"></head><body>\n${page}\n</body></html>`, {
  url: `file://${encodeURI(path)}`, runScripts: "dangerously", pretendToBeVisual: true, virtualConsole: vc, beforeParse: dialogShim,
});
const w = dom.window;
const d = w.document;
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));
const check = (ok: unknown, what: string) => { if (!ok) failures.push(what); return !!ok; };
const hydrated = (el: Element | null) => !!el && Object.keys(el).some((k) => k.startsWith("__reactFiber"));
const pointer = (type: string, el: Element) => {
  const init = { bubbles: true, cancelable: true, composed: true, pointerType: "mouse", view: w } as PointerEventInit;
  el.dispatchEvent(typeof w.PointerEvent === "function" ? new w.PointerEvent(type, init) : new w.MouseEvent(type, init));
};
const key = (k: string) => d.dispatchEvent(new w.KeyboardEvent("keydown", { key: k, bubbles: true }));

// ---- hydration
const content = JSON.parse(d.getElementById("tdd-data")?.textContent || "null");
check(content, "no #tdd-data JSON island");
let waited = 0;
while (!hydrated(d.querySelector("#tdd-root .wrap")) && waited < 5000) { await sleep(50); waited += 50; }
check(hydrated(d.querySelector("#tdd-root .wrap")), `React did not hydrate .wrap within ${waited} ms`);
await sleep(100); // effects (delegation, overlays' bus listeners)
check(d.documentElement.classList.contains("js"), "client did not add the js class");
const counts = {
  sections: d.querySelectorAll("#tdd-root section[id]").length,
  refs: d.querySelectorAll("#tdd-root .wrap a.ref").length,
  hotspots: d.querySelectorAll("#tdd-root [data-hs]").length,
  shots: d.querySelectorAll("#tdd-root .wrap figure.shot img").length,
  diagrams: d.querySelectorAll("#tdd-root figure.diagram").length,
  details: d.querySelectorAll("#tdd-root details").length,
  expandLines: d.querySelectorAll("#tdd-root .expand-line").length,
};
check(counts.sections === (content?.sections?.length ?? -1), `sections in DOM ${counts.sections} != content ${content?.sections?.length}`);

// ---- 1. a React handler: the diagram zoom toggle
const zoom = d.querySelector("figure.diagram .zoom") as HTMLButtonElement | null;
if (zoom) {
  const before = zoom.textContent;
  zoom.click(); await sleep(30);
  const after = zoom.textContent;
  check(before !== after && zoom.closest("figure")?.classList.contains("zoomed"), `zoom toggle did not re-render (${before} -> ${after})`);
  zoom.click(); await sleep(30);
} else notes.push("no diagram on the page; zoom toggle not exercised");

// ---- 2. click a reference token -> the panel dialog
const defs = content?.defs || {};
const refs = [...d.querySelectorAll("#tdd-root .wrap a.ref")] as HTMLAnchorElement[];
const ref = refs.find((a) => { const id = a.dataset.ref || ""; return defs[id] && defs[id].kind !== "table" && d.getElementById(id); });
const panel = d.querySelector("dialog.ref-panel") as HTMLDialogElement | null;
check(panel, "no dialog.ref-panel rendered");
let panelInfo = "not run";
if (ref && panel) {
  ref.click(); await sleep(60);
  const body = panel.querySelector(".ref-panel-body");
  const open = panel.hasAttribute("open");
  const kids = body?.childElementCount || 0;
  const head = panel.querySelector(".ref-id")?.textContent || "";
  panelInfo = `${ref.textContent} -> open=${open} body children=${kids} head="${head}"`;
  check(open, `clicking a.ref ${ref.textContent} did not open the panel`);
  check(kids > 0, "the panel body is empty");
  check(!body?.querySelector("[id]"), "the panel copy kept ids (duplicates on the page)");
  (panel.querySelector(".ref-panel-head button") as HTMLButtonElement | null)?.click(); await sleep(30);
  check(!panel.hasAttribute("open"), "Close did not close the panel");
} else if (!ref) notes.push("no a.ref with a page anchor; panel not exercised");

// ---- 3. hover a reference token -> the hover card; Esc hides it
let popInfo = "not run";
if (ref) {
  pointer("pointerover", ref); await sleep(400);
  const pop = d.querySelector(".ref-pop");
  const shown = !!pop?.classList.contains("show");
  popInfo = `shown=${shown} "${(pop?.querySelector("b")?.textContent || "").slice(0, 60)}"`;
  check(shown, "hovering a.ref did not show .ref-pop.show");
  key("Escape"); await sleep(30);
  check(!pop?.classList.contains("show"), "Esc did not hide the hover card");
  pointer("pointerout", ref); await sleep(200);
}

// ---- 4. hover a diagram hotspot -> the hotspot card
let hs = d.querySelector("#tdd-root .wrap [data-hs]") as HTMLElement | null;
let synthetic = false;
if (!hs) {
  // The page has no hotspots: add an unmanaged probe inside the first section; delegation sees it like a real one.
  hs = d.createElement("span");
  hs.setAttribute("data-hs", "");
  hs.setAttribute("data-label", "probe hotspot");
  hs.setAttribute("data-note", "synthetic, added by check_hydrate");
  d.querySelector("#tdd-root section")?.appendChild(hs);
  synthetic = true;
}
pointer("pointerover", hs); await sleep(300);
const hsPop = d.querySelector(".hs-pop");
const hsShown = !!hsPop?.classList.contains("show");
check(hsShown, `hovering [data-hs]${synthetic ? " (synthetic probe)" : ""} did not show .hs-pop.show`);
check((hsPop?.querySelector("b")?.textContent || "") === (hs.dataset.label || ""), "hotspot card label does not match data-label");
pointer("pointerout", hs); await sleep(300);
check(!hsPop?.classList.contains("show"), "hotspot card did not hide after the pointer left");
if (synthetic) hs.remove();

// ---- 5. click a shot -> lightbox
const img = d.querySelector("#tdd-root .wrap figure.shot img") as HTMLImageElement | null;
let shotInfo = "not run";
if (img) {
  img.click(); await sleep(60);
  const scrim = !!d.querySelector(".shot-scrim");
  const big = d.querySelector("figure.shot.big img") as HTMLImageElement | null;
  shotInfo = `scrim=${scrim} big=${!!big}`;
  check(scrim, "clicking a shot did not show .shot-scrim");
  check(big && big.src === img.src, `the lightbox image is not the clicked one (${big?.src} vs ${img.src})`);
  key("Escape"); await sleep(60);
  check(!d.querySelector(".shot-scrim"), "Esc did not close the lightbox");
} else notes.push("no figure.shot on the page; lightbox not exercised");

// ---- 6. Expand all / Collapse all
const line = d.querySelector(".expand-line");
if (line) {
  const sec = line.closest("section")!;
  const blocks = [...sec.querySelectorAll("details.more")] as HTMLDetailsElement[];
  const [exp, col] = [...line.querySelectorAll("button")] as HTMLButtonElement[];
  exp?.click(); await sleep(30);
  check(blocks.length && blocks.every((b) => b.open), "Expand all left a block closed");
  col?.click(); await sleep(30);
  check(blocks.every((b) => !b.open), "Collapse all left a block open");
} else notes.push("no .expand-line on the page");

// ---- 6b. a table hotspot reaches the schema drawer (the schema worker's component; reported, not judged here)
let drawerInfo = "no [data-hs][data-table] on the page";
const tableHs = d.querySelector("#tdd-root .wrap [data-hs][data-table]") as HTMLElement | null;
if (tableHs) {
  tableHs.click(); await sleep(60);
  const drawer = d.querySelector("dialog.drawer");
  drawerInfo = drawer ? `${tableHs.dataset.table} -> dialog.drawer open=${drawer.hasAttribute("open")}` : "no dialog.drawer rendered (SchemaDrawer stub?)";
  key("Escape"); await sleep(30);
  if (drawer?.hasAttribute("open")) (drawer.querySelector("button") as HTMLButtonElement | null)?.click();
  await sleep(30);
}

// ---- 7. a hash into a closed block opens it
const hidden = [...d.querySelectorAll("#tdd-root details:not([open]) [id]")].find((e) => e.tagName !== "DETAILS") as HTMLElement | undefined;
if (hidden) {
  w.location.hash = hidden.id; await sleep(80);
  let p: Element | null = hidden, closed = 0;
  for (; p; p = p.parentElement) if (p.tagName === "DETAILS" && !(p as HTMLDetailsElement).open) closed++;
  check(closed === 0, `#${hidden.id}: ${closed} enclosing block(s) still closed after hashchange`);
}

await sleep(50);
const hydrationWarnings = warnings.filter((m) => /hydrat|did not match|mismatch/i.test(m));
console.log(`page ${path}`);
console.log(`hydrated in <= ${waited} ms | sections ${counts.sections} | a.ref ${counts.refs} | [data-hs] ${counts.hotspots} | shots ${counts.shots} | diagrams ${counts.diagrams} | details ${counts.details} | expand lines ${counts.expandLines}`);
console.log(`panel: ${panelInfo}`);
console.log(`hover card: ${popInfo}`);
console.log(`hotspot card: shown=${hsShown}${synthetic ? " (synthetic probe: the page has no hotspots)" : ""}`);
console.log(`lightbox: ${shotInfo}`);
console.log(`schema drawer (info): ${drawerInfo}`);
console.log(`console errors ${errors.length} | console warnings ${warnings.length} (hydration ${hydrationWarnings.length}) | failed steps ${failures.length}`);
for (const n of notes) console.log(`note: ${n}`);
for (const m of warnings) console.log(`warn: ${m}`);
for (const m of errors) console.log(`ERROR: ${m}`);
for (const f of failures) console.log(`FAIL: ${f}`);
w.close();
process.exit(errors.length || hydrationWarnings.length || failures.length ? 1 : 0);
