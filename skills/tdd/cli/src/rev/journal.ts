// The run's memory under build/.rev/: journal.jsonl (append-only, one JSON object per event: run-start, step-start,
// step, note, run-end) and state.json (each step's last outcome in the current cycle, written whole and renamed into
// place after every step, so a stop at any moment leaves the last complete state). Every string is scrubbed of keys
// before it is written. A lock file keeps two runs off the same build.
import { appendFileSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { Context, Effect, Layer, Schema } from "effect";
import { RevPaths } from "./context";
import { Secrets } from "./secrets";
import { STEPS } from "./steps";

const OkRef = Schema.Struct({
  run: Schema.String,
  at: Schema.String,
  cycle: Schema.Number,
  inputs: Schema.optionalKey(Schema.String),
  outputs: Schema.optionalKey(Schema.String),
});

export const StepRecord = Schema.Struct({
  cycle: Schema.Number,
  run: Schema.String,
  at: Schema.String,
  seconds: Schema.Number,
  outcome: Schema.Literals(["ok", "skipped", "failed"]),
  detail: Schema.optionalKey(Schema.String),
  reason: Schema.optionalKey(Schema.String),
  error: Schema.optionalKey(Schema.String),
  stderr_tail: Schema.optionalKey(Schema.String),
  inputs: Schema.optionalKey(Schema.String),
  outputs: Schema.optionalKey(Schema.String),
  last_ok: Schema.optionalKey(OkRef),
});
export type StepRecord = typeof StepRecord.Type;

export const State = Schema.Struct({
  version: Schema.Literal(1),
  packet: Schema.String,
  cycle: Schema.Number,
  cycle_started: Schema.String,
  last_run: Schema.NullOr(Schema.String),
  steps: Schema.Record(Schema.String, StepRecord),
});
export type State = typeof State.Type;

export const freshState = (packet: string, at: string): State =>
  ({ version: 1, packet, cycle: 0, cycle_started: at, last_run: null, steps: {} });

export interface JournalApi {
  /** The saved state, or a fresh one (with a note saying why when a state.json was there but unreadable). */
  readonly load: Effect.Effect<{ readonly state: State; readonly note: string | null }>;
  readonly save: (s: State) => Effect.Effect<void>;
  /** Append one event: `at` is added, keys are scrubbed. */
  readonly append: (entry: Record<string, unknown>) => Effect.Effect<void>;
  /** Take the build's lock: null when taken, else why not. Released when the run's scope closes. */
  readonly lock: (run: string) => Effect.Effect<string | null>;
  readonly unlock: Effect.Effect<void>;
}
export class Journal extends Context.Service<Journal, JournalApi>()("tdd/rev/Journal") {}

export const nowIso = (): string => new Date().toISOString();
const decodeState = Schema.decodeUnknownEffect(State);

const alive = (pid: number): boolean => {
  try { process.kill(pid, 0); return true; } catch (e) { return (e as { code?: string }).code === "EPERM"; }
};

export const JournalLive = Layer.effect(
  Journal,
  Effect.gen(function* () {
    const p = yield* RevPaths;
    const secrets = yield* Secrets;
    const dir = p.rev, statePath = join(dir, "state.json"), journalPath = join(dir, "journal.jsonl"), lockPath = join(dir, "lock");
    const ensure = () => {
      mkdirSync(dir, { recursive: true });
      // the run's memory is local: a packet repo that commits build/ never picks up build/.rev
      const ignore = join(dir, ".gitignore");
      if (!existsSync(ignore)) writeFileSync(ignore, "*\n");
    };
    const scrubbed = (v: unknown) => secrets.scrub(JSON.stringify(v));
    return {
      load: Effect.gen(function* () {
        if (!existsSync(statePath)) return { state: freshState(p.packet, nowIso()), note: null };
        let raw: unknown;
        try { raw = JSON.parse(readFileSync(statePath, "utf8")); } catch (e) {
          return { state: freshState(p.packet, nowIso()), note: `build/.rev/state.json is not JSON (${(e as Error).message}); starting a new cycle` };
        }
        return yield* decodeState(raw).pipe(
          Effect.map((s) => ({ state: { ...s, steps: Object.fromEntries(Object.entries(s.steps).filter(([k]) => (STEPS as readonly string[]).includes(k))) }, note: null as string | null })),
          Effect.orElseSucceed(() => ({ state: freshState(p.packet, nowIso()), note: "build/.rev/state.json does not match the state schema; starting a new cycle" })),
        );
      }),
      save: (s) =>
        Effect.sync(() => {
          ensure();
          const tmp = `${statePath}.${process.pid}.tmp`;
          writeFileSync(tmp, secrets.scrub(JSON.stringify(s, null, 1)) + "\n");
          renameSync(tmp, statePath);
        }),
      append: (entry) =>
        Effect.sync(() => {
          ensure();
          appendFileSync(journalPath, scrubbed({ at: nowIso(), ...entry }) + "\n");
        }),
      lock: (run) =>
        Effect.sync(() => {
          ensure();
          if (existsSync(lockPath)) {
            try {
              const held = JSON.parse(readFileSync(lockPath, "utf8")) as { pid?: number; run?: string };
              if (typeof held.pid === "number" && held.pid !== process.pid && alive(held.pid)) {
                return `another tdd rev is running on this build (pid ${held.pid}, run ${held.run ?? "?"})`;
              }
            } catch {
              // an unreadable lock is a stale one
            }
          }
          writeFileSync(lockPath, JSON.stringify({ pid: process.pid, run }) + "\n");
          return null;
        }),
      unlock: Effect.sync(() => rmSync(lockPath, { force: true })),
    };
  }),
);
