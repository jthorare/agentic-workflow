// The scanner behind replace-ban.test.ts. JS's s.replace("x", y) changes only the first occurrence; Python's
// str.replace changes every one, so a port calls py.replace (src/py/str.ts) or passes a RegExp. A line grep is easy
// to slip past (a pattern on the next line, a const holding a string, s["replace"], .replace.call), so this tokenizes
// the source (comments, strings, templates and regex literals told apart) and accepts a .replace( only when its first
// argument is provably a RegExp: a regex literal, new RegExp(...) / RegExp(...), a name this file binds to one of
// those (and to nothing else), or an expression cast `as RegExp` (which tsc rejects for a string).

import { readdirSync, statSync } from "node:fs";
import { join } from "node:path";

type Kind = "id" | "str" | "tmpl" | "regex" | "num" | "punct";
interface Tok {
  kind: Kind;
  value: string;
  line: number;
}

const REGEX_AFTER_ID = new Set(["return", "typeof", "instanceof", "in", "of", "new", "delete", "void", "throw", "case", "do", "else", "yield", "await"]);
const PUNCT3 = ["===", "!==", "**=", "...", "<<=", ">>=", ">>>", "&&=", "||=", "??="];
const PUNCT2 = ["=>", "==", "!=", "<=", ">=", "&&", "||", "??", "?.", "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<", ">>", "**"];

export function tokenize(src: string): Tok[] {
  const toks: Tok[] = [];
  let i = 0;
  let line = 1;
  const braces: number[] = []; // for each open template expression, the brace depth it started at
  let depth = 0;
  const regexAllowed = () => {
    const t = toks[toks.length - 1];
    if (!t) return true;
    if (t.kind === "punct") return t.value !== ")" && t.value !== "]" && t.value !== "}";
    if (t.kind === "id") return REGEX_AFTER_ID.has(t.value);
    return false;
  };
  const push = (kind: Kind, value: string, at: number) => toks.push({ kind, value, line: at });
  // reads a template body from i (just after ` or }) to its end or to the next ${
  const templateBody = () => {
    const start = line;
    let v = "";
    while (i < src.length) {
      const c = src[i]!;
      if (c === "\\") {
        v += src.slice(i, i + 2);
        i += 2;
        continue;
      }
      if (c === "`") {
        i++;
        push("tmpl", v, start);
        return;
      }
      if (c === "$" && src[i + 1] === "{") {
        i += 2;
        push("tmpl", v, start);
        braces.push(depth);
        depth++;
        return;
      }
      if (c === "\n") line++;
      v += c;
      i++;
    }
  };
  while (i < src.length) {
    const c = src[i]!;
    if (c === "\n") {
      line++;
      i++;
      continue;
    }
    if (/\s/.test(c)) {
      i++;
      continue;
    }
    if (c === "/" && src[i + 1] === "/") {
      while (i < src.length && src[i] !== "\n") i++;
      continue;
    }
    if (c === "/" && src[i + 1] === "*") {
      const end = src.indexOf("*/", i + 2);
      const stop = end < 0 ? src.length : end + 2;
      for (let k = i; k < stop; k++) if (src[k] === "\n") line++;
      i = stop;
      continue;
    }
    if (c === '"' || c === "'") {
      const start = line;
      let v = "";
      i++;
      while (i < src.length && src[i] !== c && src[i] !== "\n") {
        if (src[i] === "\\") {
          v += src.slice(i, i + 2);
          i += 2;
        } else v += src[i++];
      }
      i++;
      push("str", v, start);
      continue;
    }
    if (c === "`") {
      i++;
      templateBody();
      continue;
    }
    if (c === "}" && braces.length && braces[braces.length - 1] === depth - 1) {
      braces.pop();
      depth--;
      i++;
      templateBody();
      continue;
    }
    if (c === "/" && regexAllowed()) {
      const start = line;
      let v = "/";
      i++;
      let cls = false;
      while (i < src.length && src[i] !== "\n") {
        const d = src[i]!;
        if (d === "\\") {
          v += src.slice(i, i + 2);
          i += 2;
          continue;
        }
        v += d;
        i++;
        if (d === "[") cls = true;
        else if (d === "]") cls = false;
        else if (d === "/" && !cls) break;
      }
      while (i < src.length && /[a-z]/.test(src[i]!)) v += src[i++];
      push("regex", v, start);
      continue;
    }
    if (/[A-Za-z_$]/.test(c)) {
      let v = "";
      while (i < src.length && /[\w$]/.test(src[i]!)) v += src[i++];
      push("id", v, line);
      continue;
    }
    if (/[0-9]/.test(c)) {
      let v = "";
      while (i < src.length && /[\w.]/.test(src[i]!)) v += src[i++];
      push("num", v, line);
      continue;
    }
    const p3 = src.slice(i, i + 3);
    const p2 = src.slice(i, i + 2);
    const p = PUNCT3.includes(p3) ? p3 : PUNCT2.includes(p2) && !(p2 === "?." && /[0-9]/.test(src[i + 2] ?? "")) ? p2 : c;
    if (p === "{") depth++;
    if (p === "}") depth--;
    push("punct", p, line);
    i += p.length;
  }
  return toks;
}

export interface Hit {
  line: number;
  why: string;
}

const isP = (t: Tok | undefined, v: string) => t?.kind === "punct" && t.value === v;
const isId = (t: Tok | undefined, v?: string) => t?.kind === "id" && (v === undefined || t.value === v);

/** Names this file binds to a regex literal / new RegExp / RegExp(...), minus any it also binds to something else. */
function regexNames(toks: Tok[]): Set<string> {
  const yes = new Set<string>();
  const no = new Set<string>();
  for (let k = 0; k < toks.length; k++) {
    const t = toks[k]!;
    if (t.kind !== "id" || isP(toks[k - 1], ".")) continue;
    let j = k + 1;
    if (isP(toks[j], ":")) {
      // a type annotation: skip to the = at this nesting level
      let d = 0;
      while (j < toks.length) {
        const u = toks[j]!;
        if (u.kind === "punct" && (u.value === "(" || u.value === "[" || u.value === "{" || u.value === "<")) d++;
        else if (u.kind === "punct" && (u.value === ")" || u.value === "]" || u.value === "}" || u.value === ">")) d--;
        if (d < 0 || (d === 0 && u.kind === "punct" && (u.value === "=" || u.value === ";" || u.value === ","))) break;
        j++;
      }
    }
    if (!isP(toks[j], "=")) continue;
    const v = toks[j + 1];
    const rx = v?.kind === "regex" || (isId(v, "new") && isId(toks[j + 2], "RegExp")) || (isId(v, "RegExp") && isP(toks[j + 2], "("));
    (rx ? yes : no).add(t.value);
  }
  for (const n of no) yes.delete(n);
  return yes;
}

/** Namespace imports of the kernel (import * as py from ".../py" or ".../py/str"), whose .replace is py.replace. */
function kernelNamespaces(toks: Tok[]): Set<string> {
  const out = new Set<string>();
  for (let k = 0; k + 5 < toks.length; k++) {
    if (isId(toks[k], "import") && isP(toks[k + 1], "*") && isId(toks[k + 2], "as") && isId(toks[k + 4], "from")) {
      const from = toks[k + 5]!;
      if (from.kind === "str" && /(^|\/)py(\/str|\/index)?$/.test(from.value)) out.add(toks[k + 3]!.value);
    }
  }
  return out;
}

/** The index after the argument starting at k (the , or ) that ends it at depth 0). */
function argEnd(toks: Tok[], k: number): number {
  let d = 0;
  for (let j = k; j < toks.length; j++) {
    const u = toks[j]!;
    if (u.kind !== "punct") continue;
    if (u.value === "(" || u.value === "[" || u.value === "{") d++;
    else if (u.value === ")" || u.value === "]" || u.value === "}") {
      if (d === 0) return j;
      d--;
    } else if (u.value === "," && d === 0) return j;
  }
  return toks.length;
}

/** Every .replace( (or ?.replace(, ["replace"](, .replace.call / .apply / .bind) whose pattern is not provably a RegExp. */
export function scan(src: string): Hit[] {
  const toks = tokenize(src);
  const rxNames = regexNames(toks);
  const kernel = kernelNamespaces(toks);
  const hits: Hit[] = [];
  for (let k = 0; k < toks.length; k++) {
    const t = toks[k]!;
    let open = -1;
    let receiver: Tok | undefined;
    if (isId(t, "replace") && (isP(toks[k - 1], ".") || isP(toks[k - 1], "?."))) {
      receiver = toks[k - 2];
      if (isP(toks[k + 1], "(")) open = k + 1;
      else if (isP(toks[k + 1], "?.") && isP(toks[k + 2], "(")) open = k + 2;
      else if (isP(toks[k + 1], ".") && ["call", "apply", "bind"].includes(toks[k + 2]?.value ?? "")) {
        if (!(receiver && kernel.has(receiver.value))) hits.push({ line: t.line, why: `.replace.${toks[k + 2]!.value}: call it directly` });
        continue;
      } else continue;
    } else if (t.kind === "str" && t.value === "replace" && isP(toks[k - 1], "[") && isP(toks[k + 1], "]") && isP(toks[k + 2], "(")) {
      receiver = toks[k - 2];
      open = k + 2;
    } else continue;
    if (receiver && receiver.kind === "id" && kernel.has(receiver.value)) continue; // py.replace(s, old, new)
    const a = open + 1;
    const end = argEnd(toks, a);
    const first = toks[a];
    const last2 = toks.slice(Math.max(a, end - 2), end);
    if (last2.length === 2 && isId(last2[0], "as") && isId(last2[1], "RegExp")) continue;
    if (end - a === 1 && first?.kind === "regex") continue;
    if (isId(first, "new") && isId(toks[a + 1], "RegExp") && argEnd(toks, a) === end) continue;
    if (isId(first, "RegExp") && isP(toks[a + 1], "(")) continue;
    // a name (or this.name / obj.name) bound to a regex in this file
    const chain = toks.slice(a, end);
    const isChain = chain.length > 0 && chain.every((u, j) => (j % 2 === 0 ? u.kind === "id" : isP(u, ".")));
    if (isChain && rxNames.has(chain[chain.length - 1]!.value)) continue;
    const why = first?.kind === "str" || first?.kind === "tmpl" ? "a string pattern" : "a pattern not provably a RegExp (use py.replace, a regex, or `as RegExp`)";
    hits.push({ line: t.line, why });
  }
  return hits;
}

/** The files the ban walks: every .ts / .tsx / .js / .mjs / .cjs under these folders of the cli, recursively. */
export const SCANNED = ["src", "bin", "parity"];

export function scannedFiles(root: string): string[] {
  const out: string[] = [];
  const walk = (dir: string) => {
    for (const name of readdirSync(dir)) {
      const p = join(dir, name);
      if (statSync(p).isDirectory()) walk(p);
      else if (/\.(ts|tsx|js|mjs|cjs)$/.test(name)) out.push(p);
    }
  };
  for (const d of SCANNED) walk(join(root, d));
  return out;
}
