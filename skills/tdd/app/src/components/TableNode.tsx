// TableNode (D26): an HTML card laid exactly over a schema-diagram box whose hotspot carries a `table` with a card in
// content.schema. The SVG box stays underneath, covered. The card is the hotspot itself (`a.hs.has-table.tnode`, the
// same data-hs / data-table attributes), so hover still shows the hotspot card, a click still opens the drawer, and it
// is keyboard focusable. It shows the table name and state chip, a column list with primary and foreign keys set
// larger and badged (PK / FK, "→ table" when the note, type or a FOREIGN KEY line names one), changed columns in their
// change colours, and the standard sizing strip: rows, columns, writes and cost tiles, each with the production value
// (or "not measured") and the expected change after the design (or "not estimated") as a green, red or neutral delta.
//
// Everything scales with the drawing: the card is a size container and its text is sized in cqw so that 1em is 16 px
// of the drawing's own coordinates (the diagram's natural width), at fit and at "Actual size" alike. How many column
// lines fit is worked out here, in drawing px, from the box height, so the server render and the client agree and the
// card reads the same with JavaScript off. Pure render: no Date, random or window.
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
  { key: "writes", label: "writes", title: "Writes" },
  { key: "cost", label: "cost", title: "Cost" },
];

/** The four standard sizing tiles for a table card. Values come only from the card: measured.rows / writes / cost /
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

// Heights in em (schema.css sizes .tnode in em to match): the head band and its gap (2 + .3), a column line (1.4), a
// key line (1.4 at 1.1em), the "+N more" line (1.5 at .8em), and FIXED: head, the strip (.3 gap + a three-row tile of
// about 3.8), the bottom padding with the frame's two borders (.4 + 2 × .125) and a small safety margin.
const COL_EM = 1.4, KEY_EM = 1.54, MORE_EM = 1.2, FIXED_EM = 7.25;
// 1em is sized so that it shows at about 11 px when the page fits the drawing to a 1300 px column (1440 viewport):
// TEXT_PX × natural width / 1300 drawing px, kept between 12 and 28; a card whose box is too short or too narrow for
// its head, strip and four tiles gets a smaller em of its own.
const TEXT_PX = 11, FIT_COLUMN_PX = 1300, EM_MIN = 12, EM_MAX = 28, WIDTH_EM = 26;

/** The card's em in drawing px. */
export function cardEm(natW: number, boxW: number, boxH: number) {
  const em = Math.min(EM_MAX, Math.max(EM_MIN, (TEXT_PX * natW) / FIT_COLUMN_PX));
  return Math.max(4, Math.min(em, boxH ? boxH / FIXED_EM : em, boxW / WIDTH_EM));
}

interface Line { c: Column; i: number; key?: ColKey; mark: string }

/** The column lines that fit `availEm` (keys first, then changed columns, then the rest, shown in table order), and
 *  whether the "+N more" line fits under them (else the count goes in the head). */
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
  let shown = take(availEm);
  let moreLine = false;
  if (shown.length < lines.length) {
    const withMore = take(availEm - MORE_EM);
    if (withMore.length) { shown = withMore; moreLine = true; }
  }
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

/** The card for one hotspot box. `natW` and `natH` are the drawing's own size (the diagram's width and height). */
export function TableNode({ h, t, natW, natH }: { h: Hotspot; t: SchemaTable; natW: number; natH: number }) {
  const boxW = Math.max(1, (h.w / 100) * natW);
  const boxH = Math.max(0, (h.h / 100) * natH);
  const em = cardEm(natW, boxW, boxH);
  return <TableCardNode h={h} t={t} em={em} boxW={boxW} availEm={boxH / em - FIXED_EM} />;
}

function TableCardNode({ h, t, em, boxW, availEm }: { h: Hotspot; t: SchemaTable; em: number; boxW: number; availEm: number }) {
  const keys = keyCols(t);
  const { shown, hidden, moreLine } = fitColumns(t, keys, availEm);
  const tiles = sizing(t);
  const tone = (TONE.exec(h.tone || "")?.[1] || (t.state === "new" ? "green" : "grey")).replace("gray", "grey");
  const desc = (h.label || "").split(" · ").slice(1).join(" · ");
  const chip = t.state === "existing" ? "existing" : t.state;
  const summary = tiles.map((x) => `${x.label} ${x.now.missing ? "not measured" : [x.now.value, x.now.unit].filter(Boolean).join(" ")}, after the design ${x.next.missing ? "not estimated" : [x.next.value, x.next.unit].filter(Boolean).join(" ")}`).join("; ");
  const style = { left: `${h.x}%`, top: `${h.y}%`, width: `${h.w}%`, height: `${h.h}%`, "--fs": ((em * 100) / boxW).toFixed(4) } as CSSProperties;
  return (
    <a className={cx("hs has-table tnode", `tone-${tone}`, /\bdashed\b/.test(h.tone || "") && "dashed", `st-${t.state}`)} href={`#${t.id}`} style={style}
      data-hs="" data-label={h.label} data-note={h.note || undefined} data-table={t.id} data-tone={h.tone || undefined}
      aria-label={`${t.table}, ${t.state} table, ${(t.columns || []).length} columns. ${summary}. Open table details`}>
      <span className="tn-in">
        <span className="tn-head">
          <b className="tn-name">{t.table}</b>
          <span className={cx("tn-chip", chip)}>{chip}</span>
          {desc ? <span className="tn-desc">{desc}</span> : null}
          {hidden && !moreLine ? <span className="tn-hidden">+{hidden} cols</span> : null}
        </span>
        <span className="tn-cols">
          {shown.map(({ c, key, mark }) => (
            <span key={c.name} className={cx("tn-col", key && "key", mark && `ch-${mark}`)}>
              {key ? <i className={cx("tn-k", key.pk ? "pk" : "fk")}>{key.pk ? "PK" : "FK"}</i> : null}
              {key?.pk && key.fk ? <i className="tn-k fk">FK</i> : null}
              <code className="tn-cn">{c.name}</code>
              {key?.fk ? <span className="tn-ref">→ {key.fk}</span> : null}
              {mark ? <span className={cx("tn-tag", mark)}>{mark}</span> : null}
              <span className="tn-ty">{baseType(c.type)}</span>
            </span>
          ))}
          {moreLine ? <span className="tn-more">+{hidden} more {hidden === 1 ? "column" : "columns"} · open the table for all</span> : null}
        </span>
        <span className="tn-strip">
          {tiles.map((x) => {
            const tip = tipText(t, x);
            return (
              <span key={x.key} className={cx("tn-tile", `k-${x.key}`)} data-tip={tip} title={tip}>
                <span className="tn-l"><Icon k={x.key} />{x.label}</span>
                <span className="tn-t1">
                  {x.now.missing ? <span className="tn-v none">not measured</span>
                    : <><b className="tn-v">{x.now.value}</b>{x.now.unit ? <span className="tn-u">{x.now.unit}</span> : null}</>}
                </span>
                <span className="tn-t2">
                  {x.next.missing ? <span className="tn-d none">not estimated</span> : (
                    <span className={cx("tn-d", x.next.dir)}>
                      <DirIcon dir={x.next.dir} />
                      <b>{x.next.value}</b>{x.next.unit ? <span className="tn-du">{x.next.unit}</span> : null}
                    </span>
                  )}
                </span>
              </span>
            );
          })}
        </span>
      </span>
    </a>
  );
}
