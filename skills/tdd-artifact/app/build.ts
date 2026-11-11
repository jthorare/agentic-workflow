#!/usr/bin/env bun
/**
 * Render a TDD packet's content.json (written by ../scripts/build_tdd.py) into the claude.ai artifact page.
 *
 * Usage: bun run build.ts <build-dir> [--css <path>] [--out <file>]
 *   <build-dir>   holds content.json and content.files.json; the page is written there as tdd.html unless --out
 *   --css <path>  the TDD stylesheet (default ../templates/tdd-system/tdd.css); src/schema.css is appended when present
 *   --out <file>  where to write the page (default <build-dir>/tdd.html)
 *
 * The page (CONTRACTS.md): <title>, the claude:viewport-fit-sides meta, one <style> (tdd.css then schema.css, the Google
 * Fonts @import first), <div id="tdd-root"> holding renderToString(<App/>), the content as a JSON island, and the client
 * as one inline production IIFE (React 19 bundled, no external scripts). content.files.json gets "page" and
 * "page_bytes". Exit 0 on success, 1 on any problem (bad input, a React warning during the server render, a failed
 * bundle, a development React build or a client-render fallback in the output).
 */
import { createElement } from "react";
import { renderToString } from "react-dom/server";
import { App } from "./src/App";
import type { Content } from "./src/types";

const APP = import.meta.dir;
const usage = "usage: bun run build.ts <build-dir> [--css <path>] [--out <file>]";

function die(msg: string): never {
  console.error(`build.ts: ${msg}`);
  process.exit(1);
}
const abs = (p: string) => (p.startsWith("/") ? p : `${process.cwd().replace(/\/+$/, "")}/${p}`).replace(/\/\.\//g, "/");
const join = (dir: string, name: string) => `${dir.replace(/\/+$/, "")}/${name}`;
const kb = (n: number) => `${(n / 1024).toFixed(1)} KB`;

function parseArgs(argv: string[]) {
  const out: { dir?: string; css?: string; out?: string } = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--css" || a === "--out") {
      const v = argv[++i];
      if (!v) die(`${a} needs a value\n${usage}`);
      out[a === "--css" ? "css" : "out"] = v;
    } else if (a === "-h" || a === "--help") {
      console.log(usage);
      process.exit(0);
    } else if (a.startsWith("--")) die(`unknown option ${a}\n${usage}`);
    else if (!out.dir) out.dir = a;
    else die(`unexpected argument ${a}\n${usage}`);
  }
  if (!out.dir) die(usage);
  return { dir: abs(out.dir), css: abs(out.css || join(APP, "../templates/tdd-system/tdd.css")), out: out.out ? abs(out.out) : "" };
}

async function readText(path: string, what: string) {
  const f = Bun.file(path);
  if (!(await f.exists())) die(`${what} not found: ${path}`);
  return f.text();
}

function checkContent(c: any, path: string): asserts c is Content {
  const missing = ["title", "tabTitle", "meta", "nav", "sections", "defs"].filter((k) => c?.[k] === undefined);
  if (missing.length) die(`${path} is not a build_tdd.py content.json (missing ${missing.join(", ")})`);
  if (!Array.isArray(c.sections) || !Array.isArray(c.nav)) die(`${path}: sections and nav must be arrays`);
  if (c.template && !String(c.template).startsWith("tdd/")) die(`${path}: template ${c.template} is not tdd/*`);
}

const escHtml = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
/** JSON that is safe inside <script type="application/json">: no "<" at all, and no raw line/paragraph separators. */
const jsonIsland = (v: unknown) => JSON.stringify(v).replace(/</g, "\\u003c").replace(/\u2028/g, "\\u2028").replace(/\u2029/g, "\\u2029");
/** JS that is safe inside a classic inline <script>: "</script" and "<!--" only occur in strings, regexes or templates
 *  in minified output, where "\/" and "\x3C" mean the same characters. */
const inlineJs = (js: string) => js.replace(/<\/script/gi, "<\\/script").replace(/<!--/g, "\\x3C!--");

/** renderToString with React's console warnings (keys, bad props, invalid nesting) turned into a failed build. */
function serverRender(content: Content) {
  const problems: string[] = [];
  const orig = { error: console.error, warn: console.warn };
  console.error = (...a: unknown[]) => { problems.push(a.map(String).join(" ")); };
  console.warn = (...a: unknown[]) => { problems.push(a.map(String).join(" ")); };
  let html = "";
  try {
    html = renderToString(createElement(App, { content }));
  } catch (e) {
    problems.push(`render threw: ${e instanceof Error ? e.stack || e.message : String(e)}`);
  } finally {
    console.error = orig.error;
    console.warn = orig.warn;
  }
  if (problems.length) die(`server render reported ${problems.length} problem(s):\n  ${problems.map((p) => p.slice(0, 600)).join("\n  ")}`);
  return html;
}

async function bundleClient() {
  const out = await Bun.build({
    entrypoints: [join(APP, "src/client.tsx")],
    target: "browser",
    format: "iife",
    minify: true,
    define: { "process.env.NODE_ENV": '"production"' },
    tsconfig: join(APP, "tsconfig.json"),
  });
  if (!out.success) die(`client bundle failed:\n  ${out.logs.map(String).join("\n  ")}`);
  const entry = out.outputs.filter((o) => o.kind === "entry-point");
  if (entry.length !== 1) die(`client bundle produced ${entry.length} entry outputs (expected 1)`);
  const js = await entry[0].text();
  if (/react[\w-]*\.development/.test(js)) die("client bundle contains a React development build");
  if (!js.includes("Minified React error")) die("client bundle has no production React marker ('Minified React error'); NODE_ENV define did not apply");
  return js;
}

const args = parseArgs(process.argv.slice(2));
const contentPath = join(args.dir, "content.json");
let content: unknown;
try { content = JSON.parse(await readText(contentPath, "content.json")); }
catch (e) { die(`${contentPath}: ${e instanceof Error ? e.message : String(e)}`); }
checkContent(content, contentPath);

const tddCss = await readText(args.css, "stylesheet");
const schemaPath = join(APP, "src/schema.css");
const schemaCss = (await Bun.file(schemaPath).exists()) ? await Bun.file(schemaPath).text() : "";
if (/@import\b/.test(schemaCss)) die("src/schema.css has an @import; imports must stay first, in tdd.css");
const css = schemaCss ? `${tddCss.trimEnd()}\n/* ---- src/schema.css ---- */\n${schemaCss.trim()}\n` : tddCss;
const firstRule = css.replace(/\/\*[\s\S]*?\*\//g, "").trimStart();
if (/@import\b/.test(css) && !firstRule.startsWith("@import")) die("the Google Fonts @import is not the first rule in the stylesheet");
if (/<\/style/i.test(css)) die("stylesheet contains </style");

const ssr = serverRender(content);
if (ssr.includes("<!--$!-->")) die("server render contains a client-render fallback (<!--$!-->): a component suspended");
const js = await bundleClient();
const data = jsonIsland(content);

const page =
  `<title>${escHtml(content.tabTitle || content.titleText || "Technical design")}</title>\n` +
  `<meta name="claude:viewport-fit-sides" content="cover">\n` +
  `<style>${css}</style>\n` +
  `<div id="tdd-root">${ssr}</div>\n` +
  `<script type="application/json" id="tdd-data">${data}</script>\n` +
  `<script>${inlineJs(js)}</script>\n`;

if (page.includes("react.development")) die("page contains react.development");
if (page.includes("<!--$!-->")) die("page contains <!--$!-->");
if ((page.match(/<\/script>/gi) || []).length !== 2) die("page has a stray </script> (escaping failed)");
if (/<!doctype|<html[\s>]|<head[\s>]|<body[\s>]/i.test(page.replace(/<script[\s\S]*?<\/script>/gi, ""))) die("page contains a document skeleton tag");
const bytes = new TextEncoder().encode(page).length;
if (bytes > 16 * 1024 * 1024) die(`page is ${bytes} bytes, over the 16 MB artifact limit`);

const outPath = args.out || join(args.dir, "tdd.html");
await Bun.write(outPath, page);

const filesPath = join(args.dir, "content.files.json");
if (await Bun.file(filesPath).exists()) {
  const files = await Bun.file(filesPath).json();
  const prefix = `${args.dir.replace(/\/+$/, "")}/`;
  files.page = outPath.startsWith(prefix) ? outPath.slice(prefix.length) : outPath;
  files.page_bytes = bytes;
  await Bun.write(filesPath, `${JSON.stringify(files, null, 2)}\n`);
} else {
  console.warn(`build.ts: no ${filesPath}; page and page_bytes not recorded`);
}

const gz = Bun.gzipSync(js).length;
const c = content as Content;
console.log(`wrote ${outPath}: ${bytes} bytes (${kb(bytes)})`);
console.log(`  style ${kb(css.length)} | server html ${kb(ssr.length)} | data ${kb(data.length)} | client js ${js.length} bytes raw, ${gz} bytes gzip`);
console.log(`  ${c.sections.length} sections, ${Object.keys(c.defs).length} defs, ${(c.schema || []).length} schema tables`);
