// The HTML guard (build_tdd.py:239-284). Author-supplied inline HTML goes through Raw (dangerouslySetInnerHTML) in the
// page, so every tag is rebuilt attribute by attribute: event handlers are dropped wherever they sit, URL attributes
// are allowed by scheme (http, https, mailto, a fragment or a relative path) after control characters, whitespace and
// entities are stripped, and every value is re-quoted with " escaped.
import * as py from "../py";
import type { Match } from "../py/re";

const re = py.re;

const SCRIPT_TAG = re.compile(String.raw`<\s*/?\s*(script|iframe|object|embed|base|meta|link|style|template)\b[^>]*>`, re.I);
const TAG = re.compile(String.raw`<([a-zA-Z][\w:-]*)((?:[^>"']|"[^"]*"|'[^']*')*)>`);
const ATTR = re.compile(String.raw`([^\s=>/"']+)(?:\s*=\s*("[^"]*"|'[^']*'|[^\s>]+))?`);
const URL_ATTRS = new Set(["href", "src", "action", "formaction", "xlink:href", "poster", "data", "srcset", "cite", "longdesc", "background"]);
const CTRL = re.compile("[\\x00-\\x20\\x7f]+");
const OK_SCHEME = re.compile("^(https?|mailto):", re.I);

function safe_url(v: string): string {
  const raw = py.html.unescape(v);
  const flat = CTRL.sub("", raw);
  const head = py.str.partition(py.str.partition(py.str.partition(flat, "/")[0], "#")[0], "?")[0];
  if (head.includes(":")) return OK_SCHEME.match(flat) ? raw : "#blocked:";
  return raw;
}

function rebuild_tag(m: Match): string {
  const name = m.group(1)!;
  const attrs = m.group(2) || "";
  const out = [name];
  const close = py.str.rstrip(attrs).endsWith("/");
  for (const a of ATTR.finditer(attrs)) {
    const key = a.group(1)!;
    let val = a.group(2);
    const k = py.lower(key);
    if (k === "/" || k.startsWith("on") || k === "style" || k === "srcdoc") continue;
    if (val === null) {
      out.push(key);
      continue;
    }
    const q = val.slice(0, 1);
    if ((q === '"' || q === "'") && val.slice(-1) === q) val = val.slice(1, -1);
    if (URL_ATTRS.has(k)) val = safe_url(val);
    const quoted = !val.includes("&") || !val.includes(";") ? py.replace(py.replace(val, "&", "&amp;"), '"', "&quot;") : py.replace(val, '"', "&quot;");
    out.push(`${key}="${quoted}"`);
  }
  return "<" + out.join(" ") + (close ? " /" : "") + ">";
}

/** build_tdd.sanitize_html(h) */
export function sanitize_html(h: string): string {
  if (!h || !h.includes("<")) return h;
  let prev: string | null = null;
  // until stable: stripping an inner tag can reassemble an outer one (<ifr<script>ame ...>)
  while (h !== prev) [prev, h] = [h, SCRIPT_TAG.sub("", h)];
  return TAG.sub(rebuild_tag, h);
}
