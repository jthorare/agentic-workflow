#!/usr/bin/env node
// Check a claude.ai artifact page the way the viewer will show it, then render it at 1440 and 390 px in light and dark.
// Usage: node artifact_check.mjs <page.html> <out-dir> [--playwright-root <dir>] [--widths 1440,390] [--no-shots]
// The page is page content only (what build_doc.py writes). It is wrapped in the artifact skeleton (doctype, charset,
// viewport with viewport-fit=cover, the small reset) in a temp file beside the page, so relative files resolve.
// Static contract: no doctype/html/head/body tags; <title> first, then the viewport-fit-sides meta, then <style>; both
// gutter max() paddings when that meta is present; dark tokens under the guarded prefers-color-scheme block and
// again under :root[data-theme="dark"]; every dark token first defined on bare :root; no literal colors outside the
// token blocks; external scripts and stylesheets only from the artifact allowlist.
// Render, per width x theme (light, dark via prefers-color-scheme, dark via data-theme="dark"): horizontal page
// scroll, broken images, missing video files and posters, console errors, text whose color matches its background,
// side-nav highlighting (wide) or the contents disclosure (narrow); plus an explicit light choice over a dark OS.
// Writes <base>-<width>-<theme>-top.png, -full.png (or -full-N.png segments) and -at50.png per render, and
// <base>-report.json. Exit 0 clean, 1 on any failure, 2 on bad usage or no Playwright.
// Playwright lookup is verify_page.mjs's: $PLAYWRIGHT_CORE, --playwright-root, $SYSTEM_DESIGN_DEPS
// (default ~/.local/share/system-design-skills), then the working directory. Open the screenshots: metrics are not a review.
import { createRequire } from "node:module";
import path from "node:path";
import fs from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";

const args = process.argv.slice(2);
const opt = (name) => { const i = args.indexOf(name); if (i < 0) return undefined; const v = args[i + 1]; args.splice(i, 2); return v; };
const flag = (name) => { const i = args.indexOf(name); if (i < 0) return false; args.splice(i, 1); return true; };
const pwRoot = opt("--playwright-root");
const widths = (opt("--widths") ?? "1440,390").split(",").map(Number).filter(Boolean);
const noShots = flag("--no-shots");
const [pageArg, outArg] = args;
if (!pageArg || !outArg) { console.error("usage: artifact_check.mjs <page.html> <out-dir> [--playwright-root dir] [--widths 1440,390] [--no-shots]"); process.exit(2); }
const pagePath = path.resolve(pageArg), outDir = path.resolve(outArg);
if (!fs.existsSync(pagePath)) { console.error(`no such page ${pagePath}`); process.exit(2); }

const deps = process.env.SYSTEM_DESIGN_DEPS ?? path.join(process.env.HOME ?? "", ".local/share/system-design-skills");
const roots = pwRoot ? [pwRoot] : [deps, process.cwd()];
let chromium, lastError;
const candidates = [
  ...(process.env.PLAYWRIGHT_CORE ? [[process.cwd(), path.resolve(process.env.PLAYWRIGHT_CORE)]] : []),
  ...roots.flatMap((root) => [[root, "playwright"], [root, "playwright-core"]]),
];
for (const [root, pkg] of candidates) {
  try { ({ chromium } = createRequire(path.join(path.resolve(root), "x.js"))(pkg)); break; }
  catch (error) { if (error.code !== "MODULE_NOT_FOUND") lastError = error; }
}
if (!chromium) {
  console.error(`no usable playwright or playwright-core (PLAYWRIGHT_CORE, ${roots.join(", ")}); run scripts/doctor.sh`);
  if (lastError) console.error(String(lastError));
  process.exit(2);
}

const failures = [], warnings = [];
const fail = (m) => failures.push(m), warn = (m) => warnings.push(m);
const src = fs.readFileSync(pagePath, "utf8");
const base = path.basename(pagePath, path.extname(pagePath));

// ------------------------------------------------------------------ static contract
const stripped = src.replace(/<!--[\s\S]*?-->/g, "");
for (const [re, what] of [[/<!doctype/i, "<!doctype"], [/<html[\s>]/i, "<html>"], [/<head[\s>]/i, "<head>"], [/<body[\s>]/i, "<body>"]]) {
  if (re.test(stripped)) fail(`static: page contains ${what}; the artifact skeleton supplies it`);
}
const firstTag = stripped.trimStart().match(/^<(!?[a-z]+)/i);
if (!firstTag || firstTag[1].toLowerCase() !== "title") fail(`static: page must begin with <title> (begins with <${firstTag ? firstTag[1] : "?"}>)`);
const title = (src.match(/<title>([\s\S]*?)<\/title>/i) || [])[1]?.trim() ?? "";
const words = title.split(/\s+/).filter(Boolean).length;
if (!title) fail("static: empty <title>");
else if (words < 2 || words > 4) warn(`static: title "${title}" has ${words} words (a name of 2 to 4 words reads best)`);
if (src.indexOf("<title") > 8192) fail("static: <title> is not in the first 8 KB");
const fitIdx = src.search(/<meta\s+name="claude:viewport-fit-sides"\s+content="cover"\s*\/?>/i);
const styleIdx = src.search(/<style[\s>]/i);
if (fitIdx < 0) warn("static: no viewport-fit-sides meta");
else if (styleIdx >= 0 && fitIdx > styleIdx) fail("static: viewport-fit-sides meta must come before <style>");
const styles = [...src.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi)].map((m) => ({ css: m[1], at: m.index }));
const detailA = src.indexOf("<!-- PR-DETAIL:START -->"), detailB = src.indexOf("<!-- PR-DETAIL:END -->");
const inDetail = (at) => detailA >= 0 && at > detailA && at < detailB;
if (!styles.length) fail("static: no <style>");
const css = styles.filter((s) => !inDetail(s.at)).map((s) => s.css).join("\n");
if (fitIdx >= 0 && !(/padding-left:\s*max\(\s*16px\s*,\s*env\(safe-area-inset-left/.test(css) && /padding-right:\s*max\(\s*16px\s*,\s*env\(safe-area-inset-right/.test(css))) {
  fail("static: viewport-fit-sides meta without both max(16px, env(safe-area-inset-*)) gutter paddings");
}
const lastScript = src.lastIndexOf("</script>");
if (lastScript >= 0 && src.slice(lastScript + 9).replace(/<!--[\s\S]*?-->/g, "").trim()) warn("static: markup after the last </script>");

// CSS walk: [{prelude, body, media}] for every rule with declarations
function rules(text) {
  text = text.replace(/\/\*[\s\S]*?\*\//g, "").replace(/@import\s+(url\([^)]*\)|"[^"]*"|'[^']*')[^;]*;/g, "");
  const out = [];
  (function walk(t, media) {
    let i = 0;
    while (i < t.length) {
      const open = t.indexOf("{", i);
      if (open < 0) break;
      const prelude = t.slice(i, open).replace(/^[\s;]+/, "").trim();
      let depth = 1, j = open + 1;
      while (j < t.length && depth) { if (t[j] === "{") depth++; else if (t[j] === "}") depth--; j++; }
      const body = t.slice(open + 1, j - 1);
      if (prelude.startsWith("@media") || prelude.startsWith("@supports") || prelude.startsWith("@layer") || prelude.startsWith("@container")) walk(body, prelude);
      else out.push({ prelude, body, media });
      i = j;
    }
  })(text, "");
  return out;
}
const COLOR = /#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(|\b(?:white|black|red|green|blue|gray|grey|silver|navy|orange|yellow|purple|pink)\b(?![-\w])/i;
const isRoot = (p) => p === ":root";
const isDarkMedia = (r) => /prefers-color-scheme:\s*dark/.test(r.media) && /^:root:not\(\[data-theme="?light"?\]\)$/.test(r.prelude);
const isDarkAttr = (r) => !r.media && /^:root\[data-theme="?dark"?\]$/.test(r.prelude);
const all = rules(css);
const rootDefs = new Set();
let darkMedia = 0, darkAttr = 0;
for (const r of all) {
  const props = [...r.body.matchAll(/(--[\w-]+)\s*:/g)].map((m) => m[1]);
  if (isRoot(r.prelude) && !r.media) { props.forEach((p) => rootDefs.add(p)); continue; }
  if (isDarkMedia(r) || isDarkAttr(r)) {
    if (isDarkMedia(r)) darkMedia++; else darkAttr++;
    for (const p of props) if (!rootDefs.has(p)) fail(`static: ${p} is defined in a dark block before bare :root`);
    if (!/color-scheme:\s*dark/.test(r.body)) fail(`static: dark block ${r.prelude} lacks color-scheme: dark`);
    continue;
  }
  for (const decl of r.body.split(";")) {
    const m = decl.match(/^\s*([\w-]+)\s*:\s*([\s\S]+)$/);
    if (!m) continue;
    if (COLOR.test(m[2].replace(/url\([^)]*\)/g, ""))) fail(`static: literal color in "${r.prelude.slice(0, 60)}" -> ${m[1]}: ${m[2].trim().slice(0, 60)}`);
  }
}
if (!darkMedia) fail('static: no @media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { ... } } block');
if (!darkAttr) fail('static: no :root[data-theme="dark"] { ... } block');
if (!/body\s*{[^}]*background(-color)?:\s*var\(--/.test(css.replace(/\/\*[\s\S]*?\*\//g, ""))) fail("static: body has no explicit token background");
for (const s of styles.filter((x) => inDetail(x.at))) {
  for (const r of rules(s.css)) if (COLOR.test(r.body)) warn(`static: build_prs.py's inlined pr-cards.css has a literal color in "${r.prelude.slice(0, 40)}" (doc-system.css overrides it for .doc pages)`);
}
const used = new Set([...css.matchAll(/var\(\s*(--[\w-]+)\s*\)/g)].map((m) => m[1]));
const definedAnywhere = new Set([...css.matchAll(/(--[\w-]+)\s*:/g)].map((m) => m[1]));
for (const v of used) if (!definedAnywhere.has(v)) fail(`static: var(${v}) is used but never defined`);
const ALLOW_SCRIPT = /^https:\/\/(cdnjs\.cloudflare\.com|cdn\.jsdelivr\.net\/npm\/|unpkg\.com|cdn\.tailwindcss\.com|code\.jquery\.com)/;
for (const m of src.matchAll(/<script[^>]*\ssrc="([^"]+)"/gi)) if (/^https?:/.test(m[1]) && !ALLOW_SCRIPT.test(m[1])) fail(`static: script host not allowed: ${m[1]}`);
for (const m of [...src.matchAll(/<link[^>]*\shref="([^"]+)"/gi), ...css.matchAll(/@import\s+url\(\s*"?([^")]+)"?\s*\)/g)]) {
  if (/^https?:/.test(m[1]) && !/^https:\/\/fonts\.googleapis\.com\//.test(m[1])) fail(`static: stylesheet host not allowed: ${m[1]}`);
}
const scripts = [...src.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/gi)].map((m) => m[1]).join("\n");
if (/\b(alert|confirm|prompt)\s*\(/.test(scripts)) warn("static: script calls alert/confirm/prompt; the viewer ignores them");
if (/<iframe|<object|<embed/i.test(stripped)) fail("static: iframe/object/embed cannot load inside an artifact");
if (Buffer.byteLength(src) > 16 * 1024 * 1024) fail("static: page is over 16 MB");

// ------------------------------------------------------------------ render
const SKELETON = (body) => `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<style>:root{color-scheme:light;padding-top:env(safe-area-inset-top);padding-bottom:env(safe-area-inset-bottom)}body{margin:0;font:14px system-ui,-apple-system,"Segoe UI",sans-serif}img{max-width:100%}[hidden]{display:none!important}</style>
</head><body>
${body}
</body></html>`;
const wrapped = path.join(path.dirname(pagePath), `.artifact-check-${base}-${process.pid}.html`);
fs.writeFileSync(wrapped, SKELETON(src));
fs.mkdirSync(outDir, { recursive: true });

let browser;
try { browser = await chromium.launch(); }
catch (error) { fs.rmSync(wrapped, { force: true }); console.error(`Chromium did not launch (run scripts/doctor.sh): ${String(error).split("\n")[0]}`); process.exit(2); }

const FONT_HOSTS = /fonts\.(googleapis|gstatic)\.com/;
const report = { page: pagePath, title, renders: [], failures, warnings };

async function open(width, scheme, attr) {
  const ctx = await browser.newContext({ viewport: { width, height: 900 }, colorScheme: scheme, deviceScaleFactor: 1, reducedMotion: "reduce" });
  const p = await ctx.newPage();
  const errors = [], netFail = [];
  p.on("console", (m) => { if (m.type() === "error") errors.push({ text: m.text(), url: m.location()?.url ?? "" }); });
  p.on("pageerror", (e) => errors.push({ text: `pageerror: ${e.message}`, url: "" }));
  p.on("requestfailed", (r) => netFail.push({ url: r.url(), err: r.failure()?.errorText ?? "" }));
  await p.goto(pathToFileURL(wrapped).href, { waitUntil: "load" });
  if (attr) await p.evaluate((a) => document.documentElement.setAttribute("data-theme", a), attr);
  await p.evaluate(async () => {
    const imgs = [...document.images];
    imgs.forEach((i) => { i.loading = "eager"; });
    await Promise.all(imgs.map((i) => i.complete ? null : new Promise((r) => { i.addEventListener("load", r, { once: true }); i.addEventListener("error", r, { once: true }); setTimeout(r, 15000); })));
    if (document.fonts) await Promise.race([document.fonts.ready, new Promise((r) => setTimeout(r, 8000))]);
  });
  await p.waitForTimeout(150);
  return { ctx, p, errors, netFail };
}

const themes = [["light", "light", null], ["dark", "dark", null], ["dark-attr", "light", "dark"]];
let lightBg = null;
for (const width of widths) {
  for (const [label, scheme, attr] of themes) {
    const tag = `${width}-${label}`;
    const { ctx, p, errors, netFail } = await open(width, scheme, attr);
    const r = await p.evaluate(() => {
      const parse = (c) => {
        let m = c.match(/rgba?\(([\d.]+)[, ]+([\d.]+)[, ]+([\d.]+)(?:[, /]+([\d.]+%?))?\)/);
        if (m) return [+m[1], +m[2], +m[3], m[4] === undefined ? 1 : (m[4].endsWith("%") ? parseFloat(m[4]) / 100 : +m[4])];
        m = c.match(/color\(srgb\s+([\d.e-]+)\s+([\d.e-]+)\s+([\d.e-]+)(?:\s*\/\s*([\d.]+%?))?\)/);
        if (m) return [m[1] * 255, m[2] * 255, m[3] * 255, m[4] === undefined ? 1 : (m[4].endsWith("%") ? parseFloat(m[4]) / 100 : +m[4])];
        return null;
      };
      const over = (top, bot) => { const a = top[3] + bot[3] * (1 - top[3]); if (!a) return [0, 0, 0, 0]; return [0, 1, 2].map((i) => (top[i] * top[3] + bot[i] * bot[3] * (1 - top[3])) / a).concat(a); };
      const lum = (c) => { const f = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]); };
      const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
      const bgOf = (el) => {
        const stack = [];
        for (let e = el; e; e = e.parentElement) { const c = parse(getComputedStyle(e).backgroundColor); if (c && c[3] > 0) { stack.push(c); if (c[3] >= 1) break; } }
        let acc = [255, 255, 255, 1];
        for (let i = stack.length - 1; i >= 0; i--) acc = over(stack[i], acc);
        return acc;
      };
      const invisible = [], low = [];
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      const seen = new Set();
      for (let n = walker.nextNode(); n; n = walker.nextNode()) {
        const el = n.parentElement;
        if (!el || seen.has(el) || !n.textContent.trim()) continue;
        seen.add(el);
        if (el.closest("template, script, style, noscript, .sr-only, dialog:not([open]), video")) continue;
        const rect = el.getBoundingClientRect();
        if (!rect.width || !rect.height) continue;
        const cs = getComputedStyle(el);
        if (cs.visibility !== "visible" || +cs.opacity === 0) continue;
        const fg = parse(cs.color), bg = bgOf(el);
        if (!fg) continue;
        const c = ratio(over(fg, bg), bg);
        const sample = `${el.tagName.toLowerCase()}${el.className && typeof el.className === "string" ? "." + el.className.split(" ")[0] : ""} "${n.textContent.trim().slice(0, 40)}" ${c.toFixed(2)}:1`;
        const large = parseFloat(cs.fontSize) >= 24 || (parseFloat(cs.fontSize) >= 18.6 && +cs.fontWeight >= 700);
        if (c < 1.5) invisible.push(sample);
        else if (c < (large ? 3 : 4.5) && !el.closest(".off")) low.push(sample);
      }
      const imgs = [...document.images];
      const videos = [...document.querySelectorAll("video")].map((v) => ({
        srcs: [...v.querySelectorAll("source")].map((s) => s.src).concat(v.getAttribute("src") ? [v.src] : []),
        poster: v.getAttribute("poster") ? v.poster : null, error: v.error ? v.error.code : 0, ready: v.readyState,
      }));
      const se = document.scrollingElement || document.documentElement;
      return {
        scrollW: se.scrollWidth, clientW: se.clientWidth, height: se.scrollHeight,
        bodyBg: getComputedStyle(document.body).backgroundColor,
        images: imgs.length, broken: imgs.filter((i) => !i.complete || i.naturalWidth === 0).map((i) => i.getAttribute("src").slice(0, 80)),
        videos, invisible, low,
        sidenavShown: !!document.querySelector(".sidenav") && getComputedStyle(document.querySelector(".sidenav")).display !== "none",
        topnavShown: !!document.querySelector(".topnav") && getComputedStyle(document.querySelector(".topnav")).display !== "none",
        wide: [...document.body.querySelectorAll("*")].filter((e) => e.getBoundingClientRect().right > se.clientWidth + 1 && !e.closest(".tbl, pre, .plate, .topnav nav, .sidenav, dialog:not([open])") && !(e.parentElement && e.parentElement.getBoundingClientRect().right > se.clientWidth + 1)).slice(0, 5).map((e) => e.tagName.toLowerCase() + "." + (typeof e.className === "string" ? e.className.split(" ")[0] : "")),
      };
    });
    const res = { width, theme: label, ...r, posterBroken: [], videoMissing: [], codec: [] };
    if (label === "light") lightBg = r.bodyBg;
    if (r.scrollW > r.clientW + 1) fail(`${tag}: horizontal page scroll ${r.scrollW}/${r.clientW} (wide: ${r.wide.join(", ")})`);
    if (r.broken.length) fail(`${tag}: ${r.broken.length} broken image(s): ${r.broken.slice(0, 4).join(", ")}`);
    for (const v of r.videos) {
      for (const s of v.srcs) {
        const f = s.startsWith("file:") ? fileURLToPath(s) : null;
        if (f && !fs.existsSync(f)) { res.videoMissing.push(s); fail(`${tag}: video file missing ${path.relative(path.dirname(pagePath), f)}`); }
        else if (v.error === 4 && f) res.codec.push(path.basename(f));
      }
      if (v.poster) {
        const ok = await p.evaluate((u) => new Promise((r) => { const i = new Image(); i.onload = () => r(i.naturalWidth > 0); i.onerror = () => r(false); i.src = u; }), v.poster);
        if (!ok) { res.posterBroken.push(v.poster); fail(`${tag}: video poster does not load ${v.poster.slice(-60)}`); }
      }
    }
    if (res.codec.length && label === "light") warn(`${width}: this Chromium cannot decode ${res.codec.join(", ")} (no proprietary codecs); the files exist, the viewer's browser plays them`);
    if (r.invisible.length) fail(`${tag}: ${r.invisible.length} text element(s) match their background: ${r.invisible.slice(0, 4).join(" | ")}`);
    if (r.low.length) warn(`${tag}: ${r.low.length} text element(s) under 4.5:1: ${r.low.slice(0, 3).join(" | ")}`);
    const fileErrors = errors.filter((e) => !FONT_HOSTS.test(e.url) && !/fonts\.(googleapis|gstatic)/.test(e.text));
    const fontErrors = errors.length - fileErrors.length;
    if (fileErrors.length) fail(`${tag}: ${fileErrors.length} console error(s): ${fileErrors.slice(0, 3).map((e) => e.text.slice(0, 120) + (e.url ? ` (${e.url.slice(-60)})` : "")).join(" | ")}`);
    if (fontErrors || netFail.some((n) => FONT_HOSTS.test(n.url))) warn(`${tag}: font requests failed (offline?); screenshots use fallback faces`);
    // navigation behaviour
    if (r.sidenavShown) {
      const nav = await p.evaluate(async () => {
        const wait = () => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(r, 120))));
        const ids = [...document.querySelectorAll(".sidenav a[href^='#']")].map((a) => a.getAttribute("href").slice(1)).filter((id) => { const e = document.getElementById(id); return e && e.getClientRects().length; });
        window.scrollTo(0, 0); await wait();
        const atTop = document.querySelector(".sidenav a.active")?.getAttribute("href")?.slice(1) ?? null;
        const mid = ids[Math.floor(ids.length / 2)];
        document.getElementById(mid).scrollIntoView({ block: "start" }); await wait();
        const atMid = document.querySelector(".sidenav a.active")?.getAttribute("href")?.slice(1) ?? null;
        const act = document.querySelector(".sidenav a.active"), side = document.querySelector(".sidenav");
        const r1 = act?.getBoundingClientRect(), r2 = side.getBoundingClientRect();
        const visible = !!act && r1.top >= r2.top - 1 && r1.bottom <= r2.bottom + 1;
        window.scrollTo(0, 0); await wait();
        return { first: ids[0], atTop, mid, atMid, visible, count: ids.length };
      });
      res.nav = nav;
      if (nav.atTop !== nav.first) fail(`${tag}: side nav highlights ${nav.atTop} at the top, expected ${nav.first}`);
      if (nav.atMid !== nav.mid) fail(`${tag}: side nav highlights ${nav.atMid} after scrolling to ${nav.mid}`);
      if (!nav.visible) fail(`${tag}: the active side-nav entry is scrolled out of the nav`);
    } else if (document_has(src, "sidenav") && !r.topnavShown) {
      fail(`${tag}: neither the side nav nor the contents disclosure is shown`);
    }
    // screenshots
    if (!noShots) {
      const shot = (suffix, o) => p.screenshot({ path: path.join(outDir, `${base}-${tag}-${suffix}.png`), ...o });
      await shot("top", {});
      if (r.height <= 16000) await shot("full", { fullPage: true });
      else {
        const seg = 8000;
        for (let y = 0, k = 1; y < r.height; y += seg, k++) await shot(`full-${k}`, { fullPage: true, clip: { x: 0, y, width, height: Math.min(seg, r.height - y) } });
      }
      await p.evaluate(() => window.scrollTo(0, (document.scrollingElement.scrollHeight - innerHeight) / 2));
      await p.waitForTimeout(200);
      await shot("at50", {});
    }
    report.renders.push(res);
    console.log(`${tag}: images ${r.images} broken ${r.broken.length} | videos ${r.videos.length} | hscroll ${r.scrollW > r.clientW + 1} (${r.scrollW}/${r.clientW}) | invisible ${r.invisible.length} low ${r.low.length} | console ${fileErrors.length}${res.nav ? ` | nav top=${res.nav.atTop} mid=${res.nav.atMid === res.nav.mid ? "ok" : res.nav.atMid}` : ` | topnav ${r.topnavShown}`} | height ${r.height}`);
    await ctx.close();
  }
}
function document_has(s, cls) { return s.includes(`class="${cls}"`); }

// explicit light choice over a dark OS
{
  const { ctx, p } = await open(widths[0], "dark", "light");
  const bg = await p.evaluate(() => getComputedStyle(document.body).backgroundColor);
  if (lightBg && bg !== lightBg) fail(`light-attr: data-theme="light" over a dark OS paints ${bg}, light theme paints ${lightBg}`);
  console.log(`light-attr over dark OS: body ${bg} (light ${lightBg})`);
  await ctx.close();
}
await browser.close();
fs.rmSync(wrapped, { force: true });
fs.writeFileSync(path.join(outDir, `${base}-report.json`), JSON.stringify(report, null, 2));
for (const w of warnings) console.log("warning:", w);
for (const f of failures) console.log("FAIL:", f);
console.log(failures.length ? `artifact_check: ${failures.length} failure(s)` : `artifact_check: ok (${report.renders.length} renders, ${warnings.length} warning(s)); screenshots in ${outDir}`);
process.exit(failures.length ? 1 : 0);
