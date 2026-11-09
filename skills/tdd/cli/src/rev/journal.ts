// The run's memory under build/.rev/: journal.jsonl (append-only, one JSON object per event: run-start, step-start,
// step, note, run-end) and state.json (each step's last outcome in the current cycle, written whole and renamed into
// place after every step, so a stop at any moment leaves the last complete state). Every string is scrubbed of keys
// before it is written. A lock file keeps two runs off the same build. The state is bound to its packet folder (the
// sha256 of its real path): a build/.rev copied from another folder is set aside with a note and the run starts fresh.
import { appendFileSync, existsSync, mkdirSync, readFileSync, realpathSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { Context, Effect, Layer, Schema } from "effect";
import { RevPaths } from "./context";
import { homeRel } from "./paths";
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
  data: Schema.optionalKey(Schema.Record(Schema.String, Schema.Number)),
  reused: Schema.optionalKey(Schema.Boolean),
  last_ok: Schema.optionalKey(OkRef),
});
export type StepRecord = typeof StepRecord.Type;

export const State = Schema.Struct({
  version: Schema.Literal(1),
  packet: Schema.String,
  /** sha256 of the packet folder's real path: the state belongs to that folder only. */
  packet_key: Schema.String,
  cycle: Schema.Number,
  cycle_started: Schema.String,
  last_run: Schema.NullOr(Schema.String),
  steps: Schema.Record(Schema.String, StepRecord),
});
export type State = typeof State.Type;

export const freshState = (packet: string, at: string, key = ""): State =>
  ({ version: 1, packet, packet_key: key, cycle: 0, cycle_started: at, last_run: null, steps: {} });

/** The packet folder's binding key: sha256 of its real path. */
export function packetKey(packet: string): string {
  let real = packet;
  try { real = realpathSync(packet); } catch { /* not there yet: the path itself */ }
  return new Bun.CryptoHasher("sha256").update(real).digest("hex");
}

export interface JournalApi {
  /** The saved state, or a fresh one (with a note saying why when a state.json was there but unreadable). */
  readonly load: (run: string) => Effect.Effect<{ readonly state: State; readonly note: string | null }>;
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
    const key = packetKey(p.packet);
    const fresh = () => freshState(homeRel(p.packet), nowIso(), key);
    const dir = p.rev, statePath = join(dir, "state.json"), journalPath = join(dir, "journal.jsonl"), lockPath = join(dir, "lock");
    const ensure = () => {
      mkdirSync(dir, { recursive: true });
      // the run's memory is local: a packet repo that commits build/ never picks up build/.rev
      const ignore = join(dir, ".gitignore");
      if (!existsSync(ignore)) writeFileSync(ignore, "*\n");
    };
    const scrubbed = (v: unknown) => secrets.scrub(JSON.stringify(v));
    return {
      load: (run) => Effect.gen(function* () {
        if (!existsSync(statePath)) return { state: fresh(), note: null };
        let raw: unknown;
        try { raw = JSON.parse(readFileSync(statePath, "utf8")); } catch (e) {
          return { state: fresh(), note: `build/.rev/state.json is not JSON (${(e as Error).message}); starting a new cycle` };
        }
        const decoded = yield* decodeState(raw).pipe(Effect.orElseSucceed(() => null));
        if (!decoded) return { state: fresh(), note: "build/.rev/state.json does not match the state schema; starting a new cycle" };
        if (decoded.packet_key !== key) {
          // a build/.rev copied from another folder: its outcomes say nothing about this packet's files
          const aside = (f: string) => { if (existsSync(f)) renameSync(f, f.replace(/\.(json|jsonl)$/, `.foreign-${run}.$1`)); };
          aside(statePath);
          aside(journalPath);
          return {
            state: fresh(),
            note: `build/.rev came from another packet folder (${decoded.packet}); set aside as *.foreign-${run}.*, this run starts fresh`,
          };
        }
        const steps = Object.fromEntries(Object.entries(decoded.steps).filter(([k]) => (STEPS as readonly string[]).includes(k)));
        return { state: { ...decoded, steps }, note: null as string | null };
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
