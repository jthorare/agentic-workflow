// Event delegation for everything the compiler emits as raw HTML (reference tokens, hotspots, shots, in-page links).
// One listener per event type on the #tdd-root container, which also holds the overlays (see CONTRACTS.md bus table).
import { useEffect, type RefObject } from "react";
import type { Def } from "../types";
import { bus, reveal } from "./bus";
import { closeDialog, goTo, revealAndScroll } from "./dom";

const FOCUS_AFTER_POINTER_MS = 600;

export function useDelegation(wrap: RefObject<HTMLElement | null>, defs: Record<string, Def>) {
  useEffect(() => {
    const root = wrap.current?.parentElement;
    if (!root) return;
    let downAt = 0;
    let curRef: Element | null = null, curHs: Element | null = null;
    const inDialog = (el: Element) => !!el.closest("dialog");
    const refOf = (a: Element) => {
      const id = (a as HTMLElement).dataset.ref || "";
      return defs[id] ? id : "";
    };

    const over = (e: Event) => {
      const t = e.target;
      if (!(t instanceof Element) || (e as PointerEvent).pointerType === "touch") return;
      const a = t.closest("a.ref");
      if (a !== curRef) {
        curRef = a;
        const id = a && !inDialog(a) ? refOf(a) : "";
        if (a && id) bus.emit("ref", { id, anchor: a });
      }
      const hs = t.closest("[data-hs]");
      if (hs !== curHs) {
        curHs = hs;
        if (hs && !inDialog(hs)) bus.emit("hs", { el: hs });
      }
    };
    const out = (e: Event) => {
      const t = e.target;
      if (!(t instanceof Element)) return;
      const rel = (e as PointerEvent).relatedTarget as Node | null;
      const a = t.closest("a.ref");
      if (a && a === curRef && !(rel && a.contains(rel))) { curRef = null; bus.emit("ref", null); }
      const hs = t.closest("[data-hs]");
      if (hs && hs === curHs && !(rel && hs.contains(rel))) { curHs = null; bus.emit("hs", { el: null }); }
    };
    const down = () => { downAt = Date.now(); };
    const focusIn = (e: Event) => {
      const t = e.target;
      if (!(t instanceof Element) || Date.now() - downAt < FOCUS_AFTER_POINTER_MS) return; // a click or tap, not keyboard focus
      const a = t.closest("a.ref");
      if (a && !inDialog(a)) { const id = refOf(a); if (id) bus.emit("ref", { id, anchor: a, pin: true }); }
      const hs = t.closest("[data-hs]");
      if (hs && !inDialog(hs)) bus.emit("hs", { el: hs, pin: true });
    };
    const focusOut = (e: Event) => {
      const t = e.target;
      if (!(t instanceof Element)) return;
      const rel = (e as FocusEvent).relatedTarget as Element | null;
      if ((t.closest("a.ref") || t.closest(".ref-pop")) && !(rel && rel.closest(".ref-pop"))) bus.emit("ref", null);
      if ((t.closest("[data-hs]") || t.closest(".hs-pop")) && !(rel && rel.closest(".hs-pop"))) bus.emit("hs", { el: null });
    };
    const click = (e: Event) => {
      const m = e as MouseEvent;
      if (m.defaultPrevented || m.button !== 0 || m.metaKey || m.ctrlKey || m.shiftKey || m.altKey) return;
      const t = m.target;
      if (!(t instanceof Element)) return;
      const a = t.closest("a.ref");
      if (a) {
        const id = refOf(a);
        if (id) {
          m.preventDefault();
          curRef = null;
          bus.emit(defs[id].kind === "table" ? "table" : "ref-panel", { id, label: (a.textContent || "").trim() });
          return;
        }
      }
      const hs = t.closest("[data-hs][data-table]") as HTMLElement | null;
      if (hs) {
        m.preventDefault();
        curHs = null;
        bus.emit("hs", { el: null });
        bus.emit("table", { id: hs.dataset.table });
        return;
      }
      const img = t.closest("figure.shot img") as HTMLImageElement | null;
      if (img && !img.closest("figure.shot.big") && !inDialog(img)) {
        const fig = img.closest("figure.shot");
        bus.emit("shot", { src: img.currentSrc || img.src, alt: img.alt, caption: fig?.querySelector("figcaption")?.innerHTML || "" });
        return;
      }
      const link = t.closest('a[href^="#"]');
      if (link) {
        const id = decodeURIComponent((link.getAttribute("href") || "").slice(1));
        if (!id || !document.getElementById(id)) return;
        const dlg = link.closest("dialog");
        if (dlg) closeDialog(dlg as HTMLDialogElement);
        reveal(id); // open what hides the target before the browser scrolls to it
      }
    };
    const onHash = () => {
      const id = decodeURIComponent(location.hash.slice(1));
      if (id) revealAndScroll(id, false);
    };

    root.addEventListener("pointerover", over);
    root.addEventListener("pointerout", out);
    root.addEventListener("pointerdown", down);
    root.addEventListener("focusin", focusIn);
    root.addEventListener("focusout", focusOut);
    root.addEventListener("click", click);
    window.addEventListener("hashchange", onHash);
    const offReveal = bus.on("reveal", (p: { id: string }) => { if (p?.id) goTo(p.id); });
    onHash();
    return () => {
      root.removeEventListener("pointerover", over);
      root.removeEventListener("pointerout", out);
      root.removeEventListener("pointerdown", down);
      root.removeEventListener("focusin", focusIn);
      root.removeEventListener("focusout", focusOut);
      root.removeEventListener("click", click);
      window.removeEventListener("hashchange", onHash);
      offReveal();
    };
  }, [wrap, defs]);
}
