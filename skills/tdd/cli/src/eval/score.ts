// Score records (ev_common.score / not_run / error / metric / output) and the typed outcome every scorer settles to.
//
// A record's status is exactly the Python's: "ok" (a 0..1 value, or None for a metric-only record), "not run" (a
// tool, key or datum is absent: never a zero) or "error". A scorer is an Effect that builds its record; when it
// cannot, it fails with NotRun or Failed, and `settle` turns that, a thrown defect or a per-scorer timeout into the
// typed Outcome (scored | not_run(reason) | failed(error)) and then into the record, so one scorer can never take
// down another (research/bun-port-2026.md section 4.2).
import { Cause, Data, Duration, Effect, Exit, Option } from "effect";
import { PyError } from "../py/errors";
import { floatRepr } from "../py/num";
import * as py from "../py";
import { type Py, PyFloat, fl, isNum, n, now } from "./common";

export type Status = "ok" | "not run" | "error";
export type Metrics = Map<string, Py> | Record<string, Py>;

export interface ScoreRec {
  name: string;
  value: PyFloat | null;
  status: Status;
  method: string;
  source: string;
  reason?: string;
  metrics?: Metrics;
  detail?: Py;
}

const entries = (m: Metrics): [string, Py][] => (m instanceof Map ? [...m.entries()] : Object.entries(m));
const nonEmpty = (m: Metrics | null | undefined): m is Metrics => !!m && (m instanceof Map ? m.size > 0 : Object.keys(m).length > 0);

/** One scorer result. value is 0..1 (or None for a metric-only record); method says how it was computed. */
export function score(name: string, value: Py, method: string, source: string, metrics?: Metrics | null, detail?: Py,
  status: Status = "ok", reason?: string | null): ScoreRec {
  let v: PyFloat | null = null;
  if (value !== null && value !== undefined) {
    const x = n(value);
    if (!(x >= 0 && x <= 1)) throw new PyError("ValueError", `score ${name}=${floatRepr(x)} outside 0..1`);
    v = fl(py.round(x, 4));
  }
  const rec: ScoreRec = { name, value: v, status, method, source };
  if (reason) rec.reason = reason;
  if (nonEmpty(metrics)) rec.metrics = metrics;
  if (detail !== null && detail !== undefined) rec.detail = detail;
  return rec;
}

export const notRun = (name: string, reason: string, method: string, source: string, metrics?: Metrics | null, detail?: Py): ScoreRec =>
  score(name, null, method, source, metrics, detail, "not run", reason);

export const error = (name: string, reason: string, method: string, source: string, metrics?: Metrics | null, detail?: Py): ScoreRec =>
  score(name, null, method, source, metrics, detail, "error", reason);

/** A record with numbers only (no 0..1 score), e.g. reading minutes or byte sizes. */
export const metric = (name: string, metrics: Metrics, method: string, source: string, detail?: Py): ScoreRec =>
  score(name, null, method, source, metrics, detail, "ok");

export interface Output {
  schema: string;
  kind: string;
  packet: string;
  generated: string;
  scores: ScoreRec[];
  metrics: Map<string, Py>;
  counts: { ok: number; metric_only: number; not_run: number; error: number };
  data?: Py;
  [extra: string]: Py;
}

export function output(kind: string, packetKey: string, scores: ScoreRec[], data?: Py, extra?: Record<string, Py>): Output {
  const flat = new Map<string, Py>();
  for (const s of scores)
    for (let [k, v] of s.metrics ? entries(s.metrics) : []) {
      if (typeof v === "boolean") v = v ? 1 : 0;
      if (isNum(v)) flat.set(`${s.name}.${k}`, v);
    }
  const out: Output = {
    schema: "tdd-eval-output/1", kind, packet: packetKey, generated: now(), scores, metrics: flat,
    counts: {
      ok: scores.filter((s) => s.status === "ok" && s.value !== null).length,
      metric_only: scores.filter((s) => s.status === "ok" && s.value === null).length,
      not_run: scores.filter((s) => s.status === "not run").length,
      error: scores.filter((s) => s.status === "error").length,
    },
  };
  if (data !== null && data !== undefined) out.data = data;
  if (extra && Object.keys(extra).length) Object.assign(out, extra);
  return out;
}

// ----------------------------------------------------------------------------- typed outcomes

/** The scorer's tool, key or data is absent: recorded as "not run: <reason>", never as a zero. */
export class NotRun extends Data.TaggedError("NotRun")<{ readonly reason: string; readonly metrics?: Metrics; readonly detail?: Py }> {}
/** The scorer ran and could not produce a measurement: recorded with status "error". */
export class Failed extends Data.TaggedError("Failed")<{ readonly reason: string; readonly metrics?: Metrics; readonly detail?: Py }> {}

export type Outcome<A> =
  | { readonly _tag: "scored"; readonly value: A }
  | { readonly _tag: "not_run"; readonly reason: string; readonly metrics?: Metrics; readonly detail?: Py }
  | { readonly _tag: "failed"; readonly error: string; readonly metrics?: Metrics; readonly detail?: Py };

/** "600" for 600, "0.05" for 0.05: the number as Python's f"{timeout}" prints it. */
export const secs = (s: number): string => (Number.isInteger(s) ? String(s) : floatRepr(s));

function describe(e: unknown): string {
  if (e instanceof PyError) return `${e.type}: ${py.cpSlice(e.message, 0, 160)}`;
  if (e instanceof Error) return `${e.name}: ${py.cpSlice(e.message, 0, 160)}`;
  return py.cpSlice(String(e), 0, 160);
}

/**
 * Run one scorer under a time limit and settle it to a typed Outcome: its value, NotRun / Failed as raised, a defect
 * (a throw anywhere inside) as failed("raised <Type>: <message>"), and the limit as not_run("timed out after Ns").
 * Never fails, so independent scorers can run side by side and keep each other's results.
 */
export function settle<A, R>(eff: Effect.Effect<A, NotRun | Failed, R>, timeoutSeconds: number): Effect.Effect<Outcome<A>, never, R> {
  return Effect.suspend(() => eff).pipe(
    Effect.timeoutOption(Duration.millis(Math.round(timeoutSeconds * 1000))),
    Effect.exit,
    Effect.map((exit): Outcome<A> => {
      if (Exit.isSuccess(exit)) {
        const v = exit.value;
        return Option.isSome(v) ? { _tag: "scored", value: v.value } : { _tag: "not_run", reason: `timed out after ${secs(timeoutSeconds)}s` };
      }
      const typed = Cause.findErrorOption(exit.cause);
      if (Option.isSome(typed)) {
        const e = typed.value;
        if (e._tag === "NotRun") return { _tag: "not_run", reason: e.reason, ...(e.metrics ? { metrics: e.metrics } : {}), ...(e.detail !== undefined ? { detail: e.detail } : {}) };
        return { _tag: "failed", error: e.reason, ...(e.metrics ? { metrics: e.metrics } : {}), ...(e.detail !== undefined ? { detail: e.detail } : {}) };
      }
      return { _tag: "failed", error: `raised ${describe(Cause.squash(exit.cause))}` };
    }),
  );
}

export interface Spec { readonly name: string; readonly method: string; readonly source: string }

/** An Outcome as the record the Python writes for it. */
export function record(spec: Spec, o: Outcome<ScoreRec>): ScoreRec {
  switch (o._tag) {
    case "scored":
      return o.value;
    case "not_run":
      return notRun(spec.name, o.reason, spec.method, spec.source, o.metrics, o.detail);
    case "failed":
      return error(spec.name, o.error, spec.method, spec.source, o.metrics, o.detail);
  }
}

/** The record for an outcome that did not score (a scorer whose value is not a record, e.g. the judge's batch). */
export function unscored(spec: Spec, o: Exclude<Outcome<unknown>, { _tag: "scored" }>): ScoreRec {
  return o._tag === "not_run" ? notRun(spec.name, o.reason, spec.method, spec.source, o.metrics, o.detail)
    : error(spec.name, o.error, spec.method, spec.source, o.metrics, o.detail);
}

/** One scorer: settle(eff, timeout) then record(spec). */
export const scorer = <R>(spec: Spec, eff: Effect.Effect<ScoreRec, NotRun | Failed, R>, timeoutSeconds: number): Effect.Effect<ScoreRec, never, R> =>
  Effect.map(settle(eff, timeoutSeconds), (o) => record(spec, o));

/** A scorer that writes several records (the judge's four criteria): each spec gets the same outcome when it fails. */
export const scorerMany = <R>(specs: readonly Spec[], eff: Effect.Effect<ScoreRec[], NotRun | Failed, R>, timeoutSeconds: number): Effect.Effect<ScoreRec[], never, R> =>
  Effect.map(settle(eff, timeoutSeconds), (o) => (o._tag === "scored" ? o.value : specs.map((s) => record(s, o as Outcome<ScoreRec>))));
