// Python's binary - / * on kernel values, for a port that spells an arithmetic expression as calls: int (number or
// bigint, bool counting as int) and float (PyFloat, or a non-integral JS number), with CPython 3.13's results and its
// exact TypeError, ZeroDivisionError and OverflowError texts. str and list repetition (`"ab" * 2`) is supported for
// counts that fit in memory; the texts for counts that do not are out of scope.
import { PyError } from "./errors";
import { PyFloat } from "./num";
import { type PyValue, pyTypeName } from "./value";

type Num = { readonly int: bigint } | { readonly float: number };

function numOf(v: unknown): Num | null {
  if (typeof v === "boolean") return { int: v ? 1n : 0n };
  if (typeof v === "bigint") return { int: v };
  if (typeof v === "number") return Number.isInteger(v) ? { int: BigInt(v) } : { float: v };
  if (v instanceof PyFloat) return { float: v.value };
  return null;
}

/** An int result in the kernel's model: a number when it is safe, else a bigint. */
function intValue(n: bigint): number | bigint {
  return n >= BigInt(Number.MIN_SAFE_INTEGER) && n <= BigInt(Number.MAX_SAFE_INTEGER) ? Number(n) : n;
}

/** float(n) for an int: correctly rounded (Number(bigint) rounds half-even), OverflowError past the float range. */
function toFloat(n: Num): number {
  if ("float" in n) return n.float;
  const f = Number(n.int);
  if (!Number.isFinite(f)) throw new PyError("OverflowError", "int too large to convert to float");
  return f;
}

const unsupported = (op: string, a: unknown, b: unknown): PyError =>
  new PyError("TypeError", `unsupported operand type(s) for ${op}: '${pyTypeName(a)}' and '${pyTypeName(b)}'`);

/** a - b */
export function sub(a: unknown, b: unknown): PyValue {
  const x = numOf(a), y = numOf(b);
  if (x === null || y === null) throw unsupported("-", a, b);
  if ("int" in x && "int" in y) return intValue(x.int - y.int);
  return new PyFloat(toFloat(x) - toFloat(y));
}

const bitLength = (n: bigint): number => (n === 0n ? 0 : n.toString(2).length);

/** x * 2**e in steps that stay normal until the last (exact except where the result is subnormal). */
function ldexp(x: number, e: number): number {
  while (e < -1000) { x *= 2 ** -1000; e += 1000; }
  return x * 2 ** e;
}

/** int / int, correctly rounded as CPython rounds it (true division of two ints). */
function intTrueDiv(a: bigint, b: bigint): number {
  if (b === 0n) throw new PyError("ZeroDivisionError", "division by zero");
  const safe = (n: bigint) => n >= -(2n ** 53n) && n <= 2n ** 53n;
  if (safe(a) && safe(b)) return Number(a) / Number(b);   // both exact doubles: IEEE division rounds correctly
  const neg = (a < 0n) !== (b < 0n);
  const n = a < 0n ? -a : a, d = b < 0n ? -b : b;
  // a quotient of at least 66 bits with a sticky bit below it, so Number() rounds it once, half-even, to 53 bits
  const k = Math.max(0, 66 - (bitLength(n) - bitLength(d)));
  const q = (n << BigInt(k)) / d, r = (n << BigInt(k)) % d;
  const f = ldexp(Number((q << 1n) | (r === 0n ? 0n : 1n)), -(k + 1));
  if (!Number.isFinite(f)) throw new PyError("OverflowError", "integer division result too large for a float");
  return neg ? -f : f;
}

/** a / b (true division: always a float) */
export function truediv(a: unknown, b: unknown): PyFloat {
  const x = numOf(a), y = numOf(b);
  if (x === null || y === null) throw unsupported("/", a, b);
  if ("int" in x && "int" in y) return new PyFloat(intTrueDiv(x.int, y.int));
  const fx = toFloat(x), fy = toFloat(y);
  if (fy === 0) throw new PyError("ZeroDivisionError", "float division by zero");
  return new PyFloat(fx / fy);
}

const isSeq = (v: unknown): v is string | PyValue[] => typeof v === "string" || Array.isArray(v);
const isIndex = (v: unknown): boolean => typeof v === "boolean" || typeof v === "bigint" || (typeof v === "number" && Number.isInteger(v));

function repeat(seq: string | PyValue[], count: unknown): PyValue {
  const c = numOf(count) as { int: bigint };
  const k = c.int <= 0n ? 0 : Number(c.int);
  if (typeof seq === "string") return seq.repeat(k);
  const out: PyValue[] = [];
  for (let i = 0; i < k; i++) out.push(...seq);
  return out;
}

/** a * b */
export function mul(a: unknown, b: unknown): PyValue {
  const x = numOf(a), y = numOf(b);
  if (x !== null && y !== null) {
    if ("int" in x && "int" in y) return intValue(x.int * y.int);
    return new PyFloat(toFloat(x) * toFloat(y));
  }
  if (isSeq(a)) {
    if (isIndex(b)) return repeat(a, b);
    throw new PyError("TypeError", `can't multiply sequence by non-int of type '${pyTypeName(b)}'`);
  }
  if (isSeq(b)) {
    if (isIndex(a)) return repeat(b, a);
    throw new PyError("TypeError", `can't multiply sequence by non-int of type '${pyTypeName(a)}'`);
  }
  throw unsupported("*", a, b);
}
