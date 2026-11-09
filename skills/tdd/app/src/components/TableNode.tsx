// TableNode (D26): an HTML card laid exactly over a schema-diagram box whose hotspot carries a `table` with a card in
// content.schema. The SVG box stays underneath, covered. The card is the hotspot itself (`a.hs.has-table.tnode`, the
// same data-hs / data-table attributes), so hover shows the hotspot card, a click opens the drawer, and Tab reaches it.
// It shows the table name and state chip, a column list with primary and foreign keys set larger and badged (PK / FK,
// "→ table" when the note, type or a FOREIGN KEY line names one), changed columns in their change colours, and the
// standard sizing strip: rows, columns and cost tiles, each with the production value (or "not measured") and the
// expected change after the design (or "not estimated") as a red, green or grey pill showing the value only. Every
// tile's full text (and writes, which has no tile) is in [data-tip] for the tile tooltip and the hotspot card, and in a
// visually hidden description the card's aria-describedby names.
//
// Everything scales with the drawing: the card is a size container and its em is set in cqw (see cardLayout). How many
// column lines fit is worked out here, in em of the box, so the server render and the client agree and the card reads
// the same with JavaScript off. Text is about 11.5 to 12 px at a 1440 window (cardLayout); a box may go down to 11 px
// to keep one column line, then drops column lines, then the strip (a compact card: the name, its keys and the count;
// the sizing stays in the hover card), and shrinks type only when it is too short for its head. Pure render: no Date,
// random or window.
import type { CSSProperties, ReactNode } from "react";
import { cx } from "../lib/raw";
import type { Column, Hotspot, Measure, SchemaTable } from "../types";

/* ------------------------------------------------------------------------------------------------ keys */

export interface ColKey { pk: boolean; fk: string }

const ARROW_REF = /(?:^|[\s(;:,])→\s*([A-Za-z_][\w.]*)/;
const TYPE_REF = /\bREFERENCES\s+([A-Za-z_][\w.]*)/i;

function listCols(s: string) {
  return s.split(",").map((x) => x.trim().replace(/^"|"$/g, "")).filter(Boolean);
}

/** Primary and foreign keys per column name, from the column type and note and the card's indexes[] lines. */
export function keyCols(t: SchemaTable): Map<string, ColKey> {
  const out = new Map<string, ColKey>();
  const get = (n: string) => { let k = out.get(n); if (!k) { k = { pk: false, fk: "" }; out.set(n, k); } return k; };
  for (const c of t.columns || []) {
    const type = c.type || "", note = c.note || "";
    if (/\bPRIMARY\s+KEY\b/i.test(type)) get(c.name).pk = true;
    const ref = TYPE_REF.exec(type)?.[1] || ARROW_REF.exec(note)?.[1] || ARROW_REF.exec(type)?.[1] || "";
    if (ref) get(c.name).fk = ref;
  }
  for (const line of t.indexes || []) {
    const pk = /^\s*PRIMARY\s+KEY\s*\(([^)]*)\)/i.exec(line);
    if (pk) for (const n of listCols(pk[1])) get(n).pk = true;
    const fk = /FOREIGN\s+KEY\b[^(]*\(([^)]*)\)\s*(?:→|REFERENCES)\s*([A-Za-z_][\w.]*)/i.exec(line);
    if (fk) for (const n of listCols(fk[1])) { const k = get(n); if (!k.fk) k.fk = fk[2]; }
  }
  const names = new Set((t.columns || []).map((c) => c.name));
  for (const n of [...out.keys()]) if (!names.has(n)) out.delete(n);
  return out;
}

/** The base SQL type, without constraints and defaults: "uuid PRIMARY KEY DEFAULT …" → "uuid". */
export function baseType(type?: string) {
  const s = (type || "").trim();
  if (!s) return "";
  const m = /^(.*?)(?=\s+(?:NOT\b|NULL\b|PRIMARY\b|DEFAULT\b|REFERENCES\b|UNIQUE\b|CHECK\b|GENERATED\b|CONSTRAINT\b|COLLATE\b)|\s*·|$)/i.exec(s);
  return (m ? m[1] : s).trim();
}

/* ------------------------------------------------------------------------------------------------ sizing */

export type Dir = "up" | "down" | "same" | "unknown";
export type TileKey = "rows" | "cols" | "writes" | "cost";
export interface Tile {
  key: TileKey; label: string; title: string;
  now: { value: string; unit: string; missing: boolean; source: string; date: string; note: string; basis: string };
  next: { value: string; unit: string; missing: boolean; dir: Dir; note: string };
}

const has = (m?: Measure) => !!m && m.value !== undefined && m.value !== null && String(m.value).trim() !== "";
const str = (v: unknown) => (v === undefined || v === null ? "" : String(v).trim());

/** A plain number ("37,821", "~170", "1.5k"); null for anything else ("≤ 1", "0.1–2", "grows"). */
function plainNum(v: string): number | null {
  const m = /^[~≈]?\s*(\d[\d,]*(?:\.\d+)?)\s*([kKmM])?$/.exec(v.trim());
  if (!m) return null;
  const n = Number(m[1].replace(/,/g, ""));
  return m[2] ? n * (/k/i.test(m[2]) ? 1e3 : 1e6) : n;
}

/** Which way an expected value moves the measure: an explicit sign or word first, then a numeric comparison with the
 *  measured value in the same unit, then, for a new table, any figure is a rise from nothing. Otherwise unknown. */
export function direction(next: string, nextUnit: string, now: string, nowUnit: string, state: string): Dir {
  const v = next.trim(), l = v.toLowerCase();
  if (!v) return "unknown";
  if (/^(no change|unchanged|same|none|nothing|no new|±\s*0(?![\d.,]))/.test(l) || /^[+−-]?\s*0(?![\d.,])/.test(l)) return "same";
  if (/^\+/.test(v)) return "up";
  if (/^[−-]\s*\d/.test(v)) return "down";
  if (/^(grows?|more|increases?|rises?|higher|adds?|up)\b/.test(l)) return "up";
  if (/^(shrinks?|fewer|less|decreases?|falls?|lower|drops?|removes?|down)\b/.test(l)) return "down";
  const a = plainNum(now), b = plainNum(v);
  if (a !== null && b !== null && (!nextUnit || !nowUnit || nextUnit === nowUnit)) return b > a ? "up" : b < a ? "down" : "same";
  if (state === "new" && /\d/.test(v)) return "up";
  return "unknown";
}

/** The delta as shown: a "+" before a bare figure that rises; a typographic minus. */
function deltaText(v: string, dir: Dir) {
  const s = v.replace(/^-(?=\s*\d)/, "−");
  return dir === "up" && /^\d/.test(s) ? `+${s}` : s;
}

const TILES: { key: TileKey; label: string; title: string }[] = [
  { key: "rows", label: "rows", title: "Rows in production" },
  { key: "cols", label: "cols", title: "Columns" },
  { key: "cost", label: "cost", title: "Cost" },
  { key: "writes", label: "writes", title: "Writes" },
];
/** The strip's tiles; writes is in the card's detail (tooltip list, hover card, description) only. */
const STRIP: TileKey[] = ["rows", "cols", "cost"];

/** The sizing facts for a table card: rows, cols, cost (the strip) and writes. Values come only from the card: measured.rows / writes / cost /
 *  columns, the column list, expected.rows / writes / cost. Absent values are flagged missing, never zero. */
export function sizing(t: SchemaTable): Tile[] {
  const cols = (t.columns || []) as Column[];
  const allNew = t.state === "new";
  const added = cols.filter((c) => allNew || c.change === "new").length;
  const dropped = cols.filter((c) => c.change === "dropped").length;
  const changed = cols.filter((c) => !allNew && c.change === "changed").length;
  const after = cols.length - dropped;
  return TILES.map(({ key, label, title }) => {
    const m = t.measured?.[key === "cols" ? "columns" : key];
    const e = key === "cols" ? undefined : t.expected?.[key];
    const now = {
      value: has(m) ? str(m!.value) : "", unit: has(m) ? str(m!.unit) : "", missing: !has(m),
      source: has(m) ? str(m!.source) : "", date: has(m) ? str(m!.date) : "", note: str(m?.note), basis: "",
    };
    let next: Tile["next"];
    if (key === "cols") {
      if (!has(m)) { now.value = String(after); now.missing = false; now.basis = "counted from this card's column list"; }
      const net = added - dropped;
      const parts = [added ? `${added} new` : "", dropped ? `${dropped} dropped` : "", changed ? `${changed} changed` : ""].filter(Boolean);
      next = {
        value: net > 0 ? `+${net}` : net < 0 ? `−${-net}` : "±0", unit: "", missing: false,
        dir: net > 0 ? "up" : net < 0 ? "down" : "same",
        note: parts.length ? `${parts.join(", ")} in this design` : "no column added, dropped or changed in this design",
      };
    } else if (has(e)) {
      const v = str(e!.value), u = str(e!.unit);
      const dir = direction(v, u, now.value, now.unit, t.state);
      next = { value: deltaText(v, dir), unit: u, missing: false, dir, note: str(e!.note) };
    } else {
      next = { value: "", unit: "", missing: true, dir: "unknown", note: str(e?.note) };
    }
    return { key, label, title, now, next };
  });
}

const DIR_WORD: Record<Dir, string> = { up: "rises", down: "falls", same: "stays the same", unknown: "direction not stated" };

/** The tooltip text for a tile: a title line, then what is in production, then the expected change and its basis. */
export function tipText(t: SchemaTable, x: Tile): string {
  const field = x.key === "cols" ? "measured.columns" : `measured.${x.key}`;
  const lines = [x.title];
  if (x.now.missing) lines.push(`Production: not measured (no ${field} in schema/${t.table}.json${t.state === "new" ? "; a new table" : ""})`);
  else {
    lines.push(`${x.key === "cols" && x.now.basis ? "This card" : "Production"}: ${[x.now.value, x.now.unit].filter(Boolean).join(" ")}`);
    if (x.now.basis) lines.push(`Source: ${x.now.basis}; no ${field} on the card`);
    else lines.push(`Source: ${x.now.source || "not stated"}${x.now.date ? `, ${x.now.date}` : ""}`);
    if (x.now.note) lines.push(`Note: ${x.now.note}`);
  }
  if (x.next.missing) lines.push(`After this design: not estimated${x.key === "cols" ? "" : ` (no expected.${x.key})`}`);
  else lines.push(`After this design: ${[x.next.value, x.next.unit].filter(Boolean).join(" ")} (${DIR_WORD[x.next.dir]})`);
  if (x.next.note) lines.push(`Basis: ${x.next.note}`);
  return lines.join("\n").replace(/`/g, "");
}

/* ------------------------------------------------------------------------------------------------ fitting */

// Heights in em of the card (schema.css sizes .tnode in em to match): the head band and its gap (1.95 + .3), a column
// line (1.45), a key line (1.4 at 1.15em), the "+N more" line (1.45 at .91em), the strip (.3 gap + a tile of .4
// padding, .125 border, label 1.18, value 1.25, pill 1.4), a second pill line when a delta wraps, the bottom padding
// with the frame's two borders (.4 + 2 × .125), and a safety margin.
const HEAD_EM = 2.25, COL_EM = 1.45, KEY_EM = 1.61, MORE_EM = 1.32, STRIP_EM = 4.65, WRAP_EM = 1.25, PAD_EM = 0.65, SAFE_EM = 0.1;
// A compact card (no strip) keeps .2em of bottom padding instead of .4.
const PAD_COMPACT_EM = 0.45;
// 1em is about 11.5 to 12 px when the page fits the drawing to its 1250 to 1300 px column at a 1440 window (a figure in a
// folded section is the narrower one): TEXT_PX × natural width
// / 1250 drawing px, kept between 12 and 32. Column names and tile numbers are 1em, keys 1.15em, labels .91em (10.5 px).
// A card may go down to MIN_PX (11 px) to keep one column line; below that it drops lines, then its strip, never size.
const TEXT_PX = 11.5, MIN_PX = 11, FIT_COLUMN_PX = 1250, EM_MIN = 12, EM_MAX = 32;
// The narrowest card that holds three tiles at full size, and the narrowest compact card (name, chip, count), in em.
const WIDTH_EM = 25, COMPACT_WIDTH_EM = 18;

/** Rough rendered width of a delta pill's text in em of the card (digits and Inter at 650 run about 0.6em a glyph). */
function pillEm(x: Tile) {
  if (x.next.missing) return 0.91 * 0.56 * "not estimated".length;
  return 0.6 * [...x.next.value].length + 0.78 + 0.25 + 0.9;
}

export interface CardLayout { em: number; strip: boolean; wrap: boolean; shown: Line[]; hidden: number; moreLine: boolean }

/** The card's em (drawing px), whether it keeps its strip, whether a delta wraps to two lines, and its column lines.
 *  Order of preference: the strip with at least one column line at the target size, then at the 11 px floor; then the
 *  compact card (no strip: name, chip, key lines, "+N more") at the target size, then at the floor; only a box too
 *  small for the head itself shrinks the text below the floor. */
export function cardLayout(t: SchemaTable, keys: Map<string, ColKey>, tiles: Tile[], natW: number, boxW: number, boxH: number): CardLayout {
  const target = Math.min(EM_MAX, Math.max(EM_MIN, (TEXT_PX * natW) / FIT_COLUMN_PX));
  const floor = (target * MIN_PX) / TEXT_PX;
  const noCols = !(t.columns || []).length;
  const hOf = (em: number) => (boxH ? boxH / em : 99);
  const sizes = (cap: number) => (cap >= target ? [target, floor] : cap >= floor - 1e-9 ? [cap, floor] : []);
  for (const em of sizes(boxW / WIDTH_EM)) {
    const tileEm = (boxW / em - 1.3 - 0.25 - 2 * 0.3) / 3 - 0.68 - 0.125;
    const wrap = tiles.filter((x) => STRIP.includes(x.key)).some((x) => pillEm(x) > tileEm);
    const room = hOf(em) - HEAD_EM - STRIP_EM - (wrap ? WRAP_EM : 0) - PAD_EM - SAFE_EM;
    const full = fitColumns(t, keys, room);
    if (full.shown.length || (noCols && room >= 0)) return { em, strip: true, wrap, ...full };
  }
  for (const em of sizes(boxW / COMPACT_WIDTH_EM)) {
    const lay = fitColumns(t, keys, hOf(em) - HEAD_EM - PAD_COMPACT_EM - SAFE_EM);
    if (lay.shown.length) return { em, strip: false, wrap: false, ...lay };
  }
  // The head alone: at the target size when it fits, else shrunk to the box.
  const headOnly = HEAD_EM + PAD_COMPACT_EM + SAFE_EM;
  let em = Math.min(target, boxW / COMPACT_WIDTH_EM);
  if (boxH && boxH / em < headOnly) em = boxH / headOnly;
  em = Math.max(4, em);
  return { em, strip: false, wrap: false, ...fitColumns(t, keys, hOf(em) - HEAD_EM - PAD_COMPACT_EM - SAFE_EM) };
}

interface Line { c: Column; i: number; key?: ColKey; mark: string }

/** The column lines that fit `availEm` (keys first, then changed columns, then the rest, shown in table order). The
 *  "+N more" line is drawn only when it costs no column line; otherwise the count goes in the head. */
export function fitColumns(t: SchemaTable, keys: Map<string, ColKey>, availEm: number) {
  const allNew = t.state === "new";
  const lines: Line[] = (t.columns || []).map((c, i) => {
    const k = keys.get(c.name);
    const mark = !allNew && c.change && c.change !== "unchanged" ? c.change : "";
    return { c, i, key: k && (k.pk || k.fk) ? k : undefined, mark };
  });
  const prio = (l: Line) => (l.key?.pk ? 0 : l.key ? 1 : l.mark ? 2 : 3);
  const ranked = [...lines].sort((a, b) => prio(a) - prio(b) || a.i - b.i);
  const h = (l: Line) => (l.key ? KEY_EM : COL_EM);
  const take = (room: number) => {
    const out: Line[] = [];
    let used = 0;
    for (const l of ranked) { if (used + h(l) > room + 1e-9) break; out.push(l); used += h(l); }
    return out;
  };
  const shown = take(availEm);
  let moreLine = false;
  if (shown.length < lines.length && take(availEm - MORE_EM).length === shown.length && shown.length) moreLine = true;
  shown.sort((a, b) => a.i - b.i);
  return { shown, hidden: lines.length - shown.length, moreLine };
}

/* ------------------------------------------------------------------------------------------------ the card */

const ICONS: Record<TileKey, ReactNode> = {
  rows: <><rect x="2" y="2.5" width="12" height="11" rx="1.6" /><path d="M2 6.2h12M2 9.8h12" /></>,
  cols: <><rect x="2" y="2.5" width="12" height="11" rx="1.6" /><path d="M6 2.5v11M10 2.5v11" /></>,
  writes: <path d="M3 13l.6-2.9L10.8 2.9a1.3 1.3 0 0 1 1.9 0l.4.4a1.3 1.3 0 0 1 0 1.9L5.9 12.4z M9.6 4.1l2.3 2.3" />,
  cost: <><circle cx="8" cy="8" r="6" /><path d="M10.1 5.9c-.4-.6-1.2-1-2.1-1-1.2 0-2.1.6-2.1 1.5 0 2.1 4.3 1.1 4.3 3.2 0 .9-1 1.6-2.2 1.6-1 0-1.9-.4-2.3-1.1M8 3.8v8.4" /></>,
};

function Icon({ k }: { k: TileKey }) {
  return <svg className="tn-ico" viewBox="0 0 16 16" aria-hidden="true">{ICONS[k]}</svg>;
}

function DirIcon({ dir }: { dir: Dir }) {
  const d = dir === "up" ? "M5 1.5 9 8.5H1z" : dir === "down" ? "M1 1.5h8L5 8.5z" : dir === "same" ? "M1.5 3.5h7M1.5 6.5h7" : "M1.5 4.2c1.2-1.2 2.3-1.2 3.5 0s2.3 1.2 3.5 0M1.5 7.2c1.2-1.2 2.3-1.2 3.5 0s2.3 1.2 3.5 0";
  return <svg className="tn-dir-ico" viewBox="0 0 10 10" aria-hidden="true"><path d={d} /></svg>;
}

const TONE = /\b(green|grey|gray|blue|amber|red|violet)\b/;

/** The card for one hotspot box. `natW` and `natH` are the drawing's own size (the diagram's width and height);
 *  `idBase` (the diagram's id) keeps the description's id unique when a table is in more than one diagram. */
export function TableNode({ h, t, natW, natH, idBase }: { h: Hotspot; t: SchemaTable; natW: number; natH: number; idBase?: string }) {
  const boxW = Math.max(1, (h.w / 100) * natW);
  const boxH = Math.max(0, (h.h / 100) * natH);
  const keys = keyCols(t);
  const tiles = sizing(t);
  const lay = cardLayout(t, keys, tiles, natW, boxW, boxH);
  const tone = (TONE.exec(h.tone || "")?.[1] || (t.state === "new" ? "green" : "grey")).replace("gray", "grey");
  const desc = (h.label || "").split(" · ").slice(1).join(" · ");
  const chip = t.state === "existing" ? "existing" : t.state;
  const tips = tiles.map((x) => ({ x, tip: tipText(t, x) }));
  const did = `${idBase || "dg"}-${t.id}-sizing`;
  const style = { left: `${h.x}%`, top: `${h.y}%`, width: `${h.w}%`, height: `${h.h}%`, "--fs": ((lay.em * 100) / boxW).toFixed(4) } as CSSProperties;
  return (
    <a className={cx("hs has-table tnode", `tone-${tone}`, /\bdashed\b/.test(h.tone || "") && "dashed", `st-${t.state}`, !lay.strip && "compact")}
      href={`#${t.id}`} style={style}
      data-hs="" data-label={h.label} data-note={h.note || undefined} data-table={t.id} data-tone={h.tone || undefined}
      aria-label={`${t.table}, ${t.state} table, ${(t.columns || []).length} columns. Open table details`} aria-describedby={did}>
      <span className="tn-in">
        <span className="tn-head">
          <b className="tn-name">{t.table}</b>
          <span className={cx("tn-chip", chip)}>{chip}</span>
          {desc ? <span className="tn-desc">{desc}</span> : null}
          {lay.hidden && !lay.moreLine ? <span className="tn-hidden">+{lay.hidden} {lay.hidden === 1 ? "col" : "cols"}</span> : null}
        </span>
        <span className="tn-cols">
          {lay.shown.map(({ c, key, mark }) => (
            <span key={c.name} className={cx("tn-col", key && "tn-key", mark && `ch-${mark}`)}>
              {key ? <i className={cx("tn-k", key.pk ? "pk" : "fk")}>{key.pk ? "PK" : "FK"}</i> : null}
              {key?.pk && key.fk ? <i className="tn-k fk">FK</i> : null}
              <code className="tn-cn">{c.name}</code>
              {key?.fk ? <span className="tn-ref">→ {key.fk}</span> : null}
              {mark ? <span className={cx("tn-tag", mark)}>{mark}</span> : null}
              <span className="tn-ty">{baseType(c.type)}</span>
            </span>
          ))}
          {lay.moreLine ? <span className="tn-more">+{lay.hidden} more {lay.hidden === 1 ? "column" : "columns"} · open the table for all</span> : null}
        </span>
        {lay.strip ? (
          <span className={cx("tn-strip", lay.wrap && "wrap")}>
            {tips.filter(({ x }) => STRIP.includes(x.key)).map(({ x, tip }) => (
              <span key={x.key} className={cx("tn-tile", `k-${x.key}`)} data-tip={tip} title={tip}>
                <span className="tn-l"><Icon k={x.key} />{x.label}</span>
                <span className="tn-t1">
                  {x.now.missing ? <span className="tn-v none">not measured</span>
                    : <><b className="tn-v">{x.now.value}</b>{x.now.unit ? <span className="tn-u">{x.now.unit}</span> : null}</>}
                </span>
                <span className="tn-t2">
                  {x.next.missing ? <span className="tn-d none">not estimated</span> : (
                    <span className={cx("tn-d", x.next.dir)}><DirIcon dir={x.next.dir} /><b>{x.next.value}</b></span>
                  )}
                </span>
              </span>
            ))}
          </span>
        ) : null}
        {tips.filter(({ x }) => !lay.strip || !STRIP.includes(x.key)).map(({ x, tip }) => <span key={x.key} className="tn-extra" hidden data-tip={tip} />)}
        <span className="sr-only" id={did}>{tips.map(({ tip }) => tip.replace(/\n/g, ". ")).join(". ")}</span>
      </span>
    </a>
  );
}
