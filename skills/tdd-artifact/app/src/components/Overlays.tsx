// The overlays the delegation in App drives through the bus: RefPop (hover card), RefPanel (mobile-sized dialog),
// HsPop (diagram hotspot card; a table's brief for a table box, D28), TipPop (strip-measure tooltip on table cards),
// Lightbox (shot zoom), plus the schema
// worker's SchemaDrawer. All render empty on the
// server and on the first client render, so hydration matches; content arrives only through bus events.
import { useEffect, useLayoutEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { useContent } from "../lib/context";
import { bus } from "../lib/bus";
import { closeDialog, cloneForPanel, kindLabel, openDialog, place } from "../lib/dom";
import { Raw, cx } from "../lib/raw";
import { Guard } from "../lib/guard";
import { SchemaDrawer, findTable } from "./SchemaDrawer";
import { TableBrief } from "./TableBrief";

const OPEN_DELAY_MS = 250, HS_OPEN_DELAY_MS = 120, HIDE_DELAY_MS = 150;

type RefShow = { id: string; anchor: HTMLElement; pin?: boolean } | null;

/** WCAG 1.4.13 hover card: shows after a short hover (at once on keyboard focus), stays while hovered or focused,
 *  hides ~150 ms after the pointer leaves, Esc dismisses it until the pointer moves to another token. */
export function RefPop() {
  const { defs } = useContent();
  const pop = useRef<HTMLDivElement>(null);
  const [cur, setCur] = useState<{ id: string; anchor: HTMLElement } | null>(null);
  const s = useRef({ open: 0, hide: 0, shown: false, inside: false, anchor: null as HTMLElement | null, dismissed: null as HTMLElement | null });

  const clear = () => { clearTimeout(s.current.open); clearTimeout(s.current.hide); };
  const hide = () => { clear(); s.current.shown = false; s.current.anchor = null; setCur(null); };
  const scheduleHide = () => {
    clearTimeout(s.current.open);
    if (!s.current.shown) return;
    clearTimeout(s.current.hide);
    s.current.hide = window.setTimeout(() => {
      const el = pop.current;
      if (s.current.inside || (el && el.contains(document.activeElement))) return;
      hide();
    }, HIDE_DELAY_MS);
  };

  useEffect(() => {
    const offRef = bus.on("ref", (p: RefShow) => {
      if (!p) { s.current.dismissed = null; scheduleHide(); return; }
      if (p.anchor === s.current.dismissed) return;
      clear();
      const show = () => { s.current.shown = true; s.current.anchor = p.anchor; setCur({ id: p.id, anchor: p.anchor }); };
      if (s.current.shown || p.pin) show();
      else s.current.open = window.setTimeout(show, OPEN_DELAY_MS);
    });
    const offPanel = bus.on("ref-panel", hide);
    const offTable = bus.on("table", hide);
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape" || !s.current.shown) return;
      s.current.dismissed = s.current.anchor;
      hide();
    };
    document.addEventListener("keydown", onKey);
    return () => { offRef(); offPanel(); offTable(); clear(); document.removeEventListener("keydown", onKey); };
  }, []);

  useLayoutEffect(() => {
    if (cur && pop.current) place(pop.current, cur.anchor.getBoundingClientRect());
  }, [cur]);

  const def = cur ? defs[cur.id] : undefined;
  const label = cur ? (cur.anchor.textContent || "").trim() || cur.id : "";
  const open = () => {
    if (!cur) return;
    const id = cur.id;
    hide();
    bus.emit(defs[id]?.kind === "table" ? "table" : "ref-panel", { id, label });
  };
  return (
    <div className={cx("ref-pop", def && "show")} ref={pop}
      onPointerEnter={() => { s.current.inside = true; clearTimeout(s.current.hide); }}
      onPointerLeave={() => { s.current.inside = false; scheduleHide(); }}>
      {def ? (
        <>
          <span className="ref-id">{label} · {kindLabel(def.kind)}</span>
          <b>{def.title}</b>
          {def.text ? <span className="ref-text">{def.text}</span> : null}
          <button type="button" className="ref-open linkish" onClick={open}>{def.kind === "table" ? "Open the table" : "Open"}</button>
        </>
      ) : null}
    </div>
  );
}

/** The mobile-sized reference panel: a modal <dialog> whose body is a copy of the anchor's element. */
export function RefPanel() {
  const { defs } = useContent();
  const dlg = useRef<HTMLDialogElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const [cur, setCur] = useState<{ id: string; label: string } | null>(null);

  useEffect(() => bus.on("ref-panel", (p: { id: string; label?: string }) => {
    if (p?.id) setCur({ id: p.id, label: p.label || p.id });
  }), []);

  useEffect(() => {
    const d = dlg.current, b = body.current;
    if (!cur || !d || !b) return;
    const frag = cloneForPanel(cur.id);
    if (frag) b.replaceChildren(frag);
    else {
      const p = document.createElement("p");
      p.textContent = defs[cur.id]?.text || defs[cur.id]?.title || cur.id;
      b.replaceChildren(p);
    }
    b.scrollTop = 0;
    d.scrollTop = 0;
    openDialog(d);
  }, [cur, defs]);

  useEffect(() => {
    const d = dlg.current;
    if (!d) return;
    // Safari has no closedby: close on a click outside the dialog box, and on Esc when shown without showModal.
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape" && d.open) closeDialog(d); };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const onBackdrop = (e: ReactMouseEvent<HTMLDialogElement>) => {
    const d = dlg.current;
    if (!d || e.target !== d) return;
    const r = d.getBoundingClientRect();
    if (e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom) closeDialog(d);
  };
  const go = (e: ReactMouseEvent<HTMLAnchorElement>) => {
    e.preventDefault();
    const d = dlg.current;
    if (d) closeDialog(d);
    if (cur) bus.emit("reveal", { id: cur.id });
  };
  const def = cur ? defs[cur.id] : undefined;
  return (
    <dialog className="ref-panel" ref={dlg} closedby="any" aria-labelledby="tdd-ref-panel-id" onClick={onBackdrop}
      onClose={() => { if (body.current) body.current.replaceChildren(); setCur(null); }}>
      <div className="ref-panel-head">
        <span className="ref-id" id="tdd-ref-panel-id">{cur ? `${cur.label}${def ? ` · ${kindLabel(def.kind)}` : ""}` : "Reference"}</span>
        <div className="acts">
          <a href={cur ? `#${cur.id}` : "#"} onClick={go}>Go to it</a>
          <button type="button" onClick={() => { if (dlg.current) closeDialog(dlg.current); }}>Close</button>
        </div>
      </div>
      <div className="ref-panel-body" ref={body} />
    </dialog>
  );
}

/** The diagram hotspot card (D4): label, note, the box's image at full card width, and a Go link when it has one. */
export function HsPop() {
  const { schema } = useContent();
  const pop = useRef<HTMLDivElement>(null);
  const [el, setEl] = useState<HTMLElement | null>(null);
  const s = useRef({ open: 0, hide: 0, inside: false, shown: false });

  useEffect(() => {
    const clear = () => { clearTimeout(s.current.open); clearTimeout(s.current.hide); };
    const hide = () => { clear(); s.current.shown = false; setEl(null); };
    const off = bus.on("hs", (p: { el: HTMLElement | null; pin?: boolean }) => {
      if (p?.el) {
        const target = p.el;
        clear();
        const show = () => { s.current.shown = true; setEl(target); };
        if (s.current.shown || p.pin) show();
        else s.current.open = window.setTimeout(show, HS_OPEN_DELAY_MS);
        return;
      }
      clearTimeout(s.current.open);
      if (!s.current.shown) return;
      clearTimeout(s.current.hide);
      s.current.hide = window.setTimeout(() => {
        if (s.current.inside || (pop.current && pop.current.contains(document.activeElement))) return;
        hide();
      }, HIDE_DELAY_MS);
    });
    const offTable = bus.on("table", hide);
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape" && s.current.shown) hide(); };
    document.addEventListener("keydown", onKey);
    return () => { off(); offTable(); clear(); document.removeEventListener("keydown", onKey); };
  }, []);

  useLayoutEffect(() => {
    if (el && pop.current) place(pop.current, el.getBoundingClientRect(), 8);
  }, [el]);

  const d: DOMStringMap = el ? el.dataset : {};
  const href = el?.getAttribute("href") || "";
  // a box that carries a schema table (D28): the table's brief, the same block as the drawer's Details tab
  const table = el && d.table ? findTable(schema, d.table) : null;
  return (
    <div className={cx("hs-pop", el && "show", d.image && "has-image", table && "has-brief")} ref={pop}
      onPointerEnter={() => { s.current.inside = true; clearTimeout(s.current.hide); }}
      onPointerLeave={() => { s.current.inside = false; bus.emit("hs", { el: null }); }}>
      {el && table ? (
        <>
          <Guard name="table brief"><TableBrief t={table} mode="pop" /></Guard>
          <span className="go">Click the box to open the table</span>
        </>
      ) : el ? (
        <>
          <b>{d.label}</b>
          {d.note ? <span>{d.note}</span> : null}
          {d.image ? <img src={d.image} alt={d.label ? `${d.label}: full image` : ""} /> : null}
          {d.table ? <span className="go">Click the box to open the table</span>
            : href && href !== "#" ? <a className="go" href={href}>Go to it</a> : null}
        </>
      ) : null}
    </div>
  );
}

/** A strip measure's tooltip text as lines: the first is the title, then "Label: text" lines; a trailing "(rises)",
 *  "(falls)" or "(stays the same)" is coloured. */
export function TipLines({ text }: { text: string }) {
  const lines = text.split("\n").filter(Boolean);
  return (
    <>
      {lines.map((l, i) => {
        if (!i) return <b key={i}>{l}</b>;
        const m = /^([^:]{1,40}):\s(.*)$/.exec(l);
        if (!m) return <span key={i}>{l}</span>;
        const d = /\((rises|falls|stays the same)\)$/.exec(m[2]);
        const tone = d ? (d[1] === "rises" ? "up" : d[1] === "falls" ? "down" : "same") : "";
        return (
          <span key={i}><span className="k">{m[1]}:</span> {d ? <>{m[2].slice(0, d.index)}<span className={tone}>({d[1]})</span></> : m[2]}</span>
        );
      })}
    </>
  );
}

/** The sizing-tile tooltip on schema-diagram table cards (D26): a [data-tip] element under the pointer shows its lines
 *  at page size beside it and hides the hotspot card while it shows; a tap on a tile toggles it (and does not open the
 *  drawer). Keyboard users get the same lines in the hotspot card when the card has focus. The server render also puts
 *  the text in `title` so a page read without JavaScript keeps a native tooltip; it is removed here. */
export function TipPop() {
  const pop = useRef<HTMLDivElement>(null);
  const [el, setEl] = useState<HTMLElement | null>(null);
  useEffect(() => {
    document.querySelectorAll<HTMLElement>("[data-tip][title]").forEach((e) => e.removeAttribute("title"));
    let cur: HTMLElement | null = null, timer = 0, touch = false;
    const tipOf = (t: EventTarget | null) => (t instanceof Element ? (t.closest("[data-tip]") as HTMLElement | null) : null);
    const over = (e: PointerEvent) => {
      if (e.pointerType === "touch") return;
      const t = tipOf(e.target);
      if (t === cur) return;
      cur = t;
      clearTimeout(timer);
      if (t) timer = window.setTimeout(() => { bus.emit("hs", { el: null }); setEl(t); }, 90);
      else timer = window.setTimeout(() => setEl(null), 90);
    };
    const down = (e: PointerEvent) => {
      touch = e.pointerType === "touch" || e.pointerType === "pen";
      if (touch && !tipOf(e.target)) { cur = null; setEl(null); }
    };
    const click = (e: MouseEvent) => {
      const t = tipOf(e.target);
      if (!t || !touch) return;
      e.preventDefault(); // a tap on a tile reads it; the rest of the card still opens the drawer
      clearTimeout(timer);
      cur = cur === t ? null : t;
      setEl(cur);
    };
    const hide = () => { clearTimeout(timer); cur = null; setEl(null); };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") hide(); };
    document.addEventListener("pointerover", over);
    document.addEventListener("pointerdown", down, true);
    document.addEventListener("click", click, true);
    document.addEventListener("keydown", onKey);
    window.addEventListener("scroll", hide, { passive: true });
    const offTable = bus.on("table", hide);
    return () => {
      clearTimeout(timer);
      document.removeEventListener("pointerover", over);
      document.removeEventListener("pointerdown", down, true);
      document.removeEventListener("click", click, true);
      document.removeEventListener("keydown", onKey);
      window.removeEventListener("scroll", hide);
      offTable();
    };
  }, []);
  useLayoutEffect(() => {
    if (el && pop.current) place(pop.current, el.getBoundingClientRect(), 6);
  }, [el]);
  return (
    <div className={cx("tn-tip", el && "show")} ref={pop} role="tooltip">
      {el ? <TipLines text={el.dataset.tip || ""} /> : null}
    </div>
  );
}

/** Click a mock to see it large: scrim plus figure.shot.big; Esc, a click on it or on the scrim closes. */
export function Lightbox() {
  const [shot, setShot] = useState<{ src: string; alt: string; caption: string } | null>(null);
  const fig = useRef<HTMLElement>(null);
  const back = useRef<Element | null>(null);
  useEffect(() => bus.on("shot", (p: { src: string; alt?: string; caption?: string }) => {
    if (!p?.src) return;
    back.current = document.activeElement;
    setShot({ src: p.src, alt: p.alt || "", caption: p.caption || "" });
  }), []);
  useEffect(() => {
    if (!shot) return;
    fig.current?.focus();
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setShot(null); };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      const b = back.current;
      if (b instanceof HTMLElement) b.focus();
    };
  }, [shot]);
  if (!shot) return null;
  const close = () => setShot(null);
  return (
    <>
      <div className="shot-scrim" onClick={close} />
      <figure className="shot big" role="dialog" aria-modal="true" aria-label={shot.alt || "Image"} tabIndex={-1} ref={fig} onClick={close}>
        <img src={shot.src} alt={shot.alt} />
        {shot.caption ? <Raw as="figcaption" html={shot.caption} /> : null}
      </figure>
    </>
  );
}

export function Overlays() {
  return (
    <>
      <Guard name="reference card"><RefPop /></Guard>
      <Guard name="reference panel"><RefPanel /></Guard>
      <Guard name="hotspot card"><HsPop /></Guard>
      <Guard name="sizing tooltip"><TipPop /></Guard>
      <Guard name="lightbox"><Lightbox /></Guard>
      <Guard name="schema drawer"><SchemaDrawer /></Guard>
    </>
  );
}
