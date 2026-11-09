// Python numbers: float repr, round() half-even on the exact binary value, int() with Unicode digits.
//
// The value model (shared with json.ts and repr.ts): a Python int is a JS `number` when it is integral and safe,
// else a `bigint`; a Python float is a `PyFloat` (so 1.0 stays 1.0). A bare JS number that is not integral (0.5,
// NaN, Infinity) can only be a float and is printed as one.
import { PyError } from "./errors";
import { strRepr as strReprForInt } from "./repr";
import { decimalValue, isSpaceCp } from "./uni";

/** A Python float. Wrap every float the code produces (`round(x, 2)`, a true division) so 0.0 prints "0.0". */
export class PyFloat {
  readonly value: number;
  constructor(value: number) {
    this.value = value;
  }
  valueOf(): number {
    return this.value;
  }
  toString(): string {
    return floatRepr(this.value);
  }
}

export const pyFloat = (value: number): PyFloat => new PyFloat(value);

/** repr(x) / str(x) of a Python float: the shortest round-trip digits, fixed notation for 1e-4 <= |x| < 1e16. */
export function floatRepr(x: number): string {
  if (Number.isNaN(x)) return "nan";
  if (x === Infinity) return "inf";
  if (x === -Infinity) return "-inf";
  if (x === 0) return Object.is(x, -0) ? "-0.0" : "0.0";
  const sign = x < 0 ? "-" : "";
  // String(x) gives the shortest digits that round-trip (and the closest of them), like CPython's dtoa mode 0.
  const s = String(Math.abs(x));
  const ei = s.indexOf("e");
  const mant = ei < 0 ? s : s.slice(0, ei);
  const exp = ei < 0 ? 0 : Number(s.slice(ei + 1));
  const dot = mant.indexOf(".");
  const intPart = dot < 0 ? mant : mant.slice(0, dot);
  let digits = intPart + (dot < 0 ? "" : mant.slice(dot + 1));
  let decpt = intPart.length + exp; // value = 0.<digits> * 10**decpt
  let lead = 0;
  while (lead < digits.length - 1 && digits.charCodeAt(lead) === 48) lead++;
  digits = digits.slice(lead);
  decpt -= lead;
  let end = digits.length;
  while (end > 1 && digits.charCodeAt(end - 1) === 48) end--;
  digits = digits.slice(0, end);
  const k = digits.length;
  if (decpt > -4 && decpt <= 16) {
    if (decpt <= 0) return `${sign}0.${"0".repeat(-decpt)}${digits}`;
    if (decpt >= k) return `${sign}${digits}${"0".repeat(decpt - k)}.0`;
    return `${sign}${digits.slice(0, decpt)}.${digits.slice(decpt)}`;
  }
  const e = decpt - 1;
  const es = Math.abs(e) < 10 ? `0${Math.abs(e)}` : String(Math.abs(e));
  return `${sign}${digits[0]}${k > 1 ? "." + digits.slice(1) : ""}e${e < 0 ? "-" : "+"}${es}`;
}

/** repr() of a Python int held as a number or a bigint. */
export function intRepr(n: number | bigint): string {
  if (typeof n === "bigint") return n.toString();
  if (!Number.isInteger(n)) throw new PyError("TypeError", `${n} is not an int`);
  return BigInt(n).toString();
}

const F64 = new Float64Array(1);
const U64 = new BigUint64Array(F64.buffer);

/** |x| = mantissa * 2**exponent for a finite, non-zero x. */
function decompose(x: number): [bigint, number] {
  F64[0] = Math.abs(x);
  const bits = U64[0]!;
  const be = Number((bits >> 52n) & 0x7ffn);
  const frac = bits & ((1n << 52n) - 1n);
  return be === 0 ? [frac, -1074] : [frac | (1n << 52n), be - 1075];
}

const NDIGITS_MAX = 323; // (DBL_MANT_DIG - DBL_MIN_EXP) * 0.30103
const NDIGITS_MIN = -308; // -(DBL_MAX_EXP + 1) * 0.30103

/** round(x, ndigits) on a float: the exact binary value rounded half-even to ndigits decimals, read back as a float. */
export function round(x: number, ndigits: number): number {
  if (!Number.isInteger(ndigits)) throw new PyError("TypeError", "'float' object cannot be interpreted as an integer");
  if (!Number.isFinite(x)) return x;
  if (ndigits > NDIGITS_MAX) return x;
  if (ndigits < NDIGITS_MIN) return 0 * x;
  if (x === 0) return x;
  const [m, e] = decompose(x);
  let num = m;
  let den = 1n;
  if (ndigits >= 0) num *= 10n ** BigInt(ndigits);
  else den *= 10n ** BigInt(-ndigits);
  if (e >= 0) num <<= BigInt(e);
  else den <<= BigInt(-e);
  let q = num / den;
  const twice = 2n * (num - q * den);
  if (twice > den || (twice === den && (q & 1n) === 1n)) q += 1n;
  const y = Number(ndigits >= 0 ? `${q}e-${ndigits}` : `${q}e${-ndigits}`);
  if (!Number.isFinite(y)) throw new PyError("OverflowError", "rounded value too large to represent");
  return x < 0 ? -y : y;
}

/** round(x) with no ndigits: the nearest int, ties to even. */
export function roundInt(x: number): number {
  if (Number.isNaN(x)) throw new PyError("ValueError", "cannot convert float NaN to integer");
  if (!Number.isFinite(x)) throw new PyError("OverflowError", "cannot convert float infinity to integer");
  const f = Math.floor(x);
  const diff = x - f; // exact for every double
  if (diff < 0.5) return f + 0; // + 0 turns -0 into 0
  if (diff > 0.5) return f + 1;
  return f % 2 === 0 ? f + 0 : f + 1;
}

const MAX_STR_DIGITS = 4300;

/** A Python int as a number when safe, else a bigint. */
export function fromBigInt(v: bigint): number | bigint {
  return v >= -9007199254740991n && v <= 9007199254740991n ? Number(v) : v;
}

/**
 * int(s, base): CPython's rules, including Unicode decimal digits ("١٢" is 12), Unicode whitespace around the
 * number, one underscore between digits, base 0 prefixes, and the ValueError text with %.200R of the input.
 */
export function int(s: string, base = 10): number | bigint {
  if (!Number.isInteger(base) || (base !== 0 && (base < 2 || base > 36)))
    throw new PyError("ValueError", "int() base must be >= 2 and <= 36, or 0");
  const fail = (): never => {
    let r = strReprForInt(s);
    if ([...r].length > 200) r = [...r].slice(0, 200).join("");
    throw new PyError("ValueError", `invalid literal for int() with base ${base}: ${r}`);
  };
  // _PyUnicode_TransformDecimalAndSpaceToASCII
  let t = "";
  for (const ch of s) {
    const cp = ch.codePointAt(0)!;
    if (cp < 127) t += ch;
    else if (isSpaceCp(cp)) t += " ";
    else {
      const d = decimalValue(cp);
      if (d < 0) {
        t += "?";
        break;
      }
      t += String.fromCharCode(48 + d);
    }
  }
  const isAsciiSpace = (c: string | undefined) => c === " " || c === "\t" || c === "\n" || c === "\v" || c === "\f" || c === "\r";
  let i = 0;
  while (i < t.length && isAsciiSpace(t[i])) i++;
  let neg = false;
  if (t[i] === "+" || t[i] === "-") {
    neg = t[i] === "-";
    i++;
  }
  let b = base;
  let errorIfNonzero = false;
  const c1 = t[i + 1];
  if (b === 0) {
    if (t[i] !== "0") b = 10;
    else if (c1 === "x" || c1 === "X") b = 16;
    else if (c1 === "o" || c1 === "O") b = 8;
    else if (c1 === "b" || c1 === "B") b = 2;
    else {
      errorIfNonzero = true;
      b = 10;
    }
  }
  if (t[i] === "0" && ((b === 16 && (c1 === "x" || c1 === "X")) || (b === 8 && (c1 === "o" || c1 === "O")) || (b === 2 && (c1 === "b" || c1 === "B")))) {
    i += 2;
    if (t[i] === "_") i++;
  }
  if (t[i] === "_") fail();
  const start = i;
  let digits = "";
  let prevUnderscore = false;
  for (; i < t.length; i++) {
    const c = t.charCodeAt(i);
    if (c === 95) {
      if (prevUnderscore) fail();
      prevUnderscore = true;
      continue;
    }
    const v = c >= 48 && c <= 57 ? c - 48 : c >= 97 && c <= 122 ? c - 87 : c >= 65 && c <= 90 ? c - 55 : 99;
    if (v >= b) break;
    prevUnderscore = false;
    digits += t[i];
  }
  if (prevUnderscore || i === start || digits === "") fail();
  if ((b & (b - 1)) !== 0 && digits.length > MAX_STR_DIGITS)
    throw new PyError("ValueError", `Exceeds the limit (${MAX_STR_DIGITS} digits) for integer string conversion: value has ${digits.length} digits; use sys.set_int_max_str_digits() to increase the limit`);
  while (i < t.length && isAsciiSpace(t[i])) i++;
  if (i !== t.length) fail();
  let v = 0n;
  const bb = BigInt(b);
  if (b === 10) v = BigInt(digits);
  else for (const ch of digits) v = v * bb + BigInt(parseInt(ch, 36));
  if (errorIfNonzero && v !== 0n) {
    fail();
  }
  return fromBigInt(neg ? -v : v);
}

/**
 * float(v): a str with CPython's rules (Unicode decimal digits and whitespace, one underscore between digits,
 * inf / infinity / nan in any case, with a sign) and its ValueError text; a number, bigint, PyFloat or bool as itself.
 */
export function float(v: string | number | bigint | boolean | PyFloat | null | undefined): number {
  if (typeof v === "number") return v;
  if (typeof v === "boolean") return v ? 1 : 0;
  if (v instanceof PyFloat) return v.value;
  if (typeof v === "bigint") {
    const x = Number(v);
    if (!Number.isFinite(x)) throw new PyError("OverflowError", "int too large to convert to float");
    return x;
  }
  if (v === null || v === undefined) throw new PyError("TypeError", "float() argument must be a string or a real number, not 'NoneType'");
  const fail = (): never => {
    throw new PyError("ValueError", `could not convert string to float: ${strReprForInt(v)}`);
  };
  let t = "";
  for (const ch of v) {
    const cp = ch.codePointAt(0)!;
    if (cp < 127) t += ch;
    else if (isSpaceCp(cp)) t += " ";
    else {
      const d = decimalValue(cp);
      if (d < 0) {
        t += "?";
        break;
      }
      t += String.fromCharCode(48 + d);
    }
  }
  let a = 0;
  let b = t.length;
  const asciiSpace = (c: string | undefined) => c === " " || c === "\t" || c === "\n" || c === "\v" || c === "\f" || c === "\r";
  while (a < b && asciiSpace(t[a])) a++;
  while (b > a && asciiSpace(t[b - 1])) b--;
  t = t.slice(a, b);
  const special = /^([+-]?)(inf|infinity|nan)$/i.exec(t);
  if (special) return special[2]!.toLowerCase() === "nan" ? NaN : special[1] === "-" ? -Infinity : Infinity;
  // digitpart = digit (["_"] digit)*
  if (!/^[+-]?(?:[0-9](?:_?[0-9])*(?:\.(?:[0-9](?:_?[0-9])*)?)?|\.[0-9](?:_?[0-9])*)(?:[eE][+-]?[0-9](?:_?[0-9])*)?$/.test(t)) fail();
  return Number(t.replaceAll("_", ""));
}

/** format(x, ".nf") / f"{x:.nf}" for n >= 0: the exact binary value rounded half-even, the sign of -0.0 kept. */
export function formatFixed(x: number, n: number): string {
  if (Number.isNaN(x)) return "nan";
  if (!Number.isFinite(x)) return x < 0 ? "-inf" : "inf";
  const neg = x < 0 || Object.is(x, -0);
  let q = 0n;
  if (x !== 0) {
    const [m, e] = decompose(Math.abs(x));
    let num = m * 10n ** BigInt(n);
    let den = 1n;
    if (e >= 0) num <<= BigInt(e);
    else den <<= BigInt(-e);
    q = num / den;
    const twice = 2n * (num - q * den);
    if (twice > den || (twice === den && (q & 1n) === 1n)) q += 1n;
  }
  const s = q.toString().padStart(n + 1, "0");
  return (neg ? "-" : "") + (n ? `${s.slice(0, s.length - n)}.${s.slice(s.length - n)}` : s);
}
