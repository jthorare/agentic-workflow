// The revision runner as one Effect program: take the build's lock, read the state, plan which steps this run takes
// (resume, --from, --only, or a new cycle), run each one in order with its journal entries and state update, stop at
// the first failure, and return the report. An interrupt (SIGINT, SIGTERM) records the running step as failed with
// the signal, so the next run resumes there; temporary files are scoped to their step.
import { Effect, Option } from "effect";
import type { RevArgs } from "./args";
import { RevOpts, RevPaths } from "./context";
import { evalStep } from "./eval";
import { fingerprint } from "./hash";
import { Journal, nowIso, type State, type StepRecord } from "./journal";
import { homeRel } from "./paths";
import { Secrets, withdrawSecrets } from "./secrets";
import { Out, Proc, type ProcApi } from "./services";
import { RunMemo, STEP_DEFS, stepDef, type MemoApi, type StepDef, type StepEnv } from "./stepdefs";
import { STEPS, stepIndex, type Outcome, type StepName } from "./steps";

export type Mode = "fresh" | "new-cycle" | "resume" | "from" | "only";

export interface Plan {
  readonly mode: Mode;
  /** The cycle this run records into. */
  readonly cycle: number;
  readonly newCycle: boolean;
  readonly steps: readonly StepName[];
  readonly notes: readonly string[];
}

/** A step is done in the current cycle when its record is from that cycle and did not fail. */
export const isDone = (state: State, s: StepName): boolean => {
  const r = state.steps[s];
  return !!r && r.cycle === state.cycle && r.outcome !== "failed";
};

/**
 * Which steps this run takes. --only and --from work inside the current cycle. Otherwise: a fresh state, or a cycle
 * whose every step is done, starts a new cycle at compile; else the run resumes at the first step not done, or at an
 * earlier ok step that `changed` since it ran: its inputs moved (the source was edited after a failure, say) or its
 * outputs are gone or were edited.
 */
export function plan(state: State, a: Pick<RevArgs, "from" | "only">, changed: (s: StepName, r: StepRecord) => boolean): Plan {
  const fresh = state.cycle === 0;
  const cycle = fresh ? 1 : state.cycle;
  if (a.only) return { mode: "only", cycle, newCycle: fresh, steps: [a.only], notes: [] };
  if (a.from) return { mode: "from", cycle, newCycle: fresh, steps: STEPS.slice(stepIndex(a.from)), notes: [] };
  if (fresh) return { mode: "fresh", cycle: 1, newCycle: true, steps: [...STEPS], notes: [] };
  const frontier = STEPS.findIndex((s) => !isDone(state, s));
  if (frontier === -1) return { mode: "new-cycle", cycle: state.cycle + 1, newCycle: true, steps: [...STEPS], notes: [] };
  // every done step is checked, the reused ones included (a reuse records the fingerprints it matched)
  for (let i = 0; i < frontier; i++) {
    const s = STEPS[i]!, r = state.steps[s]!;
    if ((r.inputs !== undefined || r.outputs !== undefined) && changed(s, r)) {
      return {
        mode: "resume", cycle, newCycle: false, steps: STEPS.slice(i),
        notes: [`what ${s} read or wrote changed since it ran (run ${r.run}): resuming at ${s}, not at ${STEPS[frontier]}`],
      };
    }
  }
  return { mode: "resume", cycle, newCycle: false, steps: STEPS.slice(frontier), notes: [] };
}

export interface Row {
  readonly step: StepName;
  /** ok, skipped, failed (this run or an earlier one in the cycle), "not reached" (after a failure), "not run". */
  readonly outcome: Outcome["outcome"] | "not reached" | "not run";
  /** Whether this invocation ran the step. */
  readonly ran: boolean;
  readonly seconds: number | null;
  readonly detail: string;
  /** The run that recorded the outcome. */
  readonly run: string | null;
  /** A failed step's last lines of stderr (or stdout). */
  readonly stderr_tail?: string;
  /** Numbers to gate on (lint_errors, lint_warnings). */
  readonly data?: Readonly<Record<string, number>>;
}

export interface Report {
  readonly packet: string;
  readonly slug: string;
  readonly run: string;
  readonly cycle: number;
  readonly mode: Mode;
  readonly steps: readonly StepName[];
  readonly notes: readonly string[];
  readonly rows: readonly Row[];
  readonly exit: number;
}

/** Why the run cannot start (the lock is held). */
export class Refused extends Error {}

/** Set by the CLI's signal handlers before it interrupts the program. */
export interface Interrupt {
  by: NodeJS.Signals | null;
}
export const signalExit = (s: NodeJS.Signals | null): number => (s === "SIGINT" ? 130 : 143);

const pad = (n: number) => String(n).padStart(2, "0");
export function runId(d = new Date(), pid = process.pid): string {
  return `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}T${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}Z-${pid}`;
}

const detailOf = (r: { outcome: string; detail?: string; reason?: string; error?: string }): string =>
  r.outcome === "ok" ? r.detail ?? "" : r.outcome === "skipped" ? r.reason ?? "" : r.error ?? "";

const secondsSince = (t0: number) => Math.round(performance.now() - t0) / 1000;

/** Everything the program needs; RunMemo is made per run. */
export type ProgramEnv = Exclude<StepEnv, RunMemo> | Journal | Out;

export const revProgram = (interrupt: Interrupt): Effect.Effect<Report, Refused, ProgramEnv> =>
  Effect.scoped(
    Effect.gen(function* () {
      const p = yield* RevPaths, a = yield* RevOpts, j = yield* Journal, out = yield* Out, secrets = yield* Secrets;
      const run = runId();
      const held = yield* j.lock(run);
      if (held) return yield* Effect.fail(new Refused(held));
      yield* Effect.addFinalizer(() => j.unlock);
      // no secret in process.env while the run lasts: the in-process tools' children (lint's git) inherit it
      yield* Effect.acquireRelease(Effect.sync(withdrawSecrets), (restore) => Effect.sync(restore));
      // SKILL.md runs step 5 from the packet folder; the in-process compile and agents take paths relative to it.
      const cwd = yield* Effect.acquireRelease(
        Effect.sync(() => { const old = process.cwd(); process.chdir(p.packet); return old; }),
        (old) => Effect.sync(() => process.chdir(old)),
      );
      void cwd;
      const loaded = yield* j.load(run);
      const S: { state: State } = { state: loaded.state };
      // A done step must be redone when what it read changed, or what it wrote is gone or was changed since.
      const changed = (s: StepName, r: StepRecord) => {
        const def = stepDef(s);
        if (r.inputs !== undefined && def.inputs && fingerprint(def.inputs(p, a, secrets)) !== r.inputs) return true;
        const outs = def.outputs(p);
        return r.outputs !== undefined && outs.length > 0 && fingerprint(outs) !== r.outputs;
      };
      const pl = plan(S.state, a, changed);
      if (pl.newCycle) S.state = { ...S.state, packet: homeRel(p.packet), cycle: pl.cycle, cycle_started: nowIso() };
      const notes = [...(loaded.note ? [loaded.note] : []), ...pl.notes].map(secrets.scrub);
      const online = a.push && Option.isSome(secrets.braintrust);
      yield* j.append({
        event: "run-start", run, cycle: pl.cycle, mode: pl.mode, steps: pl.steps, packet: homeRel(p.packet),
        flags: { no_shots: a.noShots, no_gh: a.noGh, push: a.push, reuse: a.reuse, published: a.published, from: a.from, only: a.only },
        keys: secrets.present, eval: evalStep(p, online).name, offline: !online, ...(notes.length ? { notes } : {}),
      });
      out.note(`tdd rev ${p.slug}: cycle ${pl.cycle}, ${pl.mode === "resume" ? `resuming at ${pl.steps[0]}` : pl.mode}, run ${run}`);
      for (const n of notes) out.note(`tdd rev: note: ${n}`);
      const t0 = performance.now();
      const results = new Map<StepName, StepRecord>();
      const memo: MemoApi = { eval: null };
      const body = Effect.gen(function* () {
        for (const [i, s] of pl.steps.entries()) {
          const rec = yield* runOne(stepDef(s), { run, cycle: pl.cycle, index: i, total: pl.steps.length, S, interrupt })
            .pipe(Effect.provideService(RunMemo, memo));
          results.set(s, rec);
          if (rec.outcome === "failed") break;
        }
      });
      yield* body.pipe(
        Effect.onInterrupt(() =>
          j.append({ event: "run-end", run, cycle: pl.cycle, exit: signalExit(interrupt.by), interrupted: interrupt.by ?? "interrupt", seconds: secondsSince(t0) })),
      );
      const exit = [...results.values()].some((r) => r.outcome === "failed") ? 1 : 0;
      yield* j.append({ event: "run-end", run, cycle: pl.cycle, exit, seconds: secondsSince(t0) });
      const rows: Row[] = STEPS.map((s) => {
        const r = results.get(s);
        const extra = (x: StepRecord) => ({
          ...(x.outcome === "failed" && x.stderr_tail ? { stderr_tail: x.stderr_tail } : {}), ...(x.data ? { data: x.data } : {}),
        });
        if (r) return { step: s, outcome: r.outcome, ran: true, seconds: r.seconds, detail: detailOf(r), run, ...extra(r) };
        const old = S.state.steps[s];
        if (pl.steps.includes(s)) return { step: s, outcome: "not reached", ran: false, seconds: null, detail: "after a failed step", run: null };
        if (old && old.cycle === S.state.cycle) {
          return { step: s, outcome: old.outcome, ran: false, seconds: old.seconds, detail: `earlier, run ${old.run}: ${detailOf(old)}`, run: old.run, ...extra(old) };
        }
        return { step: s, outcome: "not run", ran: false, seconds: null, detail: "", run: null };
      });
      return { packet: homeRel(p.packet), slug: p.slug, run, cycle: pl.cycle, mode: pl.mode, steps: pl.steps, notes, rows, exit };
    }),
  );

interface StepCtx {
  readonly run: string;
  readonly cycle: number;
  readonly index: number;
  readonly total: number;
  readonly S: { state: State };
  readonly interrupt: Interrupt;
}

/** Run one step: journal its start, honour --reuse, recover Skip / Fail / a defect into an outcome, fingerprint what it
 *  read and wrote, and save the state and the journal entry, also when it is interrupted. */
const runOne = (def: StepDef, c: StepCtx): Effect.Effect<StepRecord, never, StepEnv | Journal | Out> =>
  Effect.gen(function* () {
    const p = yield* RevPaths, a = yield* RevOpts, j = yield* Journal, out = yield* Out, base = yield* Proc, secrets = yield* Secrets;
    const prev = c.S.state.steps[def.name];
    const t0 = performance.now();
    out.note(`tdd rev: ${def.name} (${c.index + 1}/${c.total})`);
    yield* j.append({ event: "step-start", run: c.run, cycle: c.cycle, step: def.name });
    const inputs = def.inputs ? fingerprint(def.inputs(p, a, secrets)) : undefined;
    const outputsNow = () => {
      const parts = def.outputs(p);
      return parts.length ? fingerprint(parts) : undefined;
    };
    const cmds: string[][] = [];
    const recording: ProcApi = { run: (spec) => { cmds.push(spec.cmd.map(homeRel)); return base.run(spec); } };

    // Paths in what the tools print are shown relative to the packet (or with ~), never as the user's absolute path;
    // a key a tool printed is scrubbed here, so the table, the progress line, the journal and the state agree.
    const rel = (t: string): string => secrets.scrub(homeRel(t.split(p.packet + "/").join("").split(p.packet).join(".")));
    const finish = (raw: Outcome, opts: { reused?: boolean } = {}) =>
      Effect.gen(function* () {
        const o: Outcome = raw.outcome === "ok" ? { outcome: "ok", detail: rel(raw.detail), ...(raw.data ? { data: raw.data } : {}) }
          : raw.outcome === "skipped" ? { outcome: "skipped", reason: rel(raw.reason) }
          : { outcome: "failed", error: rel(raw.error), stderr_tail: rel(raw.stderr_tail) };
        const seconds = secondsSince(t0);
        const at = nowIso();
        const outputs = o.outcome === "ok" || opts.reused ? outputsNow() : undefined;
        const lastOk = o.outcome === "ok"
          ? { run: c.run, at, cycle: c.cycle, ...(inputs !== undefined ? { inputs } : {}), ...(outputs !== undefined ? { outputs } : {}) }
          : prev?.last_ok;
        const rec: StepRecord = {
          cycle: c.cycle, run: c.run, at, seconds, outcome: o.outcome,
          ...(o.outcome === "ok" ? { detail: o.detail } : o.outcome === "skipped" ? { reason: o.reason } : { error: o.error, stderr_tail: o.stderr_tail }),
          ...(o.outcome === "ok" && o.data ? { data: o.data } : {}),
          ...(opts.reused ? { reused: true } : {}),
          ...(inputs !== undefined ? { inputs } : {}),
          ...(outputs !== undefined ? { outputs } : {}),
          ...(lastOk ? { last_ok: lastOk } : {}),
        };
        c.S.state = { ...c.S.state, last_run: c.run, steps: { ...c.S.state.steps, [def.name]: rec } };
        yield* j.save(c.S.state);
        yield* j.append({
          event: "step", run: c.run, cycle: c.cycle, step: def.name, outcome: o.outcome, seconds,
          ...(o.outcome === "ok" ? { detail: o.detail } : o.outcome === "skipped" ? { reason: o.reason } : { error: o.error, stderr_tail: o.stderr_tail }),
          ...(o.outcome === "ok" && o.data ? { data: o.data } : {}),
          ...(opts.reused ? { reused: true } : {}),
          ...(cmds.length ? { cmds } : {}),
          ...(inputs !== undefined ? { inputs } : {}), ...(outputs !== undefined ? { outputs } : {}),
        });
        const shown = o.outcome === "ok" ? o.detail : o.outcome === "skipped" ? o.reason : o.error;
        out.note(`tdd rev: ${def.name} ${o.outcome} in ${seconds.toFixed(2)} s${shown ? `: ${shown}` : ""}`);
        return rec;
      });

    // --reuse: only when the step's latest record did not fail, and its last ok read these inputs and left these outputs
    // (a step that writes nothing is matched on its inputs)
    const lastOk = prev?.last_ok;
    const writes = def.outputs(p).length > 0;
    if (a.reuse && inputs !== undefined && prev && prev.outcome !== "failed" && lastOk && lastOk.inputs === inputs &&
      (!writes || (lastOk.outputs !== undefined && lastOk.outputs === outputsNow()))) {
      return yield* finish(
        { outcome: "skipped", reason: `reused: inputs and outputs unchanged since its ok in run ${lastOk.run} (${lastOk.at})` },
        { reused: true },
      );
    }

    const outcome: Outcome = yield* def.run.pipe(
      Effect.provideService(Proc, recording),
      Effect.map((d): Outcome => ({ outcome: "ok", detail: d.detail, ...(d.data ? { data: d.data } : {}) })),
      Effect.catchTags({
        Skip: (s) => Effect.succeed<Outcome>({ outcome: "skipped", reason: s.reason }),
        Fail: (f) => Effect.succeed<Outcome>({ outcome: "failed", error: f.error, stderr_tail: f.tail }),
      }),
      Effect.catchDefect((d) =>
        Effect.succeed<Outcome>({ outcome: "failed", error: `the step died: ${d instanceof Error ? `${d.name}: ${d.message}` : String(d)}`, stderr_tail: "" })),
      Effect.onInterrupt(() =>
        finish({ outcome: "failed", error: `interrupted by ${c.interrupt.by ?? "an interrupt"}; the next run resumes here`, stderr_tail: "" }).pipe(Effect.asVoid)),
    );
    return yield* finish(outcome);
  });

export { STEP_DEFS };
