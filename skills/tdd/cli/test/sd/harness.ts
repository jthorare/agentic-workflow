// Shared helpers for the D1-surface tests: run an oracle suite into a file (its output can be tens of MB), turn a
// src/sd result into the oracle's tagged form, and run a call the way the oracle's attempt() does.
import { spawnSync } from "bun";
import { createHash } from "node:crypto";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { SdError } from "../../src/sd/errors";
import { SystemExit } from "../../src/sd/page";
import { PyError, qualifiedName } from "../../src/py/errors";
import { PyFloat } from "../../src/py/num";
import { ORACLE, fhex, hasPython, type Tagged } from "../py/oracle";

export { fromTag, hasPython, SKIP_REASON, type Tagged } from "../py/oracle";

export const CLI = new URL("../../", import.meta.url).pathname;
/** The system-design copy UPSTREAM.json pins (the tdd skill's vendor folder). */
export const VENDOR = join(CLI, "..", "vendor", "system-design");
export const UPSTREAM = JSON.parse(readFileSync(join(CLI, "UPSTREAM.json"), "utf8")) as {
  source: string;
  files: Record<string, { sha256: string; lines: number; ranges: { lines: [number, number]; sha256: string; names: string[] }[] }>;
};

/** Run `parity/oracle.py <suite> ...args --out <tmp>` with the vendor copy of system-design; the parsed JSON. */
export function runOracle<T = any>(suite: string, args: string[] = [], env: Record<string, string> = {}): T {
  const dir = mkdtempSync(join(tmpdir(), "tdd-sd-"));
  try {
    const out = join(dir, "golden.json");
    const r = spawnSync(["python3", "-I", "-B", ORACLE, suite, ...args, "--out", out], {
      stdout: "pipe", stderr: "pipe", env: { ...process.env, SYSTEM_DESIGN_SKILL: VENDOR, ...env },
    });
    if (r.exitCode !== 0) throw new Error(`oracle ${suite} failed (${r.exitCode}): ${r.stderr.toString()}`);
    return JSON.parse(readFileSync(out, "utf8")) as T;
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

/** A src/sd value in the oracle's tagged form: Map and plain objects are dicts (in key order), arrays are lists
 * (Python tuples are tagged as lists too), integral numbers and bigints are ints, PyFloat a float. */
export function tagOf(v: unknown): Tagged {
  if (v === null || v === undefined) return { t: "n" };
  if (typeof v === "boolean") return { t: "b", v };
  if (typeof v === "bigint") return { t: "i", v: v.toString() };
  if (typeof v === "number") return Number.isInteger(v) ? { t: "i", v: BigInt(v).toString() } : { t: "f", v: fhex(v) };
  if (v instanceof PyFloat) return { t: "f", v: fhex(v.value) };
  if (typeof v === "string") return { t: "s", v };
  if (Array.isArray(v)) return { t: "l", v: v.map(tagOf) };
  if (v instanceof Map) return { t: "d", v: [...v.entries()].map(([k, x]) => [k as string, tagOf(x)] as [string, Tagged]) };
  if (typeof v === "object") return { t: "d", v: Object.entries(v as object).map(([k, x]) => [k, tagOf(x)] as [string, Tagged]) };
  throw new TypeError(`cannot tag ${typeof v}`);
}

export type Outcome = { r: Tagged } | { x: [string, string | number | null] };

/** The exception's name as the oracle's exc_name() prints it. */
function excName(e: unknown): string {
  if (e instanceof SystemExit) return "SystemExit";
  if (e instanceof SdError) return e.type;
  if (e instanceof PyError) return qualifiedName(e.type);
  throw e; // not a Python exception: a bug in the port, so let the test see it
}

/** attempt(fn): {r} or {x: [type, str(e)]}, as parity/oracle.py's attempt(). */
export function attempt(fn: () => unknown): Outcome {
  try {
    return { r: tagOf(fn()) };
  } catch (e) {
    return { x: [excName(e), (e as Error).message] };
  }
}

/** Assets.ref's [url, src] with a data: URL cut to 64 characters, its length and its sha256 (oracle uri_digest). */
export function uriDigest([url, src]: [string, string | null]): unknown[] {
  return url.startsWith("data:") ? [[url.slice(0, 64), url.length, createHash("sha256").update(url).digest("hex")], src] : [url, src];
}

export const same = (a: unknown, b: unknown): boolean => JSON.stringify(a) === JSON.stringify(b);

/** A short, printable view of a value for a diff report. */
export function show(v: unknown, n = 300): string {
  const s = JSON.stringify(v);
  return s.length > n ? s.slice(0, n) + "…" : s;
}

