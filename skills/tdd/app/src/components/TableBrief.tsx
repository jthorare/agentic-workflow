// TableBrief (D28): one shape for a table's detail, wherever it opens: in the hotspot card of a schema-diagram table
// card (mode "pop", on hover or keyboard focus) and at the top of the drawer's Details tab (mode "drawer").
//   1. Purpose: the first sentence of the card's summary (the whole summary in the drawer).
//   2. Measures: a small table, measure · production · after this design · delta · source (date), one row each for
//      rows, columns, writes and cost, with "not measured" / "not estimated" cells; any other measured or expected key
//      follows them in the drawer (the hotspot card counts them). The drawer lists each row's basis under the table.
//   3. Trade-offs: the card's optional `tradeoffs` ([{gain, cost}] or ["…"]), hidden when absent.
//   4. Place and impact: a mini diagram drawn from the cards' columns and indexes: the table in the middle, the
//      tables it references on the right, the tables that reference it on the left, each side box carrying the key,
//      toned as on the page's diagrams (else new green, changed amber, existing grey; a table with no card dashed),
//      and a writes arrow with the expected write rate when the card states one.
// Pure SVG and HTML, no library; colours from the theme tokens (schema.css .tbrief, .mm-*). Pure render.
import { Fragment, useId, useMemo } from "react";
import { useContent } from "../lib/context";
import { cx } from "../lib/raw";
import type { Content, Measure, SchemaTable } from "../types";
import { DirIcon, deltaText, direction, keyCols, shortDelta, sizing, type Dir } from "./TableNode";

const has = (m?: Measure) => !!m && m.value !== undefined && m.value !== null && String(m.value).trim() !== "";
const str = (v: unknown) => (v === undefined || v === null ? "" : String(v).trim());
const val = (m?: Measure) => (has(m) ? [str(m!.value), str(m!.unit)].filter(Boolean).join(" ") : "");

/** Plain text with `code` spans (the schema JSON is not run through the inline renderer). */
function Ticks({ text }: { text: string }) {
  const parts = text.split("`");
  return <>{parts.map((p, i) => (i % 2 ? <code key={i}>{p}</code> : <Fragment key={i}>{p}</Fragment>))}</>;
}

/** The first sentence of a summary: up to the first ". " that is followed by a capital, a quote or a code span. */
export function firstSentence(s: string) {
  const m = /^(.+?[.!?])\s+(?=[A-Z"“`(])/.exec(s.trim());
  return m ? m[1] : s.trim();
}

/* ------------------------------------------------------------------------------------------------ measures */

export interface BriefRow {
  key: string; label: string; prod: string; prodNone: boolean; after: string; afterNone: boolean;
  dir: Dir; delta: string; source: string; basis: string; extra: boolean;
}

const LABELS: Record<string, string> = { rows: "Rows", columns: "Columns", writes: "Writes", cost: "Cost", reads: "Reads", storage: "Storage" };
const labelOf = (k: string, m?: Measure & { label?: string }) =>
  m?.label || LABELS[k] || (k.replace(/_/g, " ").replace(/^./, (c) => c.toUpperCase()));
const sourceOf = (m?: Measure) => {
  if (!has(m)) return "–";
  const s = str(m!.source), d = str(m!.date);
  return s ? (d ? `${s} (${d})` : s) : d ? `not stated (${d})` : "not stated";
};

/** The measures table: rows, columns, writes, cost, then every other measured or expected key in file order. */
export function briefRows(t: SchemaTable): BriefRow[] {
  const tiles = new Map(sizing(t).map((x) => [x.key, x]));
  const cols = t.columns || [];
  const allNew = t.state === "new";
  const added = cols.filter((c) => allNew || c.change === "new").length;
  const dropped = cols.filter((c) => c.change === "dropped").length;
  const out: BriefRow[] = [];
  for (const k of ["rows", "columns", "writes", "cost"] as const) {
    if (k === "columns") {
      const x = tiles.get("cols")!, m = t.measured?.columns;
      const live = has(m) ? Number(str(m!.value).replace(/,/g, "")) : NaN;
      const net = added - dropped;
      out.push({
        key: k, label: "Columns", extra: false,
        prod: has(m) ? val(m) : allNew ? "none (new table)" : String(cols.length - added), prodNone: !has(m) && allNew,
        after: Number.isFinite(live) ? String(live + net) : String(cols.length - dropped), afterNone: false,
        dir: x.next.dir, delta: x.next.value, source: has(m) ? sourceOf(m) : "this card's column list", basis: x.next.note,
      });
      continue;
    }
    const x = tiles.get(k)!, m = t.measured?.[k], e = t.expected?.[k];
    out.push({
      key: k, label: LABELS[k], extra: false,
      prod: x.now.missing ? "not measured" : val(m), prodNone: x.now.missing,
      after: x.next.missing ? "not estimated" : val(e), afterNone: x.next.missing,
      dir: x.next.dir, delta: shortDelta(x.next), source: sourceOf(m), basis: [x.next.note, x.now.note].filter(Boolean).join(" "),
    });
  }
  const seen = new Set(["rows", "columns", "writes", "cost"]);
  for (const k of [...Object.keys(t.measured || {}), ...Object.keys(t.expected || {})]) {
    if (seen.has(k)) continue;
    seen.add(k);
    const m = t.measured?.[k] as (Measure & { label?: string }) | undefined, e = t.expected?.[k] as (Measure & { label?: string }) | undefined;
    const dir = has(e) ? direction(str(e!.value), str(e!.unit), has(m) ? str(m!.value) : "", has(m) ? str(m!.unit) : "", t.state) : "unknown";
    out.push({
      key: k, label: labelOf(k, m || e), extra: true,
      prod: has(m) ? val(m) : "not measured", prodNone: !has(m),
      after: has(e) ? val(e) : "not estimated", afterNone: !has(e),
      dir, delta: has(e) ? shortDelta({ value: deltaText(str(e!.value), dir), unit: str(e!.unit), missing: false, dir, note: "" }) : "",
      source: sourceOf(m), basis: [str(e?.note), str(m?.note)].filter(Boolean).join(" "),
    });
  }
  return out;
}

/* ------------------------------------------------------------------------------------------------ trade-offs */

export interface TradeLine { kind: "gain" | "cost" | "note"; text: string }

/** The card's `tradeoffs`: {gain, cost} pairs become a gain line and a cost line; a string starting with "+" or
 *  "gain:" is a gain, with "−", "-" or "cost:" a cost, anything else a plain line. */
export function tradeLines(t: SchemaTable): TradeLine[] {
  const out: TradeLine[] = [];
  for (const x of t.tradeoffs || []) {
    if (typeof x === "string") {
      const s = x.trim();
      if (!s) continue;
      const g = /^(?:\+|gains?:)\s*/i.exec(s), c = /^(?:[−-]|costs?:)\s*/i.exec(s);
      out.push(g ? { kind: "gain", text: s.slice(g[0].length) } : c ? { kind: "cost", text: s.slice(c[0].length) } : { kind: "note", text: s });
    } else if (x && typeof x === "object") {
      if (str(x.gain)) out.push({ kind: "gain", text: str(x.gain) });
      if (str(x.cost)) out.push({ kind: "cost", text: str(x.cost) });
    }
  }
  return out;
}

/* ------------------------------------------------------------------------------------------------ neighbours */

const TONE = /\b(green|grey|gray|blue|amber|red|violet)\b/;
const base = (n: string) => (n.split(".").pop() || n).replace(/"/g, "");

export interface Nb { table: string; cols: string[]; card: SchemaTable | null; tone: string }

/** Each table's tone on the page's diagrams (the first hotspot that carries it), by table name. */
function diagramTones(c: Content): Map<string, string> {
  const out = new Map<string, string>();
  const byKey = (k: string) => c.schema.find((t) => t.id === k || t.table === k);
  const visit = (n: any) => {
    if (!n || typeof n !== "object") return;
    if (Array.isArray(n)) { n.forEach(visit); return; }
    if (n.type === "diagram" && Array.isArray(n.hotspots)) {
      for (const h of n.hotspots) {
        const t = h?.table ? byKey(h.table) : null;
        const tone = TONE.exec(h?.tone || "")?.[1];
        if (t && tone && !out.has(t.table)) out.set(t.table, tone.replace("gray", "grey"));
      }
    }
    for (const k in n) if (k !== "hotspots") visit(n[k]);
  };
  visit(c.sections);
  return out;
}

const stateTone = (t: SchemaTable | null) => (!t ? "ext" : t.state === "new" ? "green" : t.state === "changed" ? "amber" : "grey");

/** The tables a card references (its foreign keys, grouped by target) and the cards that reference it. */
export function neighbours(t: SchemaTable, schema: SchemaTable[], tones: Map<string, string>): { out: Nb[]; inn: Nb[] } {
  const find = (name: string) => schema.find((u) => u.table === name) || null;
  const toneOf = (name: string, card: SchemaTable | null) => tones.get(name) || stateTone(card);
  const group = (pairs: [string, string][]) => {
    const m = new Map<string, string[]>();
    for (const [table, col] of pairs) m.set(table, [...(m.get(table) || []), col]);
    return [...m.entries()].map(([table, cols]) => { const card = find(table); return { table, cols, card, tone: toneOf(table, card) }; });
  };
  const outPairs: [string, string][] = [];
  for (const [col, k] of keyCols(t)) if (k.fk) outPairs.push([base(k.fk), col]);
  const order = new Map((t.columns || []).map((c, i) => [c.name, i]));
  outPairs.sort((a, b) => (order.get(a[1]) ?? 0) - (order.get(b[1]) ?? 0));
  const inPairs: [string, string][] = [];
  for (const u of schema) {
    if (u === t) continue;
    for (const [col, k] of keyCols(u)) if (k.fk && base(k.fk) === t.table) inPairs.push([u.table, col]);
  }
  return { out: group(outPairs), inn: group(inPairs) };
}

/* ------------------------------------------------------------------------------------------------ mini diagram */

const W = 560, MID_W = 156, LANE = 34, NAME_PX = 13.5, KEY_PX = 13, MONO = 0.6, LINE = 16, PAD = 7, GAP = 8, MAX_SIDE = 5;

/** A table name in lines of at most `maxPx`, broken after underscores, at most `max` lines (the last one cut with …). */
function wrapName(name: string, maxPx: number, px: number, max: number) {
  const cap = Math.max(4, Math.floor(maxPx / (px * MONO)));
  const segs = name.match(/[^_]+_?|_/g) || [name];
  const lines: string[] = [];
  let cur = "";
  for (const s of segs) {
    if ((cur + s).length <= cap) { cur += s; continue; }
    if (cur) lines.push(cur);
    cur = s;
    while (cur.length > cap) { lines.push(cur.slice(0, cap)); cur = cur.slice(cap); }
  }
  if (cur) lines.push(cur);
  if (lines.length <= max) return lines;
  const kept = lines.slice(0, max);
  kept[max - 1] = kept[max - 1].slice(0, cap - 1) + "…";
  return kept;
}

const fit = (s: string, maxPx: number, px: number) => {
  const cap = Math.max(4, Math.floor(maxPx / (px * MONO)));
  return s.length <= cap ? s : s.slice(0, cap - 1) + "…";
};

/** A side box: its name lines and key; `inline` puts the key after the name on one line when both fit. */
interface Box { nb: Nb | null; more: number; lines: string[]; key: string; inline: boolean; y: number; h: number }

function sideBoxes(list: Nb[], dir: "in" | "out", sideW: number): Box[] {
  const inner = sideW - 16;
  const shown = list.length > MAX_SIDE ? list.slice(0, MAX_SIDE - 1) : list;
  const boxes: Box[] = shown.map((nb) => {
    const cols = nb.cols[0] + (nb.cols.length > 1 ? ` +${nb.cols.length - 1}` : "");
    const keyFull = dir === "in" ? `${cols} →` : `← ${cols}`;
    const inline = (nb.table.length + 2) * NAME_PX * MONO + keyFull.length * KEY_PX * MONO <= inner;
    if (inline) return { nb, more: 0, lines: [nb.table], key: keyFull, inline, y: 0, h: PAD * 2 + LINE };
    const lines = wrapName(nb.table, inner, NAME_PX, 2);
    return { nb, more: 0, lines, key: fit(keyFull, inner, KEY_PX), inline, y: 0, h: PAD + lines.length * LINE + LINE + PAD - 2 };
  });
  if (list.length > shown.length) boxes.push({ nb: null, more: list.length - shown.length, lines: [`+${list.length - shown.length} more`], key: "", inline: false, y: 0, h: PAD * 2 + LINE });
  return boxes;
}

export function MiniMap({ t, tone, out, inn, writes }: { t: SchemaTable; tone: string; out: Nb[]; inn: Nb[]; writes: string }) {
  const uid = useId().replace(/[^a-zA-Z0-9_-]/g, "");
  // columns: tables that reference it | the table | tables it references; a side with nothing gives its width away
  const both = inn.length > 0 && out.length > 0;
  const sideW = both ? (W - MID_W - 2 * LANE) / 2 : W - MID_W - LANE;
  const midX = both ? sideW + LANE : out.length ? 0 : inn.length ? W - MID_W : (W - MID_W) / 2;
  const lX = 0, rX = both ? W - sideW : MID_W + LANE;
  const L = sideBoxes(inn, "in", sideW), R = sideBoxes(out, "out", sideW);
  const midLines = wrapName(t.table, MID_W - 14, 14, 3);
  const midH = PAD + midLines.length * 17 + 15 + PAD;
  const top = writes ? 36 : 6;
  const colH = (bs: Box[]) => (bs.length ? bs.reduce((a, b) => a + b.h, 0) + GAP * (bs.length - 1) : 0);
  const contentH = Math.max(colH(L), colH(R), midH);
  const place = (bs: Box[]) => { let y = top + (contentH - colH(bs)) / 2; for (const b of bs) { b.y = y; y += b.h + GAP; } };
  place(L); place(R);
  const midY = top + (contentH - midH) / 2;
  const lone = !L.length && !R.length;
  const H = top + contentH + (lone ? 26 : 6);
  const arrow = `mm-a-${uid}`;
  const stateWord = t.state === "new" ? "new table" : t.state === "changed" ? "changed" : "existing";
  const label = [
    `${t.table} (${stateWord})`,
    out.length ? `references ${out.map((n) => `${n.table} by ${n.cols.join(", ")}`).join("; ")}` : "references no table",
    inn.length ? `referenced by ${inn.map((n) => `${n.table}.${n.cols.join(", ")}`).join("; ")}` : "no table references it",
    writes ? `writes ${writes}` : "",
  ].filter(Boolean).join(". ");
  const node = (b: Box, x: number, side: "l" | "r", i: number) => {
    const nameW = b.lines[0].length * NAME_PX * MONO;
    return (
      <g key={`${side}${i}`} className={cx("mm-node", b.nb ? `t-${b.nb.tone}` : "t-more", b.nb && !b.nb.card && "ext")}>
        <title>{b.nb ? `${b.nb.table}${b.nb.card ? ` (${b.nb.card.state})` : " (no card in this design)"}: ${side === "l" ? `${b.nb.table}.${b.nb.cols.join(", ")} → ${t.table}` : `${t.table}.${b.nb.cols.join(", ")} → ${b.nb.table}`}` : `${b.more} more tables`}</title>
        <rect x={x + 0.75} y={b.y} width={sideW - 1.5} height={b.h} rx="6" />
        {b.lines.map((l, j) => <text key={j} className="mm-name" x={x + 8} y={b.y + PAD + LINE * (j + 1) - 4}>{l}</text>)}
        {b.key && b.inline ? <text className="mm-key" x={x + 8 + nameW + 2 * NAME_PX * MONO} y={b.y + PAD + LINE - 4}>{b.key}</text> : null}
        {b.key && !b.inline ? <text className="mm-key" x={x + 8} y={b.y + PAD + LINE * (b.lines.length + 1) - 4}>{b.key}</text> : null}
      </g>
    );
  };
  return (
    <svg className="mm" viewBox={`0 0 ${W} ${H}`} role="img" aria-label={label}>
      <defs>
        <marker id={arrow} viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
          <path className="mm-arrow" d="M0 0L10 5L0 10z" />
        </marker>
      </defs>
      {L.map((b, i) => {
        const y1 = b.y + b.h / 2, y2 = midY + ((i + 1) * midH) / (L.length + 1);
        return <path key={`el${i}`} className="mm-edge" d={`M${lX + sideW} ${y1} L${midX} ${y2}`} markerEnd={`url(#${arrow})`} />;
      })}
      {R.map((b, i) => {
        const y1 = midY + ((i + 1) * midH) / (R.length + 1), y2 = b.y + b.h / 2;
        return <path key={`er${i}`} className="mm-edge" d={`M${midX + MID_W} ${y1} L${rX} ${y2}`} markerEnd={`url(#${arrow})`} />;
      })}
      {writes ? (
        <g className="mm-writes">
          <title>{`writes: ${writes}`}</title>
          <path className="mm-edge w" d={`M${midX + MID_W / 2} 4 L${midX + MID_W / 2} ${midY}`} markerEnd={`url(#${arrow})`} />
          <text className="mm-wlabel" x={midX + MID_W / 2 + (inn.length && !out.length ? -9 : 9)} y="18" textAnchor={inn.length && !out.length ? "end" : "start"}>
            {fit(`writes ${writes}`, (inn.length && !out.length ? midX + MID_W / 2 : W - midX - MID_W / 2) - 14, 12 * 0.92)}
          </text>
        </g>
      ) : null}
      {L.map((b, i) => node(b, lX, "l", i))}
      {R.map((b, i) => node(b, rX, "r", i))}
      <g className={cx("mm-node mid", `t-${tone}`)}>
        <title>{`${t.table} (${stateWord})`}</title>
        <rect x={midX} y={midY} width={MID_W} height={midH} rx="7" />
        {midLines.map((l, j) => <text key={j} className="mm-name" x={midX + 8} y={midY + PAD + 17 * (j + 1) - 4}>{l}</text>)}
        <text className="mm-state" x={midX + 8} y={midY + PAD + 17 * midLines.length + 12}>{stateWord}</text>
      </g>
      {lone ? <text className="mm-key" x={W / 2} y={H - 8} textAnchor="middle">no foreign key in or out</text> : null}
    </svg>
  );
}

/* ------------------------------------------------------------------------------------------------ the brief */

const CHIP: Record<string, string> = { new: "new", changed: "changed", existing: "existing" };

export function TableBrief({ t, mode }: { t: SchemaTable; mode: "pop" | "drawer" }) {
  const c = useContent();
  const tones = useMemo(() => diagramTones(c), [c]);
  const rows = briefRows(t);
  const extra = rows.filter((r) => r.extra);
  const shown = mode === "drawer" ? rows : rows.filter((r) => !r.extra);
  const purpose = mode === "drawer" ? str(t.summary) : firstSentence(str(t.summary));
  const trade = tradeLines(t);
  const { out, inn } = neighbours(t, c.schema, tones);
  const midTone = tones.get(t.table) || stateTone(t);
  const writes = val(t.expected?.writes) || val(t.measured?.writes);
  const basis = rows.filter((r) => r.basis);
  const Sec = mode === "drawer" ? "section" : "div";
  const H = mode === "drawer" ? "h3" : "h4";
  return (
    <div className={cx("tbrief", mode)}>
      {mode === "pop" ? (
        <p className="tb-title"><code>{t.table}</code><span className={cx("tb-chip", t.state)}>{CHIP[t.state] || t.state}</span></p>
      ) : null}
      {purpose ? <p className={cx("tb-purpose", mode === "drawer" && "drawer-summary")}><Ticks text={purpose} /></p> : null}
      <Sec className={cx("tb-sec", mode === "drawer" && "drawer-sec")}>
        {mode === "drawer" ? <H>Measures</H> : null}
        <table className="tb-measures">
          <thead>
            <tr><th scope="col">Measure</th><th scope="col">Production</th><th scope="col">After this design</th><th scope="col">Delta</th><th scope="col">Source (date)</th></tr>
          </thead>
          <tbody>
            {shown.map((r) => (
              <tr key={r.key} className={cx(r.extra && "extra")}>
                <th scope="row">{r.label}</th>
                <td className={cx(r.prodNone && "none")}>{r.prod}</td>
                <td className={cx(r.afterNone && "none")}>{r.after}</td>
                <td className="d">{r.delta ? <span className={cx("tb-d", r.dir)}><DirIcon dir={r.dir} />{r.delta}</span> : <span className="none">–</span>}</td>
                <td className={cx("src", r.source === "–" && "none")}>{r.source}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {mode === "pop" && extra.length ? (
          <p className="tb-more">+{extra.length} more {extra.length === 1 ? "measure" : "measures"} ({extra.map((r) => r.label.toLowerCase()).join(", ")}) in the table's Details</p>
        ) : null}
        {mode === "drawer" && basis.length ? (
          <dl className="tb-basis">
            {basis.map((r) => <div key={r.key}><dt>{r.label}</dt><dd><Ticks text={r.basis} /></dd></div>)}
          </dl>
        ) : null}
      </Sec>
      {trade.length ? (
        <Sec className={cx("tb-sec tb-trade", mode === "drawer" && "drawer-sec")}>
          <H>Trade-offs</H>
          <ul>
            {trade.map((l, i) => (
              <li key={i} className={l.kind}>
                <span className="tb-sign" aria-hidden="true">{l.kind === "gain" ? "+" : l.kind === "cost" ? "−" : "·"}</span>
                <span>{l.kind === "gain" ? <span className="sr-only">Gains: </span> : l.kind === "cost" ? <span className="sr-only">Costs: </span> : null}<Ticks text={l.text} /></span>
              </li>
            ))}
          </ul>
        </Sec>
      ) : null}
      <Sec className={cx("tb-sec tb-map", mode === "drawer" && "drawer-sec")}>
        <H>Place and impact</H>
        <div className="tb-map-box"><MiniMap t={t} tone={midTone} out={out} inn={inn} writes={writes} /></div>
        <p className="tb-map-key">Arrows point at the referenced table; each side box names the key.</p>
      </Sec>
    </div>
  );
}
