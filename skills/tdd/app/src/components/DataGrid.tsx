// DataGrid: the D7 table card drawn the Snowsight way (D14a). Three tabs: "Data preview" (a compact mono grid with row
// numbers, a sticky typed header carrying each column's D7 change chip, a stats row, sort, filter and Copy TSV),
// "Columns" (one row per column: name, change, note, type with the struck-through `was`, and statistics), "Details"
// (the D7 measured and expected load cards, DDL, indexes, notes, where the table appears). Every statistic is computed
// here from sample_rows, deterministically, and labelled "from sample rows"; none is a measurement. Rendered only in
// the browser (the drawer is empty on the server), but render stays pure: no Date, random or window during render.
// The Snowsight reading behind this is in references/data-view.md.
import { Fragment, useMemo, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent, ReactNode, RefObject } from "react";
import { cx } from "../lib/raw";
import type { Column, Measure, SchemaTable } from "../types";

export type Where = { id: string; kind: string; title: string };
type M = Measure & { label?: string };
export type TabKey = "preview" | "columns" | "details";

/** Rows every load card shows, measured or not; any other key in the JSON follows them in file order. */
const CORE = ["reads", "writes", "rows", "storage"];
const LABELS: Record<string, string> = { reads: "Reads", writes: "Writes", rows: "Rows", storage: "Storage", p50: "p50", p95: "p95", p99: "p99" };
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

/** The mini graph: a filled meter, then a histogram (number, date, time) or a frequency bar (anything else). */
export function MiniStat({ s }: { s: ColStat }) {
  const filled = s.n - s.nulls;
  const fp = pct(filled, s.n);
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
  return (
    <span className="dg-mini">
      <span className="dg-meter" title={`${fp}% filled, ${100 - fp}% NULL (from sample rows)`}><i style={{ width: `${fp}%` }} /></span>
      {dist}
    </span>
  );
}

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

function label(key: string, m?: M) {
  if (m?.label) return m.label;
  if (LABELS[key]) return LABELS[key];
  const s = key.replace(/_/g, " ");
  return s.charAt(0).toUpperCase() + s.slice(1);
}

function hasValue(m?: M) {
  return !!m && m.value !== undefined && m.value !== null && m.value !== "";
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

/** The tab bar (fixed under the drawer head) and the scrolling drawer body holding `lead` and the active panel. */
export function DataGrid({ t, where, onWhere, bodyRef, lead }: {
  t: SchemaTable; where: { list: Where[]; more: number }; onWhere: () => void;
  bodyRef: RefObject<HTMLDivElement | null>; lead?: ReactNode;
}) {
  const [tab, setTab] = useState<TabKey>("preview");
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  const rows = t.sample_rows || [];
  const keys = useMemo(() => gridKeys(t), [t]);
  const byName = useMemo(() => new Map((t.columns || []).map((c) => [c.name, c])), [t]);
  const stats = useMemo(() => {
    const m = new Map<string, ColStat>();
    for (const k of keys) m.set(k, colStat(k, t.sample_rows || [], kindOfType(byName.get(k)?.type)));
    return m;
  }, [keys, t, byName]);
  const order: TabKey[] = ["preview", "columns", "details"];
  const names: Record<TabKey, string> = { preview: "Data preview", columns: "Columns", details: "Details" };
  const counts: Record<TabKey, string> = { preview: String(rows.length), columns: String((t.columns || []).length), details: "" };
  const go = (k: TabKey) => {
    setTab(k);
    if (bodyRef.current) bodyRef.current.scrollTop = 0;
  };
  const onKey = (e: ReactKeyboardEvent<HTMLDivElement>) => {
    const i = order.indexOf(tab);
    let j = -1;
    if (e.key === "ArrowRight") j = (i + 1) % order.length;
    else if (e.key === "ArrowLeft") j = (i + order.length - 1) % order.length;
    else if (e.key === "Home") j = 0;
    else if (e.key === "End") j = order.length - 1;
    if (j < 0) return;
    e.preventDefault();
    go(order[j]);
    tabs.current[j]?.focus();
  };
  return (
    <>
      <div className="dg-tabs" role="tablist" aria-label={`${t.table} views`} onKeyDown={onKey}>
        {order.map((k, i) => (
          <button key={k} type="button" role="tab" id={`dg-tab-${k}`} aria-controls={`dg-panel-${k}`} aria-selected={tab === k}
            tabIndex={tab === k ? 0 : -1} className={cx("dg-tab", tab === k && "on")} onClick={() => go(k)}
            ref={(el) => { tabs.current[i] = el; }}>
            {names[k]}{counts[k] ? <span className="dg-count">{counts[k]}</span> : null}
          </button>
        ))}
      </div>
      <div className="drawer-body" ref={bodyRef}>
        {lead}
        <div role="tabpanel" id="dg-panel-preview" aria-labelledby="dg-tab-preview" hidden={tab !== "preview"} className="dg-panel">
          <Preview t={t} keys={keys} byName={byName} stats={stats} />
        </div>
        <div role="tabpanel" id="dg-panel-columns" aria-labelledby="dg-tab-columns" hidden={tab !== "columns"} className="dg-panel">
          <Columns t={t} stats={stats} />
        </div>
        <div role="tabpanel" id="dg-panel-details" aria-labelledby="dg-tab-details" hidden={tab !== "details"} className="dg-panel">
          <Details t={t} where={where} onWhere={onWhere} />
        </div>
      </div>
    </>
  );
}

/* ------------------------------------------------------------------------------------------------ Data preview */

type Sort = { key: string; dir: 1 | -1 } | null;

function cmp(a: unknown, b: unknown) {
  const an = a === null || a === undefined, bn = b === null || b === undefined;
  if (an || bn) return an && bn ? 0 : an ? 1 : -1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  const x = text(a), y = text(b);
  return x < y ? -1 : x > y ? 1 : 0;
}

function Preview({ t, keys, byName, stats }: { t: SchemaTable; keys: string[]; byName: Map<string, Column>; stats: Map<string, ColStat> }) {
  const rows = t.sample_rows || [];
  const [q, setQ] = useState("");
  const [sort, setSort] = useState<Sort>(null);
  const [open, setOpen] = useState<Set<string>>(() => new Set());
  const [showStats, setShowStats] = useState(true);
  const [copied, setCopied] = useState("");
  const allNew = t.state === "new";

  const shown = useMemo(() => {
    const needle = q.trim().toLowerCase();
    let list = rows.map((r, i) => ({ r: r || {}, i }));
    if (needle) list = list.filter(({ r }) => keys.some((k) => text(r[k]).toLowerCase().includes(needle)));
    if (sort) list = [...list].sort((a, b) => sort.dir * cmp(a.r[sort.key], b.r[sort.key]) || a.i - b.i);
    return list;
  }, [rows, keys, q, sort]);

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
  return (
    <section className="dg-preview" aria-label="Data preview">
      <div className="dg-bar">
        <label className="dg-filter">
          <span className="sr-only">Filter rows</span>
          <input type="search" placeholder="Filter rows" value={q} onChange={(e) => setQ(e.target.value)} spellCheck={false} autoComplete="off" />
        </label>
        <span className="dg-shown">{q.trim() ? `${shown.length} of ${rows.length} rows` : `${rows.length} ${rows.length === 1 ? "row" : "rows"} · ${keys.length} columns`}</span>
        <span className="dg-actions">
          <button type="button" className={cx("dg-btn", showStats && "on")} aria-pressed={showStats} onClick={() => setShowStats((v) => !v)}>Column stats</button>
          <button type="button" className="dg-btn" onClick={copy} disabled={!shown.length}>Copy TSV</button>
        </span>
        {copied ? <span className="dg-toast" role="status">{copied}</span> : null}
      </div>
      <div className="dg-grid" tabIndex={0} role="region" aria-label={`${t.table} sample rows; scrolls sideways`}>
        <table>
          <thead>
            <tr className="dg-head">
              <th className="rn" scope="col"><span className="sr-only">Row</span></th>
              {keys.map((k) => {
                const c = byName.get(k);
                const sorted = sort?.key === k ? (sort.dir === 1 ? "ascending" : "descending") : "none";
                return (
                  <th key={k} scope="col" aria-sort={sorted} className={cx(c && c.change !== "unchanged" && !allNew && `h-${c.change}`)}>
                    <button type="button" className="dg-h" onClick={() => toggleSort(k)} title={`${k}: ${c?.type || "type not stated"}. Click to sort.`}>
                      <Glyph kind={kindOfType(c?.type)} />
                      <span className="nm">{k}</span>
                      <ChangeChip c={c} all={allNew} />
                      {sorted !== "none" ? <span className="dg-sort" aria-hidden="true">{sorted === "ascending" ? "▲" : "▼"}</span> : null}
                    </button>
                  </th>
                );
              })}
            </tr>
            {showStats ? (
              <tr className="dg-hstats">
                <th className="rn" scope="row"><span className="sr-only">Column stats from sample rows</span></th>
                {keys.map((k) => {
                  const s = stats.get(k)!;
                  return (
                    <th key={k} className="st">
                      <MiniStat s={s} />
                      <span className="dg-st-t">{pct(s.nulls, s.n)}% null · {s.distinct} distinct</span>
                    </th>
                  );
                })}
              </tr>
            ) : null}
          </thead>
          <tbody>
            {shown.map(({ r, i }) => (
              <tr key={i}>
                <th className="rn" scope="row" title={`row ${i + 1} in schema/${t.table}.json`}>{i + 1}</th>
                {keys.map((k) => <Cell key={k} v={r[k]} id={`${i}:${k}`} open={open.has(`${i}:${k}`)} toggle={toggleOpen} />)}
              </tr>
            ))}
            {!shown.length ? <tr><td className="dg-empty" colSpan={keys.length + 1}>No sample row matches “{q.trim()}”.</td></tr> : null}
          </tbody>
        </table>
      </div>
      <p className="dg-cap">
        <b>{rows.length} sample {rows.length === 1 ? "row" : "rows"} · {sourceTag(t.sample_source)}</b>
        <span> · column stats from sample rows, not the database. Source: {t.sample_source || "not stated"}</span>
      </p>
    </section>
  );
}

function Cell({ v, id, open, toggle }: { v: unknown; id: string; open: boolean; toggle: (id: string) => void }) {
  if (v === null || v === undefined) return <td className="null">NULL</td>;
  const obj = typeof v === "object";
  const s = text(v);
  const num = typeof v === "number";
  if (s.length <= TRUNC) return <td className={cx(num && "num", obj && "js")}>{s}</td>;
  return (
    <td className={cx("long", obj && "js", open && "open")}>
      <button type="button" className="dg-more" aria-expanded={open} onClick={() => toggle(id)} title={open ? "Collapse" : "Show the whole value"}>
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
      <section className="drawer-sec">
        <h3>Table</h3>
        <dl className="dg-kv">
          <div><dt>Name</dt><dd><code>{t.table}</code></dd></div>
          <div><dt>In this design</dt><dd>{t.state === "new" ? "new table" : t.state === "changed" ? "changed" : "existing, unchanged"}</dd></div>
          <div><dt>Columns</dt><dd>{(t.columns || []).length}</dd></div>
          <div><dt>Sample rows</dt><dd>{n} · {sourceTag(t.sample_source)}</dd></div>
        </dl>
      </section>
      <LoadSec t={t} />
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

function LoadSec({ t }: { t: SchemaTable }) {
  return (
    <section className="drawer-sec">
      <h3>Load</h3>
      <div className="load-grid">
        <LoadCard kind="measured" data={t.measured} />
        <LoadCard kind="expected" data={t.expected} />
      </div>
    </section>
  );
}

function LoadCard({ kind, data }: { kind: "measured" | "expected"; data?: Record<string, M> }) {
  const d = data || {};
  const keys = [...CORE, ...Object.keys(d).filter((k) => !CORE.includes(k))];
  const measured = kind === "measured";
  return (
    <div className={cx("card load-card", kind)}>
      <div className="load-h">
        <b>{measured ? "Measured" : "Expected"}</b>
        <span>{measured ? "recorded, with source and date" : "after this design, with its basis"}</span>
      </div>
      <dl>
        {keys.map((k) => {
          const m = d[k];
          return (
            <div className="m" key={k}>
              <dt>{label(k, m)}</dt>
              {hasValue(m) ? (
                <dd className="k">{String(m!.value)}{m!.unit ? <span className="u">{m!.unit}</span> : null}</dd>
              ) : (
                <dd className="none">{measured ? "not measured" : "not estimated"}</dd>
              )}
              {measured && hasValue(m) && (m!.source || m!.date) ? <dd className="src">{[m!.source, m!.date].filter(Boolean).join(" · ")}</dd> : null}
              {m?.note ? <dd className="src basis"><Ticks text={m.note} /></dd> : null}
            </div>
          );
        })}
      </dl>
    </div>
  );
}
