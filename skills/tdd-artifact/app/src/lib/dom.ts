// Browser-only helpers shared by the delegation in App and the overlays. Nothing here runs during the server render.
import { reveal } from "./bus";

/** Human label for a def kind, shown in the hover card and the panel head. */
export function kindLabel(kind: string) {
  return ({
    decision: "Decision", row: "Row", pr: "PR", persona: "Front end", fold: "Reference block", glossary: "Glossary", section: "Section",
    heading: "Heading", card: "Card", diagram: "Diagram", paragraph: "Paragraph", table: "Table",
  } as Record<string, string>)[kind] || kind;
}

/** Place an absolutely positioned pop below (or, if it does not fit, above) a viewport rect, clamped to the viewport. */
export function place(pop: HTMLElement, r: DOMRect, gap = 6) {
  const de = document.documentElement;
  const vw = de.clientWidth || window.innerWidth, vh = de.clientHeight || window.innerHeight;
  const w = pop.offsetWidth, h = pop.offsetHeight;
  const left = Math.min(Math.max(8, r.left), Math.max(8, vw - w - 8));
  let top = r.bottom + gap;
  if (top + h > vh - 8 && r.top - gap - h >= 8) top = r.top - gap - h;
  // viewport -> the pop's containing block (page coordinates unless a positioned ancestor wraps the page)
  let ox = window.scrollX, oy = window.scrollY;
  const op = pop.offsetParent as HTMLElement | null;
  if (op && op !== document.body && op !== de) {
    const o = op.getBoundingClientRect();
    ox = op.scrollLeft - o.left - op.clientLeft;
    oy = op.scrollTop - o.top - op.clientTop;
  }
  pop.style.left = `${Math.round(left + ox)}px`;
  pop.style.top = `${Math.round(top + oy)}px`;
}

function hiddenByDetails(el: HTMLElement) {
  for (let p = el.parentElement; p; p = p.parentElement) if (p instanceof HTMLDetailsElement && !p.open) return true;
  return false;
}

/** reveal() plus a scroll when the target was inside a closed block (the browser's own fragment scroll missed it). */
export function revealAndScroll(id: string, force: boolean) {
  const el = document.getElementById(id);
  if (!el) return false;
  const wasHidden = hiddenByDetails(el);
  reveal(id);
  if ((force || wasHidden) && typeof el.scrollIntoView === "function") el.scrollIntoView({ block: "start" });
  return true;
}

/** Same-document navigation to an anchor: opens what hides it, updates the hash (so :target styles apply), scrolls. */
export function goTo(id: string) {
  if (decodeURIComponent(location.hash.slice(1)) === id) { revealAndScroll(id, true); return; }
  reveal(id);
  location.hash = id;
}

export function openDialog(d: HTMLDialogElement) {
  if (d.open) return;
  if (typeof d.showModal === "function") { try { d.showModal(); return; } catch { /* not connected or already open */ } }
  d.setAttribute("open", "");
}
export function closeDialog(d: HTMLDialogElement) {
  if (!d.open) return;
  if (typeof d.close === "function") { d.close(); return; }
  d.removeAttribute("open");
  d.dispatchEvent(new Event("close"));
}

/** A copy of an anchor's element for the reference panel: rows keep their header, headings keep the content under
 *  them, every <details> is opened, ids are stripped (the page keeps them) and controls that only work in place go. */
export function cloneForPanel(id: string): DocumentFragment | null {
  const el = document.getElementById(id);
  if (!el) return null;
  const frag = document.createDocumentFragment();
  if (el.tagName === "TR") {
    const src = el.closest("table");
    const wrap = document.createElement("div");
    wrap.className = "tbl";
    const table = document.createElement("table");
    const thead = src?.querySelector("thead");
    if (thead) table.appendChild(thead.cloneNode(true));
    const tbody = document.createElement("tbody");
    tbody.appendChild(el.cloneNode(true));
    table.appendChild(tbody);
    wrap.appendChild(table);
    frag.appendChild(wrap);
  } else if (el.parentElement?.matches("dl.glossary")) {
    const dl = document.createElement("dl");
    dl.className = "glossary";
    dl.appendChild(el.cloneNode(true));
    frag.appendChild(dl);
  } else if (/^H[3-6]$/.test(el.tagName)) {
    const level = Number(el.tagName[1]);
    frag.appendChild(el.cloneNode(true));
    for (let s = el.nextElementSibling; s; s = s.nextElementSibling) {
      const m = /^H([1-6])$/.exec(s.tagName);
      if (m && Number(m[1]) <= level) break;
      frag.appendChild(s.cloneNode(true));
    }
  } else {
    frag.appendChild(el.cloneNode(true));
  }
  frag.querySelectorAll("[id]").forEach((n) => n.removeAttribute("id"));
  frag.querySelectorAll("details").forEach((d) => { (d as HTMLDetailsElement).open = true; });
  frag.querySelectorAll(".expand-line, .zoom").forEach((n) => n.remove());
  frag.querySelectorAll("tr.lit").forEach((n) => n.classList.remove("lit"));
  return frag;
}
