#!/usr/bin/env node
// mermaid -> tldraw offline -> project styling -> lint -> save -> one SVG per diagram.
// One tldraw page per diagram. Re-runnable: each page is cleared and redrawn.
//
// Usage: node draw_diagrams.mjs --spec <dir> --out <dir> [--name <doc-name>] [--only <key>] [--theme dark|light]
//   <dir>/<key>.mmd      mermaid source (flowchart TB|LR, stateDiagram-v2)
//   <dir>/colors.json    optional { "<key>": [["substring of node text", "blue"], ...] }; first match wins, default grey
//   --out                where <name>.tldraw (created via POST /api/docs/create) and <key>.svg land
//   --theme              dark (default) or light. dark exports the SVG with tldraw's darkMode export option, then swaps tldraw's
//                        dark palette for Sindri's by role (SINDRI below); the saved .tldraw doc itself stays theme-neutral.
//   --name               tldraw doc name (default: basename of --out). Always your OWN doc; never another session's.
// Needs the tldraw offline app running. Exit: 0 drawn, 1 app not running or API error, 2 bad arguments or no .mmd files.
// Colors are tldraw's names. Role mapping: blue = primary/new, violet = secondary/flagged, green = outcome, red = alert, grey = existing/context.
import fs from "node:fs";
import path from "node:path";

const arg = (k, d) => { const i = process.argv.indexOf(`--${k}`); return i > -1 ? process.argv[i + 1] : d; };
const SPEC = arg("spec"), OUT = arg("out"), ONLY = arg("only"), THEME = arg("theme", "dark");
if (THEME !== "dark" && THEME !== "light") { console.error("--theme is dark or light"); process.exit(2); }
if (!SPEC || !OUT) { console.error("usage: draw_diagrams.mjs --spec <dir> --out <dir> [--name n] [--only key] [--theme dark|light]"); process.exit(2); }
const DARK = THEME === "dark";
// Sindri's colors per role (Tokens.axaml; skills/system-design/assets/sindri-tokens.json): `solid` is the stroke and the label,
// `semi` the node fill. blue = primary, violet = flagged, green = outcome, red = alert, grey = context. Label colors are the
// lighter Sindri variants (primary-hover for blue) so text keeps its contrast on the dark page.
const SINDRI = {
  blue: { solid: "#828fff", semi: "#1d2036" },
  violet: { solid: "#a78bfa", semi: "#170f2a" },
  green: { solid: "#27a644", semi: "#06180b" },
  red: { solid: "#eb5757", semi: "#220d0e" },
  grey: { solid: "#8a8f98", semi: "#141516" },
};
// tldraw's own dark palette cannot be named in the shape props (only its color names), so the real hex of each role is read
// back from the app: one shape per used color is exported alone with darkMode, and its stroke and fill are the pair to swap.
function remapPalette(svg, found) {
  let out = svg;
  for (const [role, { solid, semi }] of Object.entries(found)) {
    const to = SINDRI[role];
    if (!to) continue;
    if (solid) out = out.replaceAll(new RegExp(solid, "gi"), to.solid);
    if (semi) out = out.replaceAll(new RegExp(semi, "gi"), to.semi);
  }
  return out;
}
const NAME = arg("name", path.basename(path.resolve(OUT)));
// Where the running app writes its port and token. Override with TLDRAW_SERVER_JSON.
const SJ = process.env.TLDRAW_SERVER_JSON ?? (process.platform === "darwin"
  ? `${process.env.HOME}/Library/Application Support/tldraw/server.json`
  : `${process.env.XDG_CONFIG_HOME ?? `${process.env.HOME}/.config`}/tldraw/server.json`);
if (!fs.existsSync(SJ)) { console.error(`tldraw offline is not running (no ${SJ}); start the app or set TLDRAW_SERVER_JSON`); process.exit(1); }

async function call(method, p, body) {
  const { port, token } = JSON.parse(fs.readFileSync(SJ, "utf8")); // re-read: token is per launch
  const res = await fetch(`http://localhost:${port}${p}`, {
    method,
    headers: { authorization: `Bearer ${token}`, "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const json = await res.json();
  if (!res.ok || json.success === false) throw new Error(`${p}: ${JSON.stringify(json).slice(0, 600)}`);
  return json;
}

const diagrams = Object.fromEntries(
  fs.readdirSync(SPEC).filter((f) => f.endsWith(".mmd")).map((f) => [f.slice(0, -4), fs.readFileSync(path.join(SPEC, f), "utf8")]),
);
if (!Object.keys(diagrams).length) { console.error(`no .mmd files in ${SPEC}`); process.exit(2); }
const colorsPath = path.join(SPEC, "colors.json");
const COLOR_RULES = fs.existsSync(colorsPath) ? JSON.parse(fs.readFileSync(colorsPath, "utf8")) : {};
fs.mkdirSync(OUT, { recursive: true });

const docs = (await call("POST", "/api/search", { code: `return await api.getDocs({ name: ${JSON.stringify(NAME)} })` })).result;
let docId = docs?.[0]?.id;
if (!docId) {
  const created = await call("POST", "/api/docs/create", { name: NAME, directory: path.resolve(OUT) });
  docId = created.id ?? created.result?.id;
}
console.log("doc", docId);

for (const [key, src] of Object.entries(diagrams)) {
  if (ONLY && key !== ONLY) continue;
  const code = `
    let page = editor.getPages().find(p => p.name === ${JSON.stringify(key)});
    if (!page) { editor.createPage({ name: ${JSON.stringify(key)} }); page = editor.getPages().find(p => p.name === ${JSON.stringify(key)}); }
    editor.setCurrentPage(page.id);
    const old = [...editor.getCurrentPageShapeIds()];
    if (old.length) editor.deleteShapes(old);
    await helpers.mermaid(${JSON.stringify(src)});
    const COLORS = ${JSON.stringify(COLOR_RULES[key] ?? [])};
    const plain = (sh) => sh.props.richText ? helpers.richTextToPlainText(sh.props.richText) : (sh.props.name ?? "");
    const updates = [];
    for (const id of editor.getCurrentPageShapeIds()) {
      const sh = editor.getShape(id);
      if (sh.type === "geo") {
        const hit = COLORS.find(([needle]) => plain(sh).includes(needle));
        updates.push({ id, type: "geo", props: { font: "sans", size: "m", dash: "solid", fill: "semi", color: hit ? hit[1] : "grey" } });
      } else if (sh.type === "arrow") {
        updates.push({ id, type: "arrow", props: { font: "sans", size: "m", color: "grey" } });
      } else if (sh.type === "text") {
        updates.push({ id, type: "text", props: { font: "sans" } });
      }
    }
    editor.updateShapes(updates);
    const { lints } = helpers.getLints();
    const ids = [...editor.getCurrentPageShapeIds()];
    const DARK = ${DARK};
    const svg = await editor.getSvgString(ids, { background: false, padding: 24, darkMode: DARK });
    // dark: read back tldraw's dark hex for each role in use (stroke = first stroke, fill = first fill that differs from it)
    const found = {};
    if (DARK) {
      const used = new Set(["grey"]);
      for (const id of ids) { const sh = editor.getShape(id); if (sh.type === "geo") used.add(sh.props.color); }
      for (const role of used) {
        const probe = ids.find((id) => { const sh = editor.getShape(id); return sh.type === "geo" && sh.props.color === role; });
        if (!probe) continue;
        const one = (await editor.getSvgString([probe], { background: false, padding: 0, darkMode: true }))?.svg ?? "";
        const solid = (one.match(/stroke="(#[0-9a-fA-F]{6})"/) ?? [])[1];
        const semi = [...one.matchAll(/fill="(#[0-9a-fA-F]{6})"/g)].map((m) => m[1]).find((h) => h.toLowerCase() !== (solid ?? "").toLowerCase());
        found[role] = { solid, semi };
      }
    }
    await helpers.saveDoc();
    return { shapes: ids.length, lints, svg: svg?.svg, found };
  `;
  const { shapes, lints, svg: raw, found } = (await call("POST", `/api/doc/${docId}/exec`, { code })).result;
  let svg = raw;
  if (DARK) {
    const mapped = Object.values(found ?? {}).filter((p) => p.solid).length;
    if (!mapped) console.error(`${key}: could not read tldraw's dark palette; kept its own dark colors`);
    else svg = remapPalette(raw, found);
  }
  fs.writeFileSync(path.join(OUT, `${key}.svg`), svg);
  console.log(key, "shapes", shapes, "lints", JSON.stringify(lints).slice(0, 300), "svg bytes", svg.length);
}
