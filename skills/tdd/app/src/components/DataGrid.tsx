// DataGrid: the D7 table card drawn the Snowsight way (D14a). Three tabs: "Data preview" (a compact mono grid with row
// numbers, a sticky typed header carrying each column's D7 change chip, a stats row, sort, filter and Copy TSV),
// "Columns" (one row per column: name, change, note, type with the struck-through `was`, and statistics), "Details"
// (the table's brief, TableBrief.tsx: purpose, measures, trade-offs, place and impact; then the table facts, DDL,
// indexes, notes, where the table appears). The tab bar sits
// in the drawer head beside the table name (D26); a grid wider than the drawer is laid out in stacked bands of columns
// (same row numbers, sticky, on every band), and the drawer's Expand view shows it as one band that scrolls sideways.
// Column stats (D30) are one small badge per header, not a row: hover or focus shows a popover with "x% null ·
// n distinct", min and max where the kind has them, one small distribution bar and "from the N sample rows, not the
// database"; a click (Enter, Space, a tap) opens the same content as a dropdown under the header that stays until Esc,
// an outside click or a second click. One at a time.
// Every statistic is computed here from sample_rows, deterministically, and labelled "from sample rows"; none is a
// measurement. Rendered only in the browser (the drawer is empty on the server), but render stays pure: no Date,
// random or window during render. The Snowsight reading behind this is in references/data-view.md.
import { Fragment, useEffect, useId, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent, ReactNode, RefObject } from "react";
import { cx } from "../lib/raw";
import { place } from "../lib/dom";
import type { Column, SchemaTable } from "../types";
import { TableBrief } from "./TableBrief";

export type Where = { id: string; kind: string; title: string };
export type TabKey = "preview" | "columns" | "details";

const TRUNC = 40;
const BINS = 5;

/* ------------------------------------------------------------------------------------------------ types and kinds */

export type Kind = "num" | "text" | "json" | "array" | "uuid" | "date" | "time" | "bool" | "unknown";

/** The kind a declared column type names; null when the JSON states no type. */
export function kindOfType(type: string | undefined): Kind | null {
  const s = (type || "").toLowerCase();
  if (!s.trim()) return null;
  if (/\bjsonb?\b/.test(s)) return "json";
  if (/\[\]|\barray\b/.test(s)) return "array";
  if (/\buuid\b/.test(s)) return "uuid";
  if (/\b(timestamptz|timestamp|datetime|time)\b/.test(s)) return "time";
  if (/\bdate\b/.test(s)) return "date";
  if (/\b(bool|boolean)\b/.test(s)) return "bool";
  if (/\b(int|int2|int4|int8|integer|bigint|smallint|serial|bigserial|numeric|decimal|real|double|float|number|money)\b/.test(s)) return "num";
  if (/\b(text|varchar|char|character|citext|enum|string)\b/.test(s)) return "text";
  return "unknown";
}

const ISO = /^\d{4}-\d{2}-\d{2}(?:[ T](\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?)(?:Z|[+-]\d{2}(?::?\d{2})?)?)?$/;

/** The kind the sample values themselves show (what the statistics are computed as). */
function valueKind(vals: unknown[], declared: Kind | null): Kind {
  if (!vals.length) return declared || "unknown";
  if (vals.every((v) => typeof v === "number" && Number.isFinite(v))) return "num";
  if (declared === "num" && vals.every((v) => (typeof v === "number" || typeof v === "string") && v !== "" && Number.isFinite(Number(v)))) return "num";
  if (vals.every((v) => typeof v === "boolean")) return "bool";
  if (vals.every((v) => Array.isArray(v))) return "array";
  if (vals.every((v) => v !== null && typeof v === "object")) return "json";
  if (vals.every((v) => typeof v === "string" && ISO.test(v))) return vals.some((v) => /\d{2}:\d{2}/.test(v as string)) ? "time" : "date";
  return "text";
}

/** Milliseconds for an ISO-ish date or timestamp ("2026-11-12 14:02:11+00" included); NaN when it will not parse. */
function isoMs(s: string) {
  let x = s.trim().replace(" ", "T");
  x = x.replace(/([+-]\d{2})$/, "$1:00").replace(/([+-]\d{2})(\d{2})$/, "$1:$2");
  if (/^\d{4}-\d{2}-\d{2}$/.test(x)) x += "T00:00:00Z";
  return Date.parse(x);
}

/** The header glyph for a column: from its declared type only ("?" when the JSON states none). */
export function Glyph({ kind }: { kind: Kind | null }) {
  const k = kind || "none";
  const title = kind ? ({ num: "number", text: "text", json: "JSON", array: "array", uuid: "uuid", date: "date", time: "timestamp", bool: "boolean", unknown: "other type" } as Record<string, string>)[k] : "type not stated";
  let body: ReactNode;
  if (k === "date" || k === "time") {
    body = (
      <svg viewBox="0 0 12 12" width="11" height="11" aria-hidden="true">
        <rect x="1.5" y="2.5" width="9" height="8" rx="1.2" fill="none" stroke="currentColor" strokeWidth="1.2" />
        <path d="M1.5 5h9M4 1.2v2.4M8 1.2v2.4" stroke="currentColor" strokeWidth="1.2" />
        {k === "time" ? <circle cx="8.6" cy="8.6" r="2.4" fill="var(--surface-2)" stroke="currentColor" strokeWidth="1" /> : null}
      </svg>
    );
  } else {
    body = ({ num: "#", text: "Aa", json: "{}", array: "[]", uuid: "id", bool: "01", unknown: "··", none: "?" } as Record<string, string>)[k];
  }
  return <span className={cx("dg-glyph", `g-${k}`)} title={title} aria-label={title} role="img">{body}</span>;
}

/* ------------------------------------------------------------------------------------------------ statistics */

export interface ColStat {
  key: string; n: number; nulls: number; distinct: number; kind: Kind;
  lo?: string; hi?: string; loLabel: "min" | "shortest"; hiLabel: "max" | "longest";
  bins?: number[]; top?: { v: string; c: number }[];
}

function text(v: unknown) {
  if (v === null || v === undefined) return "";
  return typeof v === "object" ? JSON.stringify(v) : String(v);
}

function fmtNum(x: number) {
  if (Number.isInteger(x)) return String(x);
  return String(Math.round(x * 1000) / 1000);
}

/** Statistics for one column key over the sample rows. Deterministic: ties keep row order. */
export function colStat(key: string, rows: Record<string, any>[], declared: Kind | null): ColStat {
  const all = rows.map((r) => (r ? r[key] : undefined));
  const vals = all.filter((v) => v !== null && v !== undefined);
  const kind = valueKind(vals, declared);
  const s: ColStat = { key, n: rows.length, nulls: all.length - vals.length, distinct: 0, kind, loLabel: "min", hiLabel: "max" };
  const counts = new Map<string, number>();
  for (const v of vals) { const t = text(v); counts.set(t, (counts.get(t) || 0) + 1); }
  s.distinct = counts.size;
  s.top = [...counts.entries()].map(([v, c], i) => ({ v, c, i })).sort((a, b) => b.c - a.c || a.i - b.i).map(({ v, c }) => ({ v, c }));
  if (!vals.length) return s;
  if (kind === "num") {
    const xs = vals.map(Number);
    const lo = Math.min(...xs), hi = Math.max(...xs);
    s.lo = fmtNum(lo); s.hi = fmtNum(hi);
    s.bins = binned(xs, lo, hi);
  } else if (kind === "date" || kind === "time") {
    const strs = vals.map(String);
    const ms = strs.map(isoMs);
    if (ms.every((x) => Number.isFinite(x))) {
      const lo = Math.min(...ms), hi = Math.max(...ms);
      s.lo = strs[ms.indexOf(lo)]; s.hi = strs[ms.indexOf(hi)];
      s.bins = binned(ms, lo, hi);
    } else {
      const sorted = [...strs].sort();
      s.lo = sorted[0]; s.hi = sorted[sorted.length - 1];
    }
  } else if (kind !== "bool") {
    s.loLabel = "shortest"; s.hiLabel = "longest";
    const ts = vals.map(text);
    let lo = 0, hi = 0;
    ts.forEach((t, i) => { if (t.length < ts[lo].length) lo = i; if (t.length > ts[hi].length) hi = i; });
    s.lo = ts[lo]; s.hi = ts[hi];
  }
  return s;
}

function binned(xs: number[], lo: number, hi: number) {
  const bins = new Array(BINS).fill(0);
  for (const x of xs) {
    const i = hi === lo ? Math.floor(BINS / 2) : Math.min(BINS - 1, Math.floor(((x - lo) / (hi - lo)) * BINS));
    bins[i]++;
  }
  return bins;
}

const pct = (a: number, b: number) => (b ? Math.round((a / b) * 100) : 0);

/** The distribution alone: a histogram (number, date, time) or a frequency bar of the most common values. */
export function Dist({ s }: { s: ColStat }) {
  const filled = s.n - s.nulls;
  let dist: ReactNode = null;
  if (s.bins) {
    const max = Math.max(...s.bins, 1);
    dist = (
      <span className="dg-hist" title={`histogram of ${filled} values in ${BINS} equal bins, from sample rows`}>
        {s.bins.map((b, i) => <i key={i} style={{ height: `${b ? Math.max(14, Math.round((b / max) * 100)) : 0}%` }} />)}
      </span>
    );
  } else if (s.top?.length) {
    const shown = s.top.slice(0, 4);
    const rest = filled - shown.reduce((a, t) => a + t.c, 0);
    dist = (
      <span className="dg-freq" title={`${s.distinct} distinct of ${filled} values, from sample rows`}>
        {shown.map((t, i) => <i key={i} className={`s${i}`} style={{ flexGrow: t.c }} title={`${short(t.v, 30)}: ${t.c}`} />)}
        {rest > 0 ? <i className="s-rest" style={{ flexGrow: rest }} /> : null}
      </span>
    );
  }
  return dist;
}

/** The mini graph (Columns tab): a filled meter, then the distribution. */
export function MiniStat({ s }: { s: ColStat }) {
  const fp = pct(s.n - s.nulls, s.n);
  return (
    <span className="dg-mini">
      <span className="dg-meter" title={`${fp}% filled, ${100 - fp}% NULL (from sample rows)`}><i style={{ width: `${fp}%` }} /></span>
      <Dist s={s} />
    </span>
  );
}

const STAT_ICON = <svg viewBox="0 0 12 12" aria-hidden="true"><path d="M1.5 10.5h9M2.8 9.2V6.2M5.2 9.2V2.6M7.6 9.2V4.6M10 9.2V7.4" /></svg>;

/** A header's stats badge (D30): a tiny bar-chart glyph, with a dot when the column has nulls. */
function StatBadge({ k, s, live, openKey, popId, on }: {
  k: string; s?: ColStat; live: boolean; openKey: string | null; popId: string; on?: StatHandlers;
}) {
  if (!s) return null;
  const cls = cx("dg-sb", s.nulls > 0 && "nulls", openKey === k && "on");
  if (!live || !on) return <span className={cls} aria-hidden="true">{STAT_ICON}</span>;
  return (
    <button type="button" className={cls} aria-label={`Column stats for ${k}: ${pct(s.nulls, s.n)}% null, ${s.distinct} distinct`}
      aria-expanded={openKey === k} aria-controls={popId}
      onPointerEnter={(e) => { if (e.pointerType !== "touch") on.enter(k, e.currentTarget); }}
      onPointerLeave={(e) => { if (e.pointerType !== "touch") on.leave(); }}
      onFocus={(e) => on.focus(k, e.currentTarget)} onBlur={() => on.leave()}
      onClick={(e) => on.click(k, e.currentTarget)}>
      {STAT_ICON}
    </button>
  );
}

interface StatHandlers { enter: (k: string, el: HTMLElement) => void; focus: (k: string, el: HTMLElement) => void; leave: () => void; click: (k: string, el: HTMLElement) => void }

/** The badge's popover and dropdown content. */
function StatPop({ k, s, c }: { k: string; s: ColStat; c?: Column }) {
  const kind = kindOfType(c?.type) || s.kind;
  return (
    <>
      <p className="dg-sp-h"><code>{k}</code><span>{c?.type ? baseTypeOf(c.type) : KIND_WORD[kind] || "type not stated"}</span></p>
      <p className="dg-sp-n"><b>{pct(s.nulls, s.n)}% null</b> · {s.distinct} distinct</p>
      {s.lo !== undefined && s.hi !== undefined ? (
        <p className="dg-sp-r"><span>{s.loLabel}</span> <code>{short(s.lo, 30)}</code><span className="sep"> · </span><span>{s.hiLabel}</span> <code>{short(s.hi, 30)}</code></p>
      ) : null}
      {s.n - s.nulls > 0 ? <span className="dg-sp-d"><Dist s={s} /></span> : null}
      <p className="dg-sp-note">From the {s.n} sample {s.n === 1 ? "row" : "rows"}, not the database.</p>
    </>
  );
}

const KIND_WORD: Record<string, string> = { num: "number", text: "text", json: "JSON", array: "array", uuid: "uuid", date: "date", time: "time", bool: "boolean" };
const baseTypeOf = (type: string) => type.split(/\s+(?=NOT\b|NULL\b|PRIMARY\b|DEFAULT\b|REFERENCES\b|UNIQUE\b|CHECK\b)/i)[0].trim();

function short(s: string, n: number) {
  return s.length > n ? s.slice(0, n - 1) + "…" : s;
}

/* ------------------------------------------------------------------------------------------------ shared helpers */

/** Plain text with `code` spans, rendered as React nodes (schema JSON is not run through the inline renderer). */
export function Ticks({ text: t }: { text?: string }) {
  if (!t) return null;
  const parts = t.split("`");
  return <>{parts.map((p, i) => (i % 2 ? <code key={i}>{p}</code> : <Fragment key={i}>{p}</Fragment>))}</>;
}

/** The grid's column keys: the table's columns (dropped ones left out) that the rows carry, then any other row key. */
export function gridKeys(t: SchemaTable): string[] {
  const rows = t.sample_rows || [];
  const seen = new Set<string>();
  rows.forEach((r) => Object.keys(r || {}).forEach((k) => seen.add(k)));
  const ordered = (t.columns || []).filter((c) => c.change !== "dropped" && seen.has(c.name)).map((c) => c.name);
  return [...ordered, ...[...seen].filter((k) => !ordered.includes(k))];
}

/** "synthetic" from "synthetic, shaped like the DDL; not production data" (the source's first clause). */
export function sourceTag(src?: string) {
  if (!src) return "source not stated";
  const first = src.split(/[,;.(]/)[0].trim();
  return first.length > 28 ? short(first, 28) : first || "source not stated";
}

function ChangeChip({ c, all }: { c?: Column; all: boolean }) {
  if (!c || all) return null;
  const ch = c.change || "unchanged";
  if (ch === "unchanged") return null;
  return <span className={cx("chip", ch)}>{ch}</span>;
}

/* ------------------------------------------------------------------------------------------------ the tabs */

export const TAB_ORDER: TabKey[] = ["preview", "columns", "details"];
const TAB_NAMES: Record<TabKey, string> = { preview: "Data preview", columns: "Columns", details: "Details" };

/** The tab bar. It sits in the drawer head beside the table name (D26: one row); the drawer owns the active tab. */
export function TabBar({ t, tab, go }: { t: SchemaTable; tab: TabKey; go: (k: TabKey) => void }) {
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  const counts: Record<TabKey, string> = { preview: String((t.sample_rows || []).length), columns: String((t.columns || []).length), details: "" };
  const onKey = (e: ReactKeyboardEvent<HTMLDivElement>) => {
    const i = TAB_ORDER.indexOf(tab);
    let j = -1;
    if (e.key === "ArrowRight") j = (i + 1) % TAB_ORDER.length;
    else if (e.key === "ArrowLeft") j = (i + TAB_ORDER.length - 1) % TAB_ORDER.length;
    else if (e.key === "Home") j = 0;
    else if (e.key === "End") j = TAB_ORDER.length - 1;
    if (j < 0) return;
    e.preventDefault();
    go(TAB_ORDER[j]);
    tabs.current[j]?.focus();
  };
  return (
    <div className="dg-tabs" role="tablist" aria-label={`${t.table} views`} onKeyDown={onKey}>
      {TAB_ORDER.map((k, i) => (
        <button key={k} type="button" role="tab" id={`dg-tab-${k}`} aria-controls={`dg-panel-${k}`} aria-selected={tab === k}
          tabIndex={tab === k ? 0 : -1} className={cx("dg-tab", tab === k && "on")} onClick={() => go(k)}
          ref={(el) => { tabs.current[i] = el; }}>
          {TAB_NAMES[k]}{counts[k] ? <span className="dg-count">{counts[k]}</span> : null}
        </button>
      ))}
    </div>
  );
}

/** The scrolling drawer body holding the three tab panels. `mode` "drawer" lays a wide grid out in stacked bands;
 *  "modal" (the Expand view, D26) keeps one band that scrolls sideways. `onBands` hears the band count. */
export function DataGrid({ t, tab, where, onWhere, bodyRef, mode, onBands }: {
  t: SchemaTable; tab: TabKey; where: { list: Where[]; more: number }; onWhere: () => void;
  bodyRef: RefObject<HTMLDivElement | null>; mode: GridMode; onBands?: (n: number) => void;
}) {
  const keys = useMemo(() => gridKeys(t), [t]);
  const byName = useMemo(() => new Map((t.columns || []).map((c) => [c.name, c])), [t]);
  const stats = useMemo(() => {
    const m = new Map<string, ColStat>();
    for (const k of keys) m.set(k, colStat(k, t.sample_rows || [], kindOfType(byName.get(k)?.type)));
    return m;
  }, [keys, t, byName]);
  return (
    <div className="drawer-body" ref={bodyRef}>
      <div role="tabpanel" id="dg-panel-preview" aria-labelledby="dg-tab-preview" hidden={tab !== "preview"} className="dg-panel">
        <Preview t={t} keys={keys} byName={byName} stats={stats} mode={mode} onBands={onBands} />
      </div>
      <div role="tabpanel" id="dg-panel-columns" aria-labelledby="dg-tab-columns" hidden={tab !== "columns"} className="dg-panel">
        <Columns t={t} stats={stats} />
      </div>
      <div role="tabpanel" id="dg-panel-details" aria-labelledby="dg-tab-details" hidden={tab !== "details"} className="dg-panel">
        <Details t={t} where={where} onWhere={onWhere} />
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------------------------------------ Data preview */

export type GridMode = "drawer" | "modal";
type Sort = { key: string; dir: 1 | -1 } | null;
type RowRef = { r: Record<string, any>; i: number };

function cmp(a: unknown, b: unknown) {
  const an = a === null || a === undefined, bn = b === null || b === undefined;
  if (an || bn) return an && bn ? 0 : an ? 1 : -1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  const x = text(a), y = text(b);
  return x < y ? -1 : x > y ? 1 : 0;
}

/** Band starts for column widths `ws` (natural, in px) in `avail` px with a row-number column of `rn` px: greedy, in
 *  column order, at least one column per band. [0] means the whole grid fits in one band. */
export function bandCuts(ws: number[], rn: number, avail: number): number[] {
  const cuts = [0];
  let acc = rn;
  ws.forEach((w, i) => {
    if (i > cuts[cuts.length - 1] && acc + w > avail) { cuts.push(i); acc = rn; }
    acc += w;
  });
  return cuts;
}

const sameCuts = (a: number[] | null, b: number[]) => !!a && a.length === b.length && a.every((x, i) => x === b[i]);

function Preview({ t, keys, byName, stats, mode, onBands }: {
  t: SchemaTable; keys: string[]; byName: Map<string, Column>; stats: Map<string, ColStat>; mode: GridMode; onBands?: (n: number) => void;
}) {
  const rows = t.sample_rows || [];
  const [q, setQ] = useState("");
  const [sort, setSort] = useState<Sort>(null);
  const [open, setOpen] = useState<Set<string>>(() => new Set());
  const [stat, setStat] = useState<{ key: string; open: boolean } | null>(null);
  const statAnchor = useRef<HTMLElement | null>(null);
  const statPop = useRef<HTMLDivElement>(null);
  const previewRef = useRef<HTMLElement>(null);
  // under the badge, kept inside the preview's width (the drawer clips anything past its edge)
  const placeStat = () => {
    const pop = statPop.current, a = statAnchor.current, box = previewRef.current;
    if (!pop || !a) return;
    place(pop, a.getBoundingClientRect(), 6);
    if (!box) return;
    const pr = pop.getBoundingClientRect(), br = box.getBoundingClientRect();
    const shift = pr.right > br.right ? pr.right - br.right : pr.left < br.left ? pr.left - br.left : 0;
    if (shift) pop.style.left = `${parseFloat(pop.style.left || "0") - shift}px`;
  };
  const statTimer = useRef({ show: 0, hide: 0 });
  const popId = `dg-sp-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const [copied, setCopied] = useState("");
  const [cuts, setCuts] = useState<number[] | null>(null);
  const [hot, setHot] = useState(-1);
  const wrap = useRef<HTMLDivElement>(null);
  const ghost = useRef<HTMLTableElement>(null);
  const banded = mode === "drawer";

  const shown = useMemo(() => {
    const needle = q.trim().toLowerCase();
    let list: RowRef[] = rows.map((r, i) => ({ r: r || {}, i }));
    if (needle) list = list.filter(({ r }) => keys.some((k) => text(r[k]).toLowerCase().includes(needle)));
    if (sort) list = [...list].sort((a, b) => sort.dir * cmp(a.r[sort.key], b.r[sort.key]) || a.i - b.i);
    return list;
  }, [rows, keys, q, sort]);

  // D26 bands: a hidden copy of the whole grid (.dg-ghost) gives every column's natural width; the visible grid is cut
  // into bands that fit the drawer's width. Re-measured when the drawer resizes, fonts land, or the rows change.
  useLayoutEffect(() => {
    if (!banded) return;
    const w = wrap.current, g = ghost.current;
    if (!w || !g) return;
    const measure = () => {
      const avail = w.clientWidth - 2;
      const head = g.querySelector("tr.dg-head");
      if (avail <= 0 || !head) return; // the panel is hidden or the dialog is closed: keep the last cut
      const cells = [...head.children] as HTMLElement[];
      const ws = cells.map((c) => c.getBoundingClientRect().width);
      const next = bandCuts(ws.slice(1), ws[0] || 0, avail);
      setCuts((prev) => (sameCuts(prev, next) ? prev : next));
    };
    measure();
    if (typeof ResizeObserver === "undefined") return;
    const ro = new ResizeObserver(measure);
    ro.observe(w);
    ro.observe(g);
    return () => ro.disconnect();
  }, [banded, keys, shown, open]);

  const bands = banded && cuts && cuts.length > 1 ? cuts.map((s, i) => ({ s, keys: keys.slice(s, cuts[i + 1] ?? keys.length) })) : [{ s: 0, keys }];
  useEffect(() => { if (banded) onBands?.(bands.length); }, [banded, bands.length, onBands]);

  // D30 stats badges: hover or focus shows the popover, a click toggles it as a dropdown that stays; one at a time
  const statOn: StatHandlers = {
    enter: (k, el) => {
      clearTimeout(statTimer.current.hide);
      clearTimeout(statTimer.current.show);
      statTimer.current.show = window.setTimeout(() => setStat((cur) => {
        if (cur?.open) return cur;
        statAnchor.current = el;
        return { key: k, open: false };
      }), 120);
    },
    focus: (k, el) => {
      clearTimeout(statTimer.current.hide);
      clearTimeout(statTimer.current.show);
      setStat((cur) => { if (cur?.open) return cur; statAnchor.current = el; return { key: k, open: false }; });
    },
    leave: () => {
      clearTimeout(statTimer.current.show);
      clearTimeout(statTimer.current.hide);
      statTimer.current.hide = window.setTimeout(() => setStat((cur) => (cur && !cur.open ? null : cur)), 150);
    },
    click: (k, el) => {
      clearTimeout(statTimer.current.show);
      clearTimeout(statTimer.current.hide);
      statAnchor.current = el;
      setStat((cur) => (cur?.open && cur.key === k ? null : { key: k, open: true }));
    },
  };
  useLayoutEffect(() => { if (stat) placeStat(); }, [stat]);
  useEffect(() => {
    if (!stat) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      e.preventDefault(); // the popover goes first; the drawer stays open
      e.stopPropagation();
      const back = stat.open ? statAnchor.current : null;
      setStat(null);
      back?.focus();
    };
    const onDown = (e: PointerEvent) => {
      const t = e.target as Node;
      if (statPop.current?.contains(t) || statAnchor.current?.contains(t)) return;
      setStat(null);
    };
    const onScroll = () => placeStat();
    document.addEventListener("keydown", onKey, true);
    if (stat.open) document.addEventListener("pointerdown", onDown, true);
    document.addEventListener("scroll", onScroll, true);
    return () => {
      document.removeEventListener("keydown", onKey, true);
      document.removeEventListener("pointerdown", onDown, true);
      document.removeEventListener("scroll", onScroll, true);
    };
  }, [stat]);
  useEffect(() => () => { clearTimeout(statTimer.current.show); clearTimeout(statTimer.current.hide); }, []);

  const toggleSort = (k: string) => setSort((s) => (!s || s.key !== k ? { key: k, dir: 1 } : s.dir === 1 ? { key: k, dir: -1 } : null));
  const toggleOpen = (id: string) => setOpen((o) => { const n = new Set(o); if (n.has(id)) n.delete(id); else n.add(id); return n; });

  const copy = async () => {
    const esc = (s: string) => s.replace(/[\t\r\n]+/g, " ");
    const tsv = [keys.join("\t"), ...shown.map(({ r }) => keys.map((k) => (r[k] === null || r[k] === undefined ? "NULL" : esc(text(r[k])))).join("\t"))].join("\n");
    let ok = false;
    try { await navigator.clipboard.writeText(tsv); ok = true; } catch {
      try {
        const ta = document.createElement("textarea");
        ta.value = tsv; ta.setAttribute("readonly", ""); ta.style.position = "fixed"; ta.style.opacity = "0";
        (document.querySelector("dialog.drawer[open]") || document.body).appendChild(ta);
        ta.select(); ok = document.execCommand("copy"); ta.remove();
      } catch { ok = false; }
    }
    setCopied(ok ? `Copied ${shown.length} ${shown.length === 1 ? "row" : "rows"}` : "Copy blocked by the browser");
    setTimeout(() => setCopied(""), 1800);
  };

  if (!rows.length) return <p className="none">No sample rows in schema/{t.table}.json.</p>;
  const grid = { t, byName, stats, shown, sort, toggleSort, open, toggleOpen, q, hot, setHot, statOn, openKey: stat?.open ? stat.key : null, popId };
  return (
    <section className="dg-preview" aria-label="Data preview" ref={previewRef}>
      <div className="dg-bar">
        <label className="dg-filter">
          <span className="sr-only">Filter rows</span>
          <input type="search" placeholder="Filter rows" value={q} onChange={(e) => setQ(e.target.value)} spellCheck={false} autoComplete="off" />
        </label>
        <span className="dg-shown">{q.trim() ? `${shown.length} of ${rows.length} rows` : `${rows.length} ${rows.length === 1 ? "row" : "rows"} · ${keys.length} columns`}{bands.length > 1 ? ` · ${bands.length} bands` : ""}</span>
        <span className="dg-actions">
          <button type="button" className="dg-btn" onClick={copy} disabled={!shown.length}>Copy TSV</button>
        </span>
        {copied ? <span className="dg-toast" role="status">{copied}</span> : null}
      </div>
      <div className={cx("dg-bands", bands.length > 1 && "multi")} ref={wrap}>
        {bands.map((b, bi) => (
          <div className="dg-band" key={b.s}>
            {bands.length > 1 ? (
              <p className="dg-band-cap">
                <b>{b.keys.length === 1 ? `Column ${b.s + 1}` : `Columns ${b.s + 1}–${b.s + b.keys.length}`}</b> of {keys.length}
                <span className="dg-band-names"> · {b.keys[0]}{b.keys.length > 1 ? ` … ${b.keys[b.keys.length - 1]}` : ""}</span>
              </p>
            ) : null}
            <div className={cx("dg-grid", bands.length > 1 && "banded")} tabIndex={0} role="region"
              aria-label={bands.length > 1 ? `${t.table} sample rows, columns ${b.s + 1} to ${b.s + b.keys.length} of ${keys.length}` : `${t.table} sample rows; scrolls sideways`}>
              <GridTable {...grid} keys={b.keys} band={bi} />
            </div>
          </div>
        ))}
        {banded ? (
          <div className="dg-ghost-box" aria-hidden="true">
            <div className="dg-ghost"><GridTable {...grid} keys={keys} band={-1} tref={ghost} /></div>
          </div>
        ) : null}
      </div>
      <div id={popId} ref={statPop} className={cx("dg-spop", stat && "show", stat?.open && "open")} role={stat?.open ? "dialog" : "tooltip"}
        aria-label={stat ? `Column stats: ${stat.key}` : undefined} hidden={!stat}
        onPointerEnter={() => clearTimeout(statTimer.current.hide)} onPointerLeave={() => { if (!stat?.open) statOn.leave(); }}>
        {stat && stats.get(stat.key) ? <StatPop k={stat.key} s={stats.get(stat.key)!} c={byName.get(stat.key)} /> : null}
      </div>
      <p className="dg-cap">
        <b>{rows.length} sample {rows.length === 1 ? "row" : "rows"} · {sourceTag(t.sample_source)}</b>
        <span> · column stats from sample rows, not the database. Source: {t.sample_source || "not stated"}</span>
      </p>
    </section>
  );
}

/** One band of the preview grid (band -1 is the hidden measuring copy): sticky typed header with stats badges, rows. */
function GridTable({ t, keys, byName, stats, shown, sort, toggleSort, open, toggleOpen, q, hot, setHot, band, tref, statOn, openKey, popId }: {
  t: SchemaTable; keys: string[]; byName: Map<string, Column>; stats: Map<string, ColStat>; shown: RowRef[]; sort: Sort;
  toggleSort: (k: string) => void; open: Set<string>; toggleOpen: (id: string) => void; q: string;
  statOn: StatHandlers; openKey: string | null; popId: string;
  hot: number; setHot: (i: number) => void; band: number; tref?: RefObject<HTMLTableElement | null>;
}) {
  const allNew = t.state === "new";
  const live = band >= 0;
  // a column whose cells are right-aligned numbers gets a right-aligned header, so the name sits over its values
  const numCol = (k: string) => {
    const vals = (t.sample_rows || []).map((r) => r?.[k]).filter((v) => v !== null && v !== undefined);
    return vals.length > 0 && vals.every((v) => typeof v === "number");
  };
  return (
    <table ref={tref}>
      <thead>
        <tr className="dg-head">
          <th className="rn" scope="col"><span className="sr-only">Row</span></th>
          {keys.map((k) => {
            const c = byName.get(k);
            const sorted = sort?.key === k ? (sort.dir === 1 ? "ascending" : "descending") : "none";
            return (
              <th key={k} scope="col" aria-sort={live ? sorted : undefined} className={cx(c && c.change !== "unchanged" && !allNew && `h-${c.change}`, numCol(k) && "num")}>
                <span className="dg-hc">
                  <button type="button" className="dg-h" onClick={live ? () => toggleSort(k) : undefined} tabIndex={live ? undefined : -1}
                    title={live ? `${k}: ${c?.type || "type not stated"}. Click to sort.` : undefined}>
                    <Glyph kind={kindOfType(c?.type)} />
                    <span className="nm">{k}</span>
                    <ChangeChip c={c} all={allNew} />
                    {sorted !== "none" ? <span className="dg-sort" aria-hidden="true">{sorted === "ascending" ? "▲" : "▼"}</span> : null}
                  </button>
                  <StatBadge k={k} s={stats.get(k)} live={live} openKey={openKey} popId={popId} on={live ? statOn : undefined} />
                </span>
              </th>
            );
          })}
        </tr>
      </thead>
      <tbody>
        {shown.map(({ r, i }) => (
          <tr key={i} className={cx(live && hot === i && "hot")}
            onPointerEnter={live ? () => setHot(i) : undefined} onPointerLeave={live ? () => setHot(-1) : undefined}>
            <th className="rn" scope="row" title={live ? `row ${i + 1} in schema/${t.table}.json` : undefined}>{i + 1}</th>
            {keys.map((k) => <Cell key={k} v={r[k]} id={`${i}:${k}`} open={open.has(`${i}:${k}`)} toggle={toggleOpen} live={live} />)}
          </tr>
        ))}
        {!shown.length && band === 0 ? <tr><td className="dg-empty" colSpan={keys.length + 1}>No sample row matches “{q.trim()}”.</td></tr> : null}
      </tbody>
    </table>
  );
}

function Cell({ v, id, open, toggle, live }: { v: unknown; id: string; open: boolean; toggle: (id: string) => void; live: boolean }) {
  if (v === null || v === undefined) return <td className="null">NULL</td>;
  const obj = typeof v === "object";
  const s = text(v);
  const num = typeof v === "number";
  if (s.length <= TRUNC) return <td className={cx(num && "num", obj && "js")}>{s}</td>;
  return (
    <td className={cx("long", obj && "js", open && "open")}>
      <button type="button" className="dg-more" aria-expanded={live ? open : undefined} onClick={live ? () => toggle(id) : undefined}
        tabIndex={live ? undefined : -1} title={live ? (open ? "Collapse" : "Show the whole value") : undefined}>
        {open ? <pre>{obj ? JSON.stringify(v, null, 2) : s}</pre> : <span>{s.slice(0, TRUNC - 1)}<span className="ell">…</span></span>}
      </button>
    </td>
  );
}

/* ------------------------------------------------------------------------------------------------ Columns */

function Columns({ t, stats }: { t: SchemaTable; stats: Map<string, ColStat> }) {
  const cols = t.columns || [];
  const allNew = t.state === "new";
  const kinds = new Set(cols.map((c) => c.change || "unchanged"));
  const n = (t.sample_rows || []).length;
  const listed = new Set(cols.map((c) => c.name));
  const extra = [...stats.keys()].filter((k) => !listed.has(k));
  return (
    <section className="dg-columns" aria-label="Columns">
      <p className="schema-legend">
        {allNew ? (
          <span><i className="lg new" />New table: all {cols.length} columns are new.</span>
        ) : (
          <>
            {kinds.has("new") ? <span><i className="lg new" />new column</span> : null}
            {kinds.has("changed") ? <span><i className="lg changed" />changed; the old definition is struck through</span> : null}
            {kinds.has("dropped") ? <span><i className="lg dropped" />dropped</span> : null}
            {kinds.has("unchanged") ? <span><i className="lg" />unchanged</span> : null}
            {!kinds.has("new") && !kinds.has("changed") && !kinds.has("dropped") ? <span className="muted">No column changes in this design.</span> : null}
          </>
        )}
      </p>
      <div className="tbl dg-cols">
        <table>
          <thead>
            <tr><th>Column</th><th>Type</th><th className="num">Nulls</th><th className="num">Distinct</th><th>Min / max</th><th>Distribution</th></tr>
          </thead>
          <tbody>
            {cols.map((c, i) => <ColRow key={i} c={c} chip={!allNew} s={c.change === "dropped" ? undefined : stats.get(c.name)} />)}
            {extra.map((k) => <ColRow key={`x-${k}`} c={{ name: k, type: "", change: "unchanged", note: "in the sample rows only; not in the columns list" }} chip={false} s={stats.get(k)} />)}
          </tbody>
        </table>
      </div>
      <p className="dg-cap">Nulls, distinct, min / max and distribution are computed from the {n} sample {n === 1 ? "row" : "rows"} in <code>schema/{t.table}.json</code>, not from the database.</p>
    </section>
  );
}

function ColRow({ c, chip, s }: { c: Column; chip: boolean; s?: ColStat }) {
  const ch = c.change || "unchanged";
  const range = s && s.lo !== undefined ? (
    <span className="dg-range">
      <span><i>{s.loLabel}</i> <code title={s.lo}>{short(s.lo, 24)}</code></span>
      <span><i>{s.hiLabel}</i> <code title={s.hi}>{short(s.hi || "", 24)}</code></span>
    </span>
  ) : s && s.kind === "bool" && s.top?.length ? (
    <span className="dg-range">{s.top.map((x) => <span key={x.v}><code>{x.v}</code> {x.c}</span>)}</span>
  ) : <span className="none">{s ? "no values" : ch === "dropped" ? "dropped" : "not in sample rows"}</span>;
  return (
    <tr className={cx(ch !== "unchanged" && `col-${ch}`)}>
      <td className="cn">
        <span className="cn-h">
          <Glyph kind={kindOfType(c.type)} />
          {ch === "dropped" ? <del><code>{c.name}</code></del> : <code>{c.name}</code>}
          {chip && ch === "new" ? <span className="chip new">new</span> : null}
          {chip && ch === "changed" ? <span className="chip changed">changed</span> : null}
          {ch === "dropped" ? <span className="chip dropped">dropped</span> : null}
        </span>
        {c.note ? <span className="cnote"><Ticks text={c.note} /></span> : null}
      </td>
      <td className="ct">
        {c.type ? <span>{c.type}</span> : <span className="none">not stated</span>}
        {c.was ? <span className="was">was <del>{c.was}</del></span> : null}
      </td>
      <td className="num" data-l="Nulls">{s ? `${pct(s.nulls, s.n)}%` : "—"}</td>
      <td className="num" data-l="Distinct">{s ? s.distinct : "—"}</td>
      <td className="rg">{range}</td>
      <td className="ds">{s && s.n ? <MiniStat s={s} /> : null}</td>
    </tr>
  );
}

/* ------------------------------------------------------------------------------------------------ Details */

function Details({ t, where, onWhere }: { t: SchemaTable; where: { list: Where[]; more: number }; onWhere: () => void }) {
  const n = (t.sample_rows || []).length;
  return (
    <div className="dg-details">
      <TableBrief t={t} mode="drawer" />
      <section className="drawer-sec">
        <h3>Table</h3>
        <dl className="dg-kv">
          <div><dt>Name</dt><dd><code>{t.table}</code></dd></div>
          <div><dt>In this design</dt><dd>{t.state === "new" ? "new table" : t.state === "changed" ? "changed" : "existing, unchanged"}</dd></div>
          <div><dt>Columns</dt><dd>{(t.columns || []).length}</dd></div>
          <div><dt>Sample rows</dt><dd>{n} · {sourceTag(t.sample_source)}</dd></div>
        </dl>
      </section>
      {t.ddl ? (
        <section className="drawer-sec">
          <h3>DDL</h3>
          <pre className="ddl"><code>{t.ddl}</code></pre>
        </section>
      ) : null}
      {t.indexes?.length ? (
        <section className="drawer-sec">
          <h3>Indexes and constraints</h3>
          <ul className="mono-list">{t.indexes.map((x, i) => <li key={i}>{x}</li>)}</ul>
        </section>
      ) : null}
      {t.notes?.length ? (
        <section className="drawer-sec">
          <h3>Notes</h3>
          <ul className="drawer-notes">{t.notes.map((x, i) => <li key={i}><Ticks text={x} /></li>)}</ul>
        </section>
      ) : null}
      <section className="drawer-sec">
        <h3>Where it appears</h3>
        {where.list.length ? (
          <ul className="where" onClick={(e) => { if ((e.target as HTMLElement).closest?.("a.ref")) onWhere(); }}>
            {where.list.map((w) => (
              <li key={w.id}><a className="ref" data-ref={w.id} href={`#${w.id}`}>{w.title || w.id}</a> <span className="kind">{w.kind}</span></li>
            ))}
            {where.more ? <li className="more">and {where.more} more</li> : null}
          </ul>
        ) : <p className="none">No anchored block on the page names <code>{t.table}</code>.</p>}
      </section>
    </div>
  );
}

