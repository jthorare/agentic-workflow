// The D7 schema drawer: a right-side <dialog class="drawer schema"> that opens on bus event "table" { id } and shows one
// table from content.schema the Snowsight way (D14a, DataGrid.tsx): one head row (D26) holding the table name, its
// state chip, the tabs "Data preview" (typed grid of the sample rows with column stats), "Columns" (change per column,
// struck-through `was`, stats from sample rows) and "Details" (summary, measured and expected load, DDL, indexes, notes,
// where the table appears), Expand and Close; with a docked "Ask about this table" chat (D14c, DataChat.tsx) that
// lights up only where claude.use("sample") resolves. A grid wider than the drawer is drawn in stacked bands; Expand
// (shown only then) turns the same dialog into a landscape modal at 95% of the page (class "expanded": tabs, one band
// that scrolls sideways, the chat), so the tab, filter and chat state carry over. Renders closed and empty on the
// server (deterministic); it fills and opens only in the browser. An absent measure reads "not measured" (or "not
// estimated"), never a number. Open: bus "table" {id} or a #tbl-… hash; close: the Close button, Esc, a click outside
// (closedby="any" or the backdrop fallback). In the expanded view those first return to the drawer.
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { MouseEvent as ReactMouseEvent, RefObject, SyntheticEvent } from "react";
import { bus } from "../lib/bus";
import { useContent } from "../lib/context";
import { cx } from "../lib/raw";
import type { Content, SchemaTable } from "../types";
import { DataGrid, TabBar } from "./DataGrid";
import type { TabKey, Where } from "./DataGrid";
import { DataChat } from "./DataChat";

const STATE_CHIP: Record<string, string> = { new: "new", changed: "changed", existing: "" };
const WHERE_MAX = 12;

/** A schema table by its id ("tbl-hold-ranking") or by its table name ("hold_ranking"). */
export function findTable(tables: SchemaTable[], key: string | null | undefined): SchemaTable | null {
  if (!key) return null;
  return tables.find((t) => t.id === key) || tables.find((t) => t.table === key) || null;
}

/** Where the table is named on the page: every anchor (an id with a definition) whose own content, up to the next
 *  nested anchor, names the table, in document order. Its own table definition is left out. */
export function tableRefs(content: Content, t: SchemaTable): { list: Where[]; more: number } {
  const name = t.table.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const re = new RegExp(`(^|[^A-Za-z0-9_])${name}(?![A-Za-z0-9_])`);
  const defs = content.defs || {};
  const hits: string[] = [];
  const seen = new Set<string>();
  const visit = (n: any, anchor: string | null) => {
    if (n == null) return;
    if (typeof n === "string") {
      if (anchor && !seen.has(anchor) && re.test(n)) { seen.add(anchor); hits.push(anchor); }
      return;
    }
    if (Array.isArray(n)) { for (const x of n) visit(x, anchor); return; }
    if (typeof n !== "object") return;
    const a = typeof n.id === "string" && defs[n.id] ? n.id : anchor;
    for (const k in n) if (k !== "id" && k !== "src" && k !== "key" && k !== "table") visit(n[k], a);
  };
  visit(content.lede, null);
  visit(content.sections, null);
  const all: Where[] = hits.filter((id) => defs[id] && defs[id].kind !== "table").map((id) => ({ id, kind: defs[id].kind, title: cleanTitle(defs[id].title) }));
  return { list: all.slice(0, WHERE_MAX), more: Math.max(0, all.length - WHERE_MAX) };
}

/** Def titles are tag-stripped source text: drop markdown backticks and [chip:tone text] markers for display. */
function cleanTitle(s: string) {
  return (s || "").replace(/\[chip:[\w-]+\s+[^\]]*\]/g, "").replace(/`/g, "").replace(/\s+/g, " ").trim();
}

export function SchemaDrawer() {
  const content = useContent();
  const tables = content?.schema || [];
  const [id, setId] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);
  const [expanded, setExpanded] = useState(false);
  const dlg = useRef<HTMLDialogElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const t = findTable(tables, id);

  // Listen for "table" { id } and for a #tbl-… hash (a deep link or a hotspot's fallback href).
  useEffect(() => {
    const open = (p: { id?: string } | undefined) => {
      const hit = findTable(tables, p?.id);
      if (!hit) return;
      setId(hit.id);
      setNonce((n) => n + 1);
    };
    const fromHash = () => {
      let h = "";
      try { h = decodeURIComponent(location.hash.slice(1)); } catch { h = location.hash.slice(1); }
      if (h && findTable(tables, h)) open({ id: h });
    };
    const off = bus.on("table", open);
    window.addEventListener("hashchange", fromHash);
    fromHash();
    return () => { off(); window.removeEventListener("hashchange", fromHash); };
  }, [tables]);

  // Open after the table's content has rendered; a second "table" while open just swaps the content. showModal
  // focuses the first focusable control (the active tab, now that the tabs share the head); Close takes it instead.
  useEffect(() => {
    const d = dlg.current;
    if (!nonce || !d) return;
    if (!d.open) {
      try { d.showModal(); } catch { d.setAttribute("open", ""); }
      d.querySelector<HTMLButtonElement>(".drawer-close")?.focus();
    }
    if (body.current) body.current.scrollTop = 0;
  }, [nonce]);

  const close = useCallback(() => { dlg.current?.close(); }, []);
  const expand = useCallback(() => {
    setExpanded(true);
    requestAnimationFrame(() => dlg.current?.querySelector<HTMLButtonElement>(".drawer-close")?.focus());
  }, []);
  const collapse = useCallback(() => {
    setExpanded(false);
    requestAnimationFrame(() => {
      const d = dlg.current;
      (d?.querySelector<HTMLButtonElement>(".drawer-expand") || d?.querySelector<HTMLButtonElement>(".drawer-close"))?.focus();
    });
  }, []);

  // Esc and light dismiss arrive as "cancel": in the expanded view they return to the drawer instead of closing it.
  const onCancel = useCallback((e: SyntheticEvent<HTMLDialogElement>) => {
    if (!expanded) return;
    e.preventDefault();
    collapse();
  }, [expanded, collapse]);

  // closedby="any" gives light dismiss where supported; this is the fallback: a click that lands on the dialog element
  // itself outside its box is a click on the backdrop.
  const onDialogClick = useCallback((e: ReactMouseEvent<HTMLDialogElement>) => {
    const d = dlg.current;
    if (!d || e.target !== d) return;
    const r = d.getBoundingClientRect();
    const inside = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    if (inside) return;
    if (expanded) collapse();
    else d.close();
  }, [expanded, collapse]);

  const where = useMemo(() => (t ? tableRefs(content, t) : { list: [], more: 0 }), [content, t]);

  return (
    <dialog ref={dlg} className={cx("drawer schema", expanded && "expanded")} closedby="any" onClick={onDialogClick}
      onCancel={onCancel} onClose={() => setExpanded(false)}
      aria-labelledby={t ? "schema-drawer-title" : undefined} aria-label={t ? undefined : "Table details"}>
      {t ? <TableCard key={t.id} t={t} where={where} close={close} bodyRef={body} expanded={expanded} expand={expand} collapse={collapse} /> : null}
    </dialog>
  );
}

const STATE_TEXT: Record<string, string> = { new: "new in this design", changed: "changed by this design", existing: "existing, unchanged" };

function TableCard({ t, where, close, bodyRef, expanded, expand, collapse }: {
  t: SchemaTable; where: { list: Where[]; more: number }; close: () => void; bodyRef: RefObject<HTMLDivElement | null>;
  expanded: boolean; expand: () => void; collapse: () => void;
}) {
  const chip = STATE_CHIP[t.state] ?? "";
  const [tab, setTab] = useState<TabKey>("preview");
  const [bands, setBands] = useState(1);
  const go = useCallback((k: TabKey) => {
    setTab(k);
    if (bodyRef.current) bodyRef.current.scrollTop = 0;
  }, [bodyRef]);
  return (
    <>
      <header className="drawer-head">
        <h2 id="schema-drawer-title" className="drawer-name" title={`${t.table}: table, ${STATE_TEXT[t.state] || t.state}`}>
          <code>{t.table}</code><span className={cx("chip", chip)}>{t.state}</span>
        </h2>
        <TabBar t={t} tab={tab} go={go} />
        <div className="drawer-acts">
          {!expanded && bands > 1 ? (
            <button type="button" className="drawer-expand" onClick={expand}
              title={`Show all ${(t.columns || []).length} columns in one band, at 95% of the page`}>
              <svg viewBox="0 0 16 16" width="13" height="13" aria-hidden="true"><path d="M9.5 2.5h4v4M13.5 2.5 9 7M6.5 13.5h-4v-4M2.5 13.5 7 9" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg>
              Expand
            </button>
          ) : null}
          <button type="button" className="drawer-close" onClick={expanded ? collapse : close}
            aria-label={expanded ? "Close the expanded view" : undefined}>Close</button>
        </div>
      </header>
      <DataGrid t={t} tab={tab} where={where} onWhere={close} bodyRef={bodyRef} mode={expanded ? "modal" : "drawer"} onBands={setBands} />
      <DataChat key={`chat-${t.id}`} t={t} />
    </>
  );
}
