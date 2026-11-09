// repr() as CPython prints it, for f"{x!r}" in warnings and lint messages: the quote choice per string, \t \n \r,
// \xhh for C0, DEL and non-printable Latin-1, \uhhhh and \Uhhhhhhhh above (printable means CPython 3.13's tables).
import { PyFloat, floatRepr, intRepr } from "./num";
import { isPrintableCp } from "./uni";

/** A Python tuple, for repr() only: (a, b) and (a,). */
export class PyTuple {
  readonly items: readonly unknown[];
  constructor(...items: unknown[]) {
    this.items = items;
  }
}

const hex = (n: number, w: number) => n.toString(16).padStart(w, "0");

/** repr(s) of a str. */
export function strRepr(s: string): string {
  const quote = s.includes("'") && !s.includes('"') ? '"' : "'";
  let out = quote;
  for (const ch of s) {
    const c = ch.codePointAt(0)!;
    if (ch === quote || ch === "\\") out += "\\" + ch;
    else if (c === 9) out += "\\t";
    else if (c === 10) out += "\\n";
    else if (c === 13) out += "\\r";
    else if (c < 0x20 || c === 0x7f) out += "\\x" + hex(c, 2);
    else if (c < 0x7f) out += ch;
    else if (isPrintableCp(c)) out += ch;
    else if (c <= 0xff) out += "\\x" + hex(c, 2);
    else if (c <= 0xffff) out += "\\u" + hex(c, 4);
    else out += "\\U" + hex(c, 8);
  }
  return out + quote;
}

/** repr(v) for the kernel's value model (see value.ts); undefined prints as None. */
export function repr(v: unknown): string {
  if (v === null || v === undefined) return "None";
  if (v === true) return "True";
  if (v === false) return "False";
  if (typeof v === "string") return strRepr(v);
  if (typeof v === "bigint") return v.toString();
  if (typeof v === "number") return Number.isInteger(v) ? intRepr(v) : floatRepr(v);
  if (v instanceof PyFloat) return floatRepr(v.value);
  if (v instanceof PyTuple) return v.items.length === 1 ? `(${repr(v.items[0])},)` : `(${v.items.map(repr).join(", ")})`;
  if (Array.isArray(v)) return `[${v.map(repr).join(", ")}]`;
  if (v instanceof Map) return `{${[...v.entries()].map(([k, x]) => `${repr(k)}: ${repr(x)}`).join(", ")}}`;
  if (v instanceof Set) return v.size === 0 ? "set()" : `{${[...v].map(repr).join(", ")}}`;
  if (typeof v === "object") return `{${Object.entries(v).map(([k, x]) => `${repr(k)}: ${repr(x)}`).join(", ")}}`;
  return String(v);
}

/** str(v): a str as itself, everything else as repr() (int, float, bool, None, list and dict all print their repr). */
export function pyStr(v: unknown): string {
  return typeof v === "string" ? v : repr(v);
}
