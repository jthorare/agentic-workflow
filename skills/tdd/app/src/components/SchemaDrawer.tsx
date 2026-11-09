// The D7 schema drawer: a right-side <dialog class="drawer schema"> that opens on bus event "table" { id } and shows one
// table from content.schema the Snowsight way (D14a, DataGrid.tsx): tabs "Data preview" (typed grid of the sample rows
// with column stats), "Columns" (change per column, struck-through `was`, stats from sample rows) and "Details"
// (measured and expected load, DDL, indexes, notes, where the table appears), with a docked "Ask about this table" chat
// (D14c, DataChat.tsx) that lights up only where claude.use("sample") resolves. Renders closed and empty on the server
// (deterministic); it fills and opens only in the browser. An absent measure reads "not measured" (or "not estimated"),
// never a number. Open: bus "table" {id} or a #tbl-… hash; close: the Close button, Esc, a click outside (closedby="any"
// or the backdrop fallback).
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { MouseEvent as ReactMouseEvent, RefObject } from "react";
import { bus } from "../lib/bus";
import { useContent } from "../lib/context";
import { cx } from "../lib/raw";
import type { Content, SchemaTable } from "../types";
import { DataGrid, Ticks } from "./DataGrid";
import type { Where } from "./DataGrid";
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

  // Open after the table's content has rendered; a second "table" while open just swaps the content.
  useEffect(() => {
    const d = dlg.current;
    if (!nonce || !d) return;
    if (!d.open) {
      try { d.showModal(); } catch { d.setAttribute("open", ""); }
    }
    if (body.current) body.current.scrollTop = 0;
  }, [nonce]);

  const close = useCallback(() => { dlg.current?.close(); }, []);

  // closedby="any" gives light dismiss where supported; this is the fallback: a click that lands on the dialog element
  // itself outside its box is a click on the backdrop.
  const onDialogClick = useCallback((e: ReactMouseEvent<HTMLDialogElement>) => {
    const d = dlg.current;
    if (!d || e.target !== d) return;
    const r = d.getBoundingClientRect();
    const inside = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    if (!inside) d.close();
  }, []);

  const where = useMemo(() => (t ? tableRefs(content, t) : { list: [], more: 0 }), [content, t]);

  return (
    <dialog ref={dlg} className="drawer schema" closedby="any" onClick={onDialogClick}
      aria-labelledby={t ? "schema-drawer-title" : undefined} aria-label={t ? undefined : "Table details"}>
      {t ? <TableCard t={t} where={where} close={close} bodyRef={body} /> : null}
    </dialog>
  );
}

function TableCard({ t, where, close, bodyRef }: { t: SchemaTable; where: { list: Where[]; more: number }; close: () => void; bodyRef: RefObject<HTMLDivElement | null> }) {
  const chip = STATE_CHIP[t.state] ?? "";
  return (
    <>
      <header className="drawer-head">
        <div className="drawer-title">
          <span className="eyebrow">Table · {t.state === "new" ? "new in this design" : t.state === "changed" ? "changed by this design" : "existing, unchanged"}</span>
          <h2 id="schema-drawer-title"><code>{t.table}</code><span className={cx("chip", chip)}>{t.state}</span></h2>
        </div>
        <button type="button" className="drawer-close" onClick={close}>Close</button>
      </header>
      <DataGrid key={t.id} t={t} where={where} onWhere={close} bodyRef={bodyRef}
        lead={t.summary ? <p className="drawer-summary"><Ticks text={t.summary} /></p> : null} />
      <DataChat key={`chat-${t.id}`} t={t} />
    </>
  );
}
