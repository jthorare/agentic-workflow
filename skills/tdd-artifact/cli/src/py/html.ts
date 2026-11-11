// Python's html.escape and html.unescape. Named references are looked up in the `entities` package's HTML5 table;
// the matching (the regex, the longest-prefix fallback for names without ";") and the numeric rules (CPython's
// Windows-1252 remaps, U+FFFD for surrogates and out-of-range values, removal of control and noncharacter code
// points) follow CPython's html/__init__.py.
import { decodeHTML, decodeHTMLStrict } from "entities";

/** html.escape(s, quote=True) */
export function escape(s: string, quote = true): string {
  let out = s.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
  if (quote) out = out.replaceAll('"', "&quot;").replaceAll("'", "&#x27;");
  return out;
}

const INVALID_CHARREFS: Record<number, string> = {
  0x00: "�", 0x0d: "\r", 0x80: "€", 0x81: "\x81", 0x82: "‚", 0x83: "ƒ", 0x84: "„", 0x85: "…",
  0x86: "†", 0x87: "‡", 0x88: "ˆ", 0x89: "‰", 0x8a: "Š", 0x8b: "‹", 0x8c: "Œ", 0x8d: "\x8d",
  0x8e: "Ž", 0x8f: "\x8f", 0x90: "\x90", 0x91: "‘", 0x92: "’", 0x93: "“", 0x94: "”", 0x95: "•",
  0x96: "–", 0x97: "—", 0x98: "˜", 0x99: "™", 0x9a: "š", 0x9b: "›", 0x9c: "œ", 0x9d: "\x9d",
  0x9e: "ž", 0x9f: "Ÿ",
};

function invalidCodepoint(n: number): boolean {
  return (
    (n >= 0x1 && n <= 0x8) || n === 0xb || (n >= 0xe && n <= 0x1f) || (n >= 0x7f && n <= 0x9f) ||
    (n >= 0xfdd0 && n <= 0xfdef) || (n & 0xfffe) === 0xfffe
  );
}

const NAME_CACHE = new Map<string, string | null>();

/** The value of `name` if it is a key of html.entities.html5 ("amp;" or the legacy "amp"), else null. */
function lookup(name: string): string | null {
  let v = NAME_CACHE.get(name);
  if (v !== undefined) return v;
  v = null;
  if (/^[A-Za-z0-9]+;?$/.test(name)) {
    if (name.endsWith(";")) {
      const r = decodeHTMLStrict("&" + name);
      if (r !== "&" + name) v = r;
    } else {
      // a legacy name (no semicolon) decodes alone to the same value its semicolon twin has
      const full = decodeHTMLStrict("&" + name + ";");
      if (full !== "&" + name + ";" && decodeHTML("&" + name) === full) v = full;
    }
  }
  NAME_CACHE.set(name, v);
  return v;
}

function replaceCharref(s: string): string {
  if (s[0] === "#") {
    const hex = s[1] === "x" || s[1] === "X";
    const digits = (hex ? s.slice(2) : s.slice(1)).replace(/;$/, "");
    const big = BigInt(hex ? "0x" + digits : digits);
    if (big <= 0x9fn && INVALID_CHARREFS[Number(big)] !== undefined) return INVALID_CHARREFS[Number(big)]!;
    if ((big >= 0xd800n && big <= 0xdfffn) || big > 0x10ffffn) return "�";
    const n = Number(big);
    if (invalidCodepoint(n)) return "";
    return String.fromCodePoint(n);
  }
  const direct = lookup(s);
  if (direct !== null) return direct;
  const cps = Array.from(s);
  for (let x = cps.length - 1; x > 1; x--) {
    const v = lookup(cps.slice(0, x).join(""));
    if (v !== null) return v + cps.slice(x).join("");
  }
  return "&" + s;
}

const CHARREF = /&(#[0-9]+;?|#[xX][0-9a-fA-F]+;?|[^\t\n\f <&#;]{1,32};?)/gu;

/** html.unescape(s) */
export function unescape(s: string): string {
  if (!s.includes("&")) return s;
  return s.replace(CHARREF, (_m, g: string) => replaceCharref(g));
}

/** html.entities.html5[name] ("amp;", or a legacy key such as "amp"), or undefined when name is not a key. */
export function html5(name: string): string | undefined {
  return lookup(name) ?? undefined;
}

const ATTR_CHARREF = /&(#[0-9]+|#[xX][0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]*)[;=]?/g;

/** html.parser's _unescape_attrvalue (CPython 3.13.6+): numeric references always; named ones only when exact. */
export function unescapeAttrValue(s: string): string {
  return s.replace(ATTR_CHARREF, (ref) => {
    if (ref.startsWith("&#")) return unescape(ref);
    if (!ref.endsWith("=") && lookup(ref.slice(1)) !== null) return unescape(ref);
    return ref;
  });
}
