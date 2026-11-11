// Schema drawer harness (D7): renders a standalone page for screenshots and interaction checks. The page holds the
// packet's schema diagram (its table boxes drawn as table cards, D26) and the real <SchemaDrawer/>, server-rendered
// closed and hydrated by a bundled client, styled by templates/tdd-system/tdd.css + src/schema.css exactly as the
// artifact page is.
//
// Usage: bun harness/render.tsx <packet build dir (holds content.json)> <out.html> [--diagram <key>] [--extra <schema.json> ...]
//        [--stub-claude] [--data-sources <file.json | inline JSON array>]
// --extra adds a schema file (for example ../templates/schema.template.json) to the page's tables with the compiler's
// defaults (id tbl-<slug>, state existing, change unchanged for existing tables and new otherwise), to preview a file
// before it is in a packet.
// --stub-claude installs a fake window.claude before the page script (as the viewer does) so the DataChat UI can be shot:
// its `sample` streams a canned reply that says "harness stub" and runs the page's real tools (get_table, get_section,
// and query_source when data sources are set); its `mcp` (only with --data-sources) answers listTools and a stub query.
// Nothing reaches Claude or any connector. Without the flag `use()` is absent and the chat shows its quiet line.
// --data-sources sets content.meta.dataSources (references/data-view.md) to exercise the connector hook.
// Then:  node harness/shoot.mjs <out.html> <png dir> <table> [<table> ...]
// Exit 0; 1 on a missing content.json, a failed bundle, or no schema tables in the content.
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { renderToStaticMarkup, renderToString } from "react-dom/server";
import { ContentCtx } from "../src/lib/context";
import { SchemaDrawer } from "../src/components/SchemaDrawer";
import { TableNode } from "../src/components/TableNode";
import { cx } from "../src/lib/raw";
import type { Content, Hotspot, Node } from "../src/types";

const HERE = dirname(new URL(import.meta.url).pathname);
const TDD_CSS = resolve(HERE, "../../templates/tdd-system/tdd.css");
const SCHEMA_CSS = resolve(HERE, "../src/schema.css");

const args = process.argv.slice(2);
const di = args.indexOf("--diagram");
const key = di >= 0 ? args.splice(di, 2)[1] : "schema";
const extras: string[] = [];
for (let i = args.indexOf("--extra"); i >= 0; i = args.indexOf("--extra")) extras.push(args.splice(i, 2)[1]);
const si = args.indexOf("--stub-claude");
const stubClaude = si >= 0;
if (stubClaude) args.splice(si, 1);
const dsi = args.indexOf("--data-sources");
const dsArg = dsi >= 0 ? args.splice(dsi, 2)[1] : "";
const [buildArg, outArg] = args;
if (!buildArg || !outArg) {
  console.error("usage: bun harness/render.tsx <build-dir> <out.html> [--diagram <key>]");
  process.exit(1);
}
const buildDir = resolve(buildArg);
const cpath = join(buildDir, "content.json");
if (!existsSync(cpath)) { console.error(`${cpath}: not found (run build_tdd.py first)`); process.exit(1); }
const content: Content = JSON.parse(await Bun.file(cpath).text());
const slug = (x: string) => x.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 48) || "section";
for (const f of extras) {
  const t = JSON.parse(await Bun.file(resolve(f)).text());
  t.table ||= f.split("/").pop()!.replace(/\.json$/, "");
  t.state ||= "existing";
  t.id = "tbl-" + slug(t.table);
  for (const c of t.columns || []) c.change ||= t.state === "existing" ? "unchanged" : "new";
  (content.schema ||= []).push(t);
  content.defs[t.id] = { kind: "table", title: `${t.table} (${t.state})`, text: t.summary || "" };
}
if (!content.schema?.length) { console.error(`${cpath}: no schema tables (add <packet>/schema/*.json)`); process.exit(1); }
if (dsArg) {
  const txt = dsArg.trim().startsWith("[") ? dsArg : await Bun.file(resolve(dsArg)).text();
  (content.meta as any).dataSources = JSON.parse(txt);
}

/** The stub runtime: the same `use`-only window.claude shape the viewer gives a page (claude.d.ts), with canned
 *  behaviour. Plain JS, injected before the client bundle so it exists before any page script runs. */
const STUB = `(() => {
  const wait = (ms, signal) => new Promise((res, rej) => { const t = setTimeout(res, ms); signal && signal.addEventListener("abort", () => { clearTimeout(t); rej({ code: "cancelled", message: "aborted" }); }); });
  const sample = async (input, opts = {}) => {
    const turns = Array.isArray(input) ? input : [{ role: "user", content: String(input) }];
    const q = turns[turns.length - 1].content;
    const bytes = turns.reduce((a, t) => a + t.content.length, 0);
    const tools = opts.tools || [];
    const ran = [];
    await wait(400, opts.signal);
    const call = async (name, args) => { const t = tools.find((x) => x.name === name); if (!t) return; try { const out = await t.execute(args, { signal: opts.signal || new AbortController().signal }); ran.push(name + "(" + JSON.stringify(args) + ") -> " + String(typeof out === "string" ? out.length + " chars" : JSON.stringify(out).length + " chars")); } catch (e) { ran.push(name + " threw: " + e.message); } };
    await call("get_table", { name: "hold_request" });
    await call("get_section", { id: "sizing" });
    await call("query_source", { name: "seed", sql_or_args: "select count(*) from ranking_run limit 50" });
    const text = "Harness stub, not Claude. The page sent " + turns.length + " turns (" + bytes + " characters: rules, this table's JSON, your question) with " + tools.length + " page tools.\\n\\nTools run: " + (ran.join("; ") || "none") + ".\\n\\nYou asked: \u201c" + q.slice(0, 120) + "\u201d. A real answer would cite \`columns\` and say the sample rows are synthetic.";
    let shown = "";
    for (const part of text.match(/[\\s\\S]{1,48}/g)) { await wait(25, opts.signal); shown += part; opts.onText && opts.onText({ text: shown, delta: part }); }
    return { text, truncated: false, modelTierApplied: "default" };
  };
  sample.limits = async () => ({ maxPromptBytes: 262144, tools: { maxCount: 16 } });
  const mcp = {
    listTools: async () => ({ servers: [{ server: "host:postgres_seed", authStatus: "connected", tools: [{ name: "query" }] }] }),
    server: async (name) => Object.freeze({ query: async (args) => ({ harness: "stub", server: name, args, rows: [] }) }),
  };
  const ds = ${JSON.stringify(!!dsArg)};
  window.claude = Object.freeze({ use: async (name) => (name === "sample" ? sample : name === "mcp" && ds ? mcp : null) });
})();`;

function findDiagram(n: any): Node | null {
  if (!n || typeof n !== "object") return null;
  if (Array.isArray(n)) { for (const x of n) { const r = findDiagram(x); if (r) return r; } return null; }
  if (n.type === "diagram" && n.key === key) return n;
  for (const v of Object.values(n)) { const r = findDiagram(v); if (r) return r; }
  return null;
}

/** The same figure markup Diagram.tsx renders (classes from tdd.css), static, so the harness owns no app-core file:
 *  a box whose table has a schema card is that table's card (TableNode, D26), any other box a plain hotspot. */
function Fig({ n, base }: { n: Node; base: string }) {
  const natW = Number(n.width) || 1600, natH = Number(n.height) || natW / 2;
  const card = (h: Hotspot) => (h.table ? content.schema.find((t) => t.id === h.table || t.table === h.table) : undefined);
  return (
    <figure className={cx("diagram", n.fit && "fit")} id={n.id}>
      <div className="bar"><span className="fig-title" dangerouslySetInnerHTML={{ __html: n.title }} /></div>
      <div className="view">
        <div className="stage">
          <img src={base + n.src} alt={n.alt} width={n.width || undefined} height={n.height || undefined} />
          {(n.hotspots || []).map((h: Hotspot, i: number) => card(h) ? <TableNode key={i} h={h} t={card(h)!} natW={natW} natH={natH} idBase={n.id} /> : (
            <a key={i} className={cx("hs", h.table && "has-table", h.image && "has-image")} href={h.href || (h.table ? `#${h.table}` : undefined)}
              style={{ left: `${h.x}%`, top: `${h.y}%`, width: `${h.w}%`, height: `${h.h}%` }}
              data-hs="" data-label={h.label} data-note={h.note || undefined} data-table={h.table || undefined}
              aria-label={h.table ? `${h.label}: open table details` : h.label}>{h.label}</a>
          ))}
        </div>
      </div>
    </figure>
  );
}

const diagram = findDiagram(content.sections);
const base = pathToFileURL(buildDir).href + "/";
const page = renderToStaticMarkup(
  <section>
    <h2>Schema drawer harness</h2>
    <p className="small muted">{content.titleText} · {content.schema.length} schema tables · the table cards open the drawer.</p>
    {diagram ? <Fig n={diagram} base={base} /> : <p className="callout warn">No diagram "{key}" in content.json; open tables with window.__harness.emit(id).</p>}
  </section>,
);
const drawer = renderToString(<ContentCtx.Provider value={content}><SchemaDrawer /></ContentCtx.Provider>);

const built = await Bun.build({
  entrypoints: [join(HERE, "client.tsx")], target: "browser", format: "iife", minify: true,
  define: { "process.env.NODE_ENV": '"production"' },
});
if (!built.success) { console.error(built.logs.join("\n")); process.exit(1); }
const js = (await built.outputs[0].text()).replace(/<\/script/gi, "<\\/script");
const css = (await Bun.file(TDD_CSS).text()) + "\n" + (await Bun.file(SCHEMA_CSS).text());
const data = JSON.stringify(content).replace(/</g, "\\u003c");

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Schema drawer harness</title><style>${css}</style></head>
<body><div class="wrap">${page}</div><div id="drawer-root">${drawer}</div>
<script type="application/json" id="tdd-data">${data}</script>
${stubClaude ? `<script>${STUB}</script>\n` : ""}<script>${js}</script></body></html>
`;
await Bun.write(resolve(outArg), html);
console.log(`wrote ${resolve(outArg)}: ${content.schema.length} tables, diagram ${diagram ? key : "none"} with ${(diagram?.hotspots || []).filter((h: Hotspot) => h.table).length} table hotspots, ${stubClaude ? "stub claude runtime, " : ""}${dsArg ? "data sources set, " : ""}${html.length} bytes`);
