// Test helper: run parity/oracle.py (CPython is the specification) and rebuild its typed values in the kernel's model.
import { spawnSync } from "bun";
import { PyFloat } from "../../src/py/num";
import type { PyValue } from "../../src/py/value";

export const ORACLE = new URL("../../parity/oracle.py", import.meta.url).pathname;
export const hasPython = Bun.which("python3") !== null;
export const SKIP_REASON = "python3 is not on PATH: the CPython golden tests cannot run";

const cache = new Map<string, unknown>();

/** The golden JSON of one oracle suite (cached per process). */
export function oracle<T = any>(suite: string, args: string[] = [], env: Record<string, string> = {}): T {
  const key = JSON.stringify([suite, args, env]);
  if (cache.has(key)) return cache.get(key) as T;
  const r = spawnSync(["python3", "-I", "-B", ORACLE, suite, ...args], {
    stdout: "pipe",
    stderr: "pipe",
    env: { ...process.env, ...env },
  });
  if (r.exitCode !== 0) throw new Error(`oracle ${suite} failed (${r.exitCode}): ${r.stderr.toString()}`);
  const v = JSON.parse(r.stdout.toString()) as T;
  cache.set(key, v);
  return v;
}

const F64 = new Float64Array(1);
const BYTES = new Uint8Array(F64.buffer);

/** A float from the oracle's 16 hex digits (big-endian IEEE bits). */
export function unhex(h: string): number {
  for (let i = 0; i < 8; i++) BYTES[7 - i] = parseInt(h.slice(2 * i, 2 * i + 2), 16);
  return F64[0]!;
}
export function fhex(x: number): string {
  F64[0] = x;
  let s = "";
  for (let i = 7; i >= 0; i--) s += BYTES[i]!.toString(16).padStart(2, "0");
  return s;
}

export type Tagged =
  | { t: "n" } | { t: "b"; v: boolean } | { t: "i"; v: string } | { t: "f"; v: string } | { t: "s"; v: string }
  | { t: "l"; v: Tagged[] } | { t: "d"; v: [string, Tagged][] };

/** The oracle's tagged value as a kernel value (int -> number or bigint, float -> PyFloat, dict -> Map). */
export function fromTag(t: Tagged): PyValue {
  switch (t.t) {
    case "n": return null;
    case "b": return t.v;
    case "i": {
      const b = BigInt(t.v);
      return b >= -9007199254740991n && b <= 9007199254740991n ? Number(b) : b;
    }
    case "f": return new PyFloat(unhex(t.v));
    case "s": return t.v;
    case "l": return t.v.map(fromTag);
    case "d": return new Map(t.v.map(([k, x]) => [k, fromTag(x)] as [string, PyValue]));
  }
}

/** A kernel value back to the tagged form, to compare types, key order and values exactly. */
export function toTag(v: PyValue): Tagged {
  if (v === null) return { t: "n" };
  if (typeof v === "boolean") return { t: "b", v };
  if (typeof v === "bigint") return { t: "i", v: v.toString() };
  if (typeof v === "number") return Number.isInteger(v) ? { t: "i", v: BigInt(v).toString() } : { t: "f", v: fhex(v) };
  if (v instanceof PyFloat) return { t: "f", v: fhex(v.value) };
  if (typeof v === "string") return { t: "s", v };
  if (Array.isArray(v)) return { t: "l", v: v.map(toTag) };
  return { t: "d", v: [...v.entries()].map(([k, x]) => [k, toTag(x)] as [string, Tagged]) };
}

/** A UTF-16 offset as Python's code point index. */
export function cpIndex(s: string, unit: number): number {
  return [...s.slice(0, unit)].length;
}

/** Runs [[a, b], ...] as a membership array over every code point. */
export function runSet(runs: [number, number][]): Uint8Array {
  const set = new Uint8Array(0x110000);
  for (const [a, b] of runs) set.fill(1, a, b + 1);
  return set;
}

/** First few code points where `pred` disagrees with the oracle's runs (empty when they agree everywhere). */
export function sweepDiff(runs: [number, number][], pred: (cp: number) => boolean, limit = 8): string[] {
  const set = runSet(runs);
  const out: string[] = [];
  for (let c = 0; c < 0x110000 && out.length < limit; c++) {
    if ((set[c] === 1) !== pred(c)) out.push(`U+${c.toString(16).toUpperCase().padStart(4, "0")} python=${set[c] === 1}`);
  }
  return out;
}

/** The packets named in $TDD_PARITY_PACKETS (path-separated), for the tests that read real packets. */
export const PACKETS = (process.env.TDD_PARITY_PACKETS ?? "").split(":").filter(Boolean);
