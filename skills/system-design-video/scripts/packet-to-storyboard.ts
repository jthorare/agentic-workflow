// Lay out whiteboard scenes from a /system-design packet's mermaid sources, so the boards match the doc's diagrams.
//
//   bun packet-to-storyboard.ts <packet-dir> --keys model,pr-order [--out storyboard.json] [--inks inks.json] [--colors colors.json]
//
// Reads <packet>/packet.json for each diagram key's .mmd path and the section that embeds it (the beat's `src`), and
// <packet>/diagrams/colors.json (the same file the doc's diagrams were styled with) for node colors.
// Exit: 0 written, 1 bad arguments; an unknown diagram key, a missing packet.json or an empty .mmd throws (non-zero).
// Supports the mermaid subset /system-design draws: flowchart TB|LR with nested subgraphs, node shapes ["x"] [("x")] {"x"} ("x"),
// edges --> -.-> ==> --- with |labels|, and stateDiagram-v2 with [*], "A --> B: label" and "A: description".
// Layout: layered (longest path, barycenter order), subgraphs laid out first and placed as one block, arrows clipped to box edges.
// Output: a storyboard with one beat per subgraph (or per layer of loose nodes); each `say` is a scaffold (the group title or its
// node names). Rewrite every `say` into one spoken sentence before rendering; keep the `src`.
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

type Node = { id: string; label: string; group: string | null; w: number; h: number; x: number; y: number; lines: string[] };
type Group = { id: string; title: string; parent: string | null; x: number; y: number; w: number; h: number; lines: string[]; th: number };
type Edge = { from: string; to: string; label: string; dashed: boolean };
type Graph = { dir: "TB" | "LR"; nodes: Map<string, Node>; groups: Map<string, Group>; edges: Edge[] };

const SIZE = 30, LH = 38, CHAR = 0.6, WRAP = 30, PAD = 24, TITLE = 36, GAP_LAYER = 120, GAP_SIB = 70, GPAD = 40;
const textW = (s: string, size = SIZE) => s.length * size * CHAR;

// ---- parse ----------------------------------------------------------------------------------------------
const clean = (s: string) => s.replace(/<br\s*\/?>/gi, "\n").replace(/&quot;/g, '"').replace(/&amp;/g, "&").replace(/^"|"$/g, "").trim();
const wrap = (label: string, width = WRAP): string[] =>
  label.split("\n").flatMap((ln) => {
    const out: string[] = [];
    let cur = "";
    for (const word of ln.split(" ")) {
      if (cur && (cur + " " + word).length > width) { out.push(cur); cur = word; } else cur = cur ? cur + " " + word : word;
    }
    return out.concat(cur);
  });

const NODE = /^\s*([A-Za-z_]\w*)(?:\[\(\s*"?(.*?)"?\s*\)\]|\[\s*"(.*?)"\s*\]|\{\s*"?(.*?)"?\s*\}|\(+\s*"?(.*?)"?\s*\)+|\[([^\]"]*)\])?/;
const EDGE = /^\s*(-->|-\.->|==>|---|-\.-)(?:\s*\|"?([^|]*?)"?\|)?/;

export function parseMermaid(src: string): Graph {
  const lines = src.split("\n").map((l) => l.replace(/%%.*$/, "")).filter((l) => l.trim());
  if (!lines.length) throw new Error("empty mermaid source");
  const head = lines[0].trim();
  const state = head.startsWith("stateDiagram");
  const g: Graph = { dir: /\b(LR|RL)\b/.test(head) ? "LR" : "TB", nodes: new Map(), groups: new Map(), edges: [] };
  const stack: string[] = [];
  const ensure = (id: string, label?: string) => {
    let n = g.nodes.get(id);
    if (!n) { n = { id, label: id, group: stack.at(-1) ?? null, w: 0, h: 0, x: 0, y: 0, lines: [] }; g.nodes.set(id, n); }
    if (label !== undefined) n.label = clean(label);
    return n;
  };
  for (const raw of lines.slice(1)) {
    const line = raw.trim();
    const dir = line.match(/^direction\s+(TB|TD|LR|RL|BT)/);
    if (dir) { if (!stack.length) g.dir = /LR|RL/.test(dir[1]) ? "LR" : "TB"; continue; }
    const sg = line.match(/^subgraph\s+(\w+)\s*(?:\[\s*"?(.*?)"?\s*\])?\s*$/);
    if (sg) { g.groups.set(sg[1], { id: sg[1], title: clean(sg[2] ?? sg[1]), parent: stack.at(-1) ?? null, x: 0, y: 0, w: 0, h: 0, lines: [], th: 0 }); stack.push(sg[1]); continue; }
    if (line === "end") { stack.pop(); continue; }
    if (state) {
      const m = line.match(/^(\[\*\]|\w+)\s*-->\s*(\[\*\]|\w+)\s*(?::\s*(.*))?$/);
      if (m) {
        const id = (s: string, role: "s" | "e") => (s === "[*]" ? (role === "s" ? "__start" : "__end") : s);
        const a = id(m[1], "s"), b = id(m[2], "e");
        ensure(a, a === "__start" ? "start" : undefined); ensure(b, b === "__end" ? "end" : undefined);
        g.edges.push({ from: a, to: b, label: clean(m[3] ?? ""), dashed: false });
        continue;
      }
      const d = line.match(/^(\w+)\s*:\s*(.+)$/);
      if (d) ensure(d[1], d[2]);
      continue;
    }
    // flowchart statement: node (edge node)*
    let rest = line;
    let m = rest.match(NODE);
    if (!m) continue;
    const label = m[2] ?? m[3] ?? m[4] ?? m[5] ?? m[6];
    let prev = ensure(m[1], label);
    rest = rest.slice(m[0].length);
    for (let e = rest.match(EDGE); e; e = rest.match(EDGE)) {
      rest = rest.slice(e[0].length);
      m = rest.match(NODE);
      if (!m) break;
      const next = ensure(m[1], m[2] ?? m[3] ?? m[4] ?? m[5] ?? m[6]);
      g.edges.push({ from: prev.id, to: next.id, label: clean(e[2] ?? ""), dashed: e[1].includes(".") });
      rest = rest.slice(m[0].length);
      prev = next;
    }
  }
  for (const n of g.nodes.values()) {
    n.lines = wrap(n.label);
    n.w = Math.max(170, ...n.lines.map((l) => textW(l))) + PAD * 2;
    n.h = n.lines.length * LH + PAD * 1.4;
  }
  for (const gr of g.groups.values()) { gr.lines = wrap(gr.title, 44); gr.th = 28 + gr.lines.length * 44; }
  return g;
}

// ---- layout ---------------------------------------------------------------------------------------------
type Item = { key: string; w: number; h: number; x: number; y: number };

/** Layered placement of items; `edges` are pairs of item keys. Returns the block's size; item x,y are relative to its top-left. */
function layer(items: Item[], edges: [string, string][], dir: "TB" | "LR"): { w: number; h: number } {
  const idx = new Map(items.map((it, i) => [it.key, i]));
  const out = new Map<string, string[]>();
  for (const [a, b] of edges) if (a !== b && idx.has(a) && idx.has(b)) (out.get(a) ?? out.set(a, []).get(a)!).push(b);
  // drop back edges (cycles) found by DFS so longest-path layering terminates
  const state = new Map<string, number>(), dag = new Map<string, string[]>();
  const dfs = (v: string) => {
    state.set(v, 1);
    for (const w of out.get(v) ?? []) {
      if (state.get(w) === 1) continue;
      (dag.get(v) ?? dag.set(v, []).get(v)!).push(w);
      if (!state.has(w)) dfs(w);
    }
    state.set(v, 2);
  };
  for (const it of items) if (!state.has(it.key)) dfs(it.key);
  const lvl = new Map<string, number>(items.map((it) => [it.key, 0]));
  for (let pass = 0; pass < items.length; pass++)
    for (const [v, ws] of dag) for (const w of ws) if (lvl.get(w)! < lvl.get(v)! + 1) lvl.set(w, lvl.get(v)! + 1);
  const rows: Item[][] = [];
  for (const it of items) (rows[lvl.get(it.key)!] ??= []).push(it);
  const preds = new Map<string, string[]>();
  for (const [v, ws] of dag) for (const w of ws) (preds.get(w) ?? preds.set(w, []).get(w)!).push(v);
  const pos = new Map<string, number>();
  rows.forEach((r) => r?.forEach((it, i) => pos.set(it.key, i)));
  for (let sweep = 0; sweep < 3; sweep++)
    for (const r of rows) {
      if (!r) continue;
      const bary = (it: Item) => { const p = preds.get(it.key); return p?.length ? p.reduce((s, k) => s + pos.get(k)!, 0) / p.length : pos.get(it.key)!; };
      r.sort((a, b) => bary(a) - bary(b));
      r.forEach((it, i) => pos.set(it.key, i));
    }
  const tb = dir === "TB";
  const rowSize = (r: Item[]) => r.reduce((s, it) => s + (tb ? it.w : it.h), 0) + GAP_SIB * (r.length - 1);
  const live = rows.filter((r) => r?.length);
  const span = Math.max(...live.map(rowSize));
  let along = 0;
  for (const r of live) {
    const thick = Math.max(...r.map((it) => (tb ? it.h : it.w)));
    let across = (span - rowSize(r)) / 2;
    for (const it of r) {
      if (tb) { it.x = across; it.y = along + (thick - it.h) / 2; across += it.w + GAP_SIB; }
      else { it.y = across; it.x = along + (thick - it.w) / 2; across += it.h + GAP_SIB; }
    }
    along += thick + GAP_LAYER;
  }
  along -= GAP_LAYER;
  return tb ? { w: span, h: along } : { w: along, h: span };
}

export function layout(g: Graph) {
  const ancestors = (id: string, isNode: boolean): string[] => {
    const chain: string[] = [];
    let p = isNode ? g.nodes.get(id)!.group : g.groups.get(id)!.parent;
    while (p) { chain.unshift(p); p = g.groups.get(p)!.parent; }
    return chain; // outermost first
  };
  const children = (gid: string | null) => [
    ...[...g.groups.values()].filter((x) => x.parent === gid).map((x) => ({ key: "g:" + x.id, w: 0, h: 0, x: 0, y: 0 })),
    ...[...g.nodes.values()].filter((x) => x.group === gid).map((x) => ({ key: "n:" + x.id, w: 0, h: 0, x: 0, y: 0 })),
  ];
  // edge -> the container it is drawn inside, with endpoints mapped to that container's children
  const byContainer = new Map<string | null, [string, string][]>();
  for (const e of g.edges) {
    const A = ["", ...ancestors(e.from, true)], B = ["", ...ancestors(e.to, true)];
    let i = 0;
    while (i < A.length && i < B.length && A[i] === B[i]) i++;
    const container = A[i - 1] || null;
    const pick = (chain: string[], id: string) => (chain[i] !== undefined ? "g:" + chain[i] : "n:" + id);
    (byContainer.get(container) ?? byContainer.set(container, []).get(container)!).push([pick(A, e.from), pick(B, e.to)]);
  }
  // second pass assigns absolute coordinates top-down
  const assign = (gid: string | null, ox: number, oy: number, tree: { items: Item[] }) => {
    for (const it of tree.items) {
      if (it.key.startsWith("n:")) { const n = g.nodes.get(it.key.slice(2))!; n.x = ox + it.x; n.y = oy + it.y; }
      else {
        const gr = g.groups.get(it.key.slice(2))!; gr.x = ox + it.x; gr.y = oy + it.y;
        assign(gr.id, gr.x + (gr.w - trees.get(gr.id)!.w) / 2, gr.y + gr.th, trees.get(gr.id)!);
      }
    }
  };
  const trees = new Map<string | null, { w: number; h: number; items: Item[] }>();
  const placeAll = (gid: string | null) => {
    const items = children(gid);
    for (const it of items) {
      if (it.key.startsWith("n:")) { const n = g.nodes.get(it.key.slice(2))!; it.w = n.w; it.h = n.h; }
      else { const id = it.key.slice(2); placeAll(id); const t = trees.get(id)!, gr = g.groups.get(id)!; gr.w = Math.max(t.w + GPAD * 2, Math.max(...gr.lines.map((l) => textW(l, TITLE))) + 48); gr.h = t.h + GPAD + gr.th; it.w = gr.w; it.h = gr.h; }
    }
    const size = items.length ? layer(items, byContainer.get(gid) ?? [], g.dir) : { w: 0, h: 0 };
    trees.set(gid, { ...size, items });
  };
  placeAll(null);
  assign(null, 0, 0, trees.get(null)!);
  const root = trees.get(null)!;
  return { w: root.w, h: root.h };
}

// ---- storyboard -----------------------------------------------------------------------------------------
const TL_INK: Record<string, string> = { blue: "primary", violet: "secondary", green: "tertiary", red: "danger", grey: "chalk" };
type Item2 = Record<string, unknown>;
type Rect = { x: number; y: number; w: number; h: number };

function clip(r: Rect, toward: [number, number]): [number, number] {
  const cx = r.x + r.w / 2, cy = r.y + r.h / 2, dx = toward[0] - cx, dy = toward[1] - cy;
  if (!dx && !dy) return [cx, cy];
  const t = Math.min(dx ? r.w / 2 / Math.abs(dx) : Infinity, dy ? r.h / 2 / Math.abs(dy) : Infinity);
  return [cx + dx * t, cy + dy * t];
}

export function scene(g: Graph, ox: number, oy: number, colors: [string, string][], src: string | undefined) {
  const inkOf = (label: string) => TL_INK[colors.find(([needle]) => label.includes(needle))?.[1] ?? "grey"] ?? "chalk";
  const rect = (id: string): Rect => (g.nodes.has(id) ? g.nodes.get(id)! : g.groups.get(id)!);
  const at = (r: Rect): Rect => ({ x: r.x + ox, y: r.y + oy, w: r.w, h: r.h });
  const nodeItems = (n: Node): Item2[] => {
    const ink = inkOf(n.label), r = at(n);
    const items: Item2[] = [{ type: "box", x: r.x, y: r.y, w: r.w, h: r.h, color: ink, fill: ink !== "chalk" }];
    n.lines.forEach((l, i) => items.push({ type: "text", text: l, x: r.x + r.w / 2, y: r.y + PAD * 0.7 + SIZE * 0.85 + i * LH, size: SIZE, color: ink, anchor: "middle" }));
    return items;
  };
  const groupItems = (gr: Group): Item2[] => {
    const r = at(gr), items: Item2[] = [{ type: "box", x: r.x, y: r.y, w: r.w, h: r.h, color: "chalk", alpha: 0.55 }];
    gr.lines.forEach((l, i) => items.push({ type: "text", text: l, x: r.x + 24, y: r.y + 14 + TITLE * 0.9 + i * 44, size: TITLE, color: "chalk", anchor: "start", bold: true, halo: true }));
    return items;
  };
  const edgeItems = (e: Edge): Item2[] => {
    const a = at(rect(e.from)), b = at(rect(e.to));
    const dashed = e.dashed ? 0.6 : undefined;
    if (e.from === e.to) {
      const items: Item2[] = [{ type: "arrow", from: [a.x + a.w * 0.7, a.y], via: [[a.x + a.w * 0.7, a.y - 80], [a.x + a.w * 0.3, a.y - 80]], to: [a.x + a.w * 0.3, a.y], color: "chalk", alpha: dashed }];
      if (e.label) items.push({ type: "text", text: e.label, x: a.x + a.w / 2, y: a.y - 100, size: 24, color: "chalk", anchor: "middle", halo: true });
      return items;
    }
    const ca: [number, number] = [a.x + a.w / 2, a.y + a.h / 2], cb: [number, number] = [b.x + b.w / 2, b.y + b.h / 2];
    const rev = g.edges.some((o) => o.from === e.to && o.to === e.from);
    const len = Math.hypot(cb[0] - ca[0], cb[1] - ca[1]) || 1;
    const off = rev ? (e.from < e.to ? 24 : -24) : 0, nx = (-(cb[1] - ca[1]) / len) * off, ny = ((cb[0] - ca[0]) / len) * off;
    // an arrow that would cross another box bends through one waypoint that clears every box
    const rects = [...g.nodes.values()].filter((n) => n.id !== e.from && n.id !== e.to).map(at);
    const hits = (p: [number, number], q: [number, number]) => {
      for (let i = 0; i <= 40; i++) {
        const x = p[0] + ((q[0] - p[0]) * i) / 40, y = p[1] + ((q[1] - p[1]) * i) / 40;
        if (rects.some((r) => x > r.x - 14 && x < r.x + r.w + 14 && y > r.y - 14 && y < r.y + r.h + 14)) return true;
      }
      return false;
    };
    let via: [number, number] | undefined;
    if (hits(ca, cb)) {
      for (const k of [100, -100, 200, -200, 320, -320, 460, -460, 620, -620]) {
        const m: [number, number] = [(ca[0] + cb[0]) / 2 - ((cb[1] - ca[1]) / len) * k, (ca[1] + cb[1]) / 2 + ((cb[0] - ca[0]) / len) * k];
        if (!hits(ca, m) && !hits(m, cb)) { via = m; break; }
      }
    }
    const from = clip(a, via ?? [cb[0] + nx, cb[1] + ny]), to = clip(b, via ?? [ca[0] + nx, ca[1] + ny]);
    const pts: [number, number][] = [[from[0] + nx, from[1] + ny], ...(via ? [via] : []), [to[0] + nx, to[1] + ny]];
    const items: Item2[] = [{ type: "arrow", from: pts[0], to: pts.at(-1), ...(via ? { via: [via] } : {}), color: "chalk", alpha: dashed }];
    if (e.label) {
      // slide the label along the arrow to the first spot that sits on no box
      const hw = textW(e.label, 24) / 2, all = [...g.nodes.values()].map(at);
      const along = (t: number): [number, number] => {
        const k = (pts.length - 1) * t, i = Math.min(pts.length - 2, Math.floor(k)), f = k - i;
        return [pts[i][0] + (pts[i + 1][0] - pts[i][0]) * f, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * f + 8];
      };
      const spot = [0.5, 0.4, 0.6, 0.3, 0.7, 0.2, 0.8].map(along)
        .find(([px, py]) => !all.some((r) => px + hw > r.x && px - hw < r.x + r.w && py + 8 > r.y && py - 26 < r.y + r.h)) ?? along(0.5);
      items.push({ type: "text", text: e.label, x: spot[0], y: spot[1], size: 24, color: "chalk", anchor: "middle", halo: true });
    }
    return items;
  };

  // one beat per top-level container (a subgraph, or one layer of loose nodes); cross edges ride with the later endpoint's beat
  const topOf = (id: string) => { let n = g.nodes.get(id)!; let gid = n.group; if (!gid) return "n:" + id; while (g.groups.get(gid)!.parent) gid = g.groups.get(gid)!.parent!; return "g:" + gid; };
  const tops = new Map<string, { key: string; title: string; r: Rect; items: Item2[] }>();
  const order: string[] = [];
  const top = (key: string, title: string, r: Rect) => { if (!tops.has(key)) { tops.set(key, { key, title, r, items: [] }); order.push(key); } return tops.get(key)!; };
  const groupsTop = [...g.groups.values()].filter((x) => !x.parent).sort((p, q) => p.y - q.y || p.x - q.x);
  const loose = [...g.nodes.values()].filter((n) => !n.group);
  const seq = [...groupsTop.map((x) => ({ k: "g:" + x.id, y: x.y, x: x.x })), ...loose.map((n) => ({ k: "n:" + n.id, y: n.y, x: n.x }))]
    .sort((p, q) => (g.dir === "TB" ? p.y - q.y || p.x - q.x : p.x - q.x || p.y - q.y));
  for (const s of seq) {
    if (s.k.startsWith("g:")) { const gr = g.groups.get(s.k.slice(2))!; top(s.k, gr.title, gr).items.push(...groupItems(gr)); }
    else { const n = g.nodes.get(s.k.slice(2))!; top(s.k, n.label.split("\n")[0], n).items.push(...nodeItems(n)); }
  }
  for (const n of g.nodes.values()) if (n.group) {
    let gid = n.group; const chain = [gid];
    while (g.groups.get(gid)!.parent) { gid = g.groups.get(gid)!.parent!; chain.push(gid); }
    const t = tops.get("g:" + gid)!;
    // nested group boxes first, then the node, in layout order
    for (const c of chain.slice(0, -1).reverse()) { const cg = g.groups.get(c)!; if (!(t as any).done?.has(c)) { ((t as any).done ??= new Set()).add(c); t.items.push(...groupItems(cg)); } }
    t.items.push(...nodeItems(n));
  }
  const rank = new Map(order.map((k, i) => [k, i]));
  for (const e of g.edges) {
    const later = [topOf(e.from), topOf(e.to)].sort((p, q) => rank.get(q)! - rank.get(p)!)[0];
    tops.get(later)!.items.push(...edgeItems(e));
  }
  // merge consecutive loose nodes of one layer into one beat so a 12-node chain is not 12 beats
  const beats: Item2[] = [];
  const bounds = (items: Item2[]) => {
    let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
    for (const it of items) {
      const pts: [number, number][] = it.type === "box" ? [[it.x as number, it.y as number], [(it.x as number) + (it.w as number), (it.y as number) + (it.h as number)]]
        : it.type === "arrow" ? [it.from as [number, number], it.to as [number, number]]
        : [[(it.x as number) - textW(String(it.text), it.size as number) / 2, (it.y as number) - 30], [(it.x as number) + textW(String(it.text), it.size as number) / 2, it.y as number]];
      for (const [px, py] of pts) { x0 = Math.min(x0, px); y0 = Math.min(y0, py); x1 = Math.max(x1, px); y1 = Math.max(y1, py); }
    }
    return { x0, y0, x1, y1 };
  };
  for (const k of order) {
    const t = tops.get(k)!;
    const b = bounds(t.items);
    const z = Math.min(1.1, 1700 / (b.x1 - b.x0 + 120), 780 / (b.y1 - b.y0 + 120));
    beats.push({ id: `${t.key.replace(/[^\w]/g, "-")}`, cam: [Math.round((b.x0 + b.x1) / 2), Math.round((b.y0 + b.y1) / 2 + 40), Math.round(z * 100) / 100], say: `${t.title}.`, ...(src ? { src } : {}), items: t.items });
  }
  return { beats, w: g.nodes.size ? Math.max(...[...g.nodes.values()].map((n) => n.x + n.w)) : 0 };
}

// ---- cli ------------------------------------------------------------------------------------------------
if (import.meta.main) {
  const args = process.argv.slice(2);
  const flag = (n: string) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : undefined; };
  const dir = args.find((a, i) => !a.startsWith("--") && !args[i - 1]?.startsWith("--"));
  const keys = flag("--keys")?.split(",");
  if (!dir || !keys) { console.error("usage: bun packet-to-storyboard.ts <packet-dir> --keys a,b [--out storyboard.json] [--inks inks.json] [--colors colors.json]"); process.exit(1); }
  const root = resolve(dir);
  const manifest = JSON.parse(readFileSync(join(root, "packet.json"), "utf8"));
  const colorsPath = flag("--colors") ?? join(root, "diagrams", "colors.json");
  let colorMap: Record<string, [string, string][]> = {};
  try { colorMap = JSON.parse(readFileSync(colorsPath, "utf8")); } catch { console.warn(`no colors file at ${colorsPath}: every node draws in the neutral ink`); }
  const inks = flag("--inks") ? JSON.parse(readFileSync(resolve(flag("--inks")!), "utf8"))
    : { board: "color.surface.board", chalk: "color.ink.chalk", primary: "color.accent.primary", secondary: "color.accent.secondary", tertiary: "color.accent.tertiary", danger: "color.accent.danger" };
  const beats: Item2[] = [];
  let ox = 0;
  for (const key of keys) {
    const d = manifest.diagrams?.[key];
    if (!d?.mmd) throw new Error(`packet.json has no diagram "${key}" (has: ${Object.keys(manifest.diagrams ?? {}).join(", ")})`);
    const g = parseMermaid(readFileSync(join(root, d.mmd), "utf8"));
    const size = layout(g);
    const sec = (manifest.sections ?? []).find((s: { diagrams?: string[] }) => s.diagrams?.includes(key));
    const src = sec ? `TDD ${sec.anchor}` : undefined;
    if (!src) console.warn(`diagram "${key}" is embedded in no TDD section: its beats get no src; add the section or drop the scene`);
    const s = scene(g, ox, 0, colorMap[key] ?? [], src);
    beats.push(...s.beats);
    console.log(`${key}: ${g.nodes.size} nodes, ${g.groups.size} subgraphs, ${g.edges.length} edges, ${Math.round(size.w)}x${Math.round(size.h)} world units, ${s.beats.length} beats${src ? `, src ${src}` : ""}`);
    ox += size.w + 600;
  }
  const out = { inks, strokeWidth: 5, lead: 0.4, tail: 0.9, speed: 1.8, beats };
  const outPath = resolve(flag("--out") ?? join(root, "storyboard.json"));
  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, JSON.stringify(out, null, 1));
  console.log(`wrote ${outPath}: ${beats.length} beats. Rewrite each "say" as one spoken sentence, then render.`);
}
