// The runner's services (research/bun-port-2026.md section 4.2, "The revision runner"). Each has a live layer; the
// tests swap in recording or scripted ones.
//   Out     where output goes: the steps' own output (stdout, or stderr under --json), progress, the final table
//   Proc    a child process with the parent's output passed through, a timeout, and a kill on interrupt
//   Tools   the in-process programs: tdd build, tdd agents and tdd lint, with their stdout and stderr captured; lint's
//           git runs through a wrapper that unsets every secret (Bun.spawn's default environment is the one the
//           process started with, whatever process.env says later)
//   Db      whether Postgres answers at a url (load-db is skipped when nothing answers, fails when a server refuses)
import { SQL } from "bun";
import { chmodSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { constants, tmpdir } from "node:os";
import { join } from "node:path";
import { Context, Effect, Layer } from "effect";
import { main as buildMain } from "../../bin/build-tdd";
import { lintMain } from "../../bin/lint-packet";
import { main as agentsMain } from "../../bin/write-agents";
import { TsCompilerLive } from "../lint/rules/parity";
import type { Services as LintServices } from "../lint/run";
import { AgentsLive, ClockLive, CompilerLive, FsLive, GIT_TIMEOUT_S, Git, gitLive } from "../lint/services";
import { isSecretName } from "./secrets";
import { Fail } from "./steps";

// ------------------------------------------------------------------------------------------------------------ Out
export interface OutApi {
  /** A tool's stdout (passed through as the tool would print it; to stderr under --json). */
  readonly child: (s: string) => void;
  /** A tool's stderr. */
  readonly childErr: (s: string) => void;
  /** The runner's progress lines (stderr). */
  readonly note: (s: string) => void;
  /** The final table or JSON (stdout). */
  readonly result: (s: string) => void;
}
export class Out extends Context.Service<Out, OutApi>()("tdd/rev/Out") {}

export function outLive(json: boolean): Layer.Layer<Out> {
  // bound once, before any capture swaps process.stdout.write for a tee
  const so = process.stdout.write.bind(process.stdout) as (s: string) => boolean;
  const se = process.stderr.write.bind(process.stderr) as (s: string) => boolean;
  return Layer.succeed(Out, {
    child: (s) => void (json ? se(s) : so(s)),
    childErr: (s) => void se(s),
    note: (s) => void se(s.endsWith("\n") ? s : s + "\n"),
    result: (s) => void so(s.endsWith("\n") ? s : s + "\n"),
  });
}

// ----------------------------------------------------------------------------------------------------------- Proc
export interface ProcSpec {
  readonly cmd: readonly string[];
  readonly cwd: string;
  readonly env: Record<string, string>;
  readonly timeoutMs: number;
}
export interface ProcResult {
  readonly code: number;
  readonly out: string;
  readonly err: string;
  readonly timedOut: boolean;
}
export interface ProcApi {
  readonly run: (spec: ProcSpec) => Effect.Effect<ProcResult, Fail>;
}
export class Proc extends Context.Service<Proc, ProcApi>()("tdd/rev/Proc") {}

const KILL_GRACE_MS = 5000;

/** Run a child: stdout and stderr pass through to Out as they arrive and are kept for the step's record. On timeout
 *  the child gets SIGTERM, then SIGKILL after a grace period; on interrupt the same, and the interrupt waits for it. */
export function procLive(out: OutApi): ProcApi {
  return {
    run: (spec) =>
      Effect.callback<ProcResult, Fail>((resume) => {
        let child: Bun.Subprocess<"ignore", "pipe", "pipe">;
        try {
          child = Bun.spawn({ cmd: [...spec.cmd], cwd: spec.cwd, env: spec.env, stdin: "ignore", stdout: "pipe", stderr: "pipe" });
        } catch (e) {
          resume(Effect.fail(new Fail({ error: `cannot start ${spec.cmd[0]}: ${(e as Error).message}`, tail: "" })));
          return;
        }
        let timedOut = false;
        let grace: ReturnType<typeof setTimeout> | undefined;
        const stop = () => {
          child.kill("SIGTERM");
          grace = setTimeout(() => child.kill("SIGKILL"), KILL_GRACE_MS);
        };
        const timer = setTimeout(() => { timedOut = true; stop(); }, spec.timeoutMs);
        const pump = async (stream: ReadableStream<Uint8Array>, sink: (s: string) => void, acc: string[]) => {
          const dec = new TextDecoder();
          for await (const chunk of stream) {
            const s = dec.decode(chunk, { stream: true });
            if (s) { acc.push(s); sink(s); }
          }
          const rest = dec.decode();
          if (rest) { acc.push(rest); sink(rest); }
        };
        const o: string[] = [], e: string[] = [];
        void Promise.all([pump(child.stdout, out.child, o), pump(child.stderr, out.childErr, e), child.exited]).then(() => {
          clearTimeout(timer);
          if (grace) clearTimeout(grace);
          const sig = child.signalCode as keyof typeof constants.signals | null;
          const code = child.exitCode ?? (sig ? 128 + (constants.signals[sig] ?? 0) : 1);
          resume(Effect.succeed({ code, out: o.join(""), err: e.join(""), timedOut }));
        }, (err: unknown) => {
          clearTimeout(timer);
          resume(Effect.fail(new Fail({ error: `${spec.cmd[0]}: ${(err as Error).message}`, tail: e.join("") })));
        });
        return Effect.promise(async () => {
          clearTimeout(timer);
          stop();
          await child.exited;
          if (grace) clearTimeout(grace);
        });
      }),
  };
}

export const ProcLive = Layer.effect(Proc, Effect.gen(function* () { return procLive(yield* Out); }));

// ---------------------------------------------------------------------------------------------------------- Tools
export interface Captured {
  readonly code: number;
  readonly out: string;
  readonly err: string;
}
export interface ToolsApi {
  /** tdd build (src/compile), argv as build_tdd.py takes it, from the current folder. */
  readonly compile: (argv: readonly string[]) => Effect.Effect<Captured>;
  /** tdd agents (src/agents), argv as write_agents.py takes it. */
  readonly agents: (argv: readonly string[]) => Effect.Effect<Captured>;
  /** tdd lint (src/lint), argv as lint_packet.py takes it. */
  readonly lint: (argv: readonly string[]) => Effect.Effect<Captured>;
}
export class Tools extends Context.Service<Tools, ToolsApi>()("tdd/rev/Tools") {}

const dec = new TextDecoder();
type Write = typeof process.stdout.write;

/** Run an in-process main(argv) with process.stdout/stderr.write teed into strings (the programs print through them). */
async function captured(fn: () => Promise<number>, out: OutApi): Promise<Captured> {
  const so = process.stdout.write, se = process.stderr.write;
  let o = "", e = "";
  const tee = (sink: (s: string) => void, add: (s: string) => void): Write =>
    ((chunk: string | Uint8Array, ...rest: unknown[]) => {
      const s = typeof chunk === "string" ? chunk : dec.decode(chunk);
      add(s);
      sink(s);
      const cb = rest.find((r) => typeof r === "function") as (() => void) | undefined;
      if (cb) cb();
      return true;
    }) as Write;
  process.stdout.write = tee(out.child, (s) => { o += s; });
  process.stderr.write = tee(out.childErr, (s) => { e += s; });
  try {
    const code = await fn();
    return { code, out: o, err: e };
  } catch (x) {
    const msg = x instanceof Error ? `${x.name}: ${x.message}` : String(x);
    out.childErr(msg + "\n");
    return { code: 1, out: o, err: e + msg + "\n" };
  } finally {
    process.stdout.write = so;
    process.stderr.write = se;
  }
}

export function toolsLive(out: OutApi, lintLayer?: Layer.Layer<LintServices>): ToolsApi {
  return {
    compile: (argv) => Effect.promise(() => captured(() => buildMain([...argv]), out)),
    agents: (argv) => Effect.promise(() => captured(() => agentsMain([...argv]), out)),
    lint: (argv) =>
      Effect.promise(async () => {
        const r = await lintMain(argv, lintLayer);
        if (r.out) out.child(r.out);
        if (r.err) out.childErr(r.err);
        return r;
      }),
  };
}

/** A `git` for lint that unsets every secret in the environment and runs the real git: a script in a temporary folder
 *  that lives as long as the run (null when there is no git, or no secret to unset). */
const gitWithoutSecrets = Effect.acquireRelease(
  Effect.sync((): { dir: string; git: string } | null => {
    const real = Bun.which("git");
    const names = Object.keys(process.env).filter(isSecretName);
    if (!real || names.length === 0) return null;
    const dir = mkdtempSync(join(tmpdir(), "tdd-rev-git-"));
    const q = (s: string) => `'${s.split("'").join(`'\\''`)}'`;
    const git = join(dir, "git");
    writeFileSync(git, `#!/bin/sh\nexec /usr/bin/env ${names.map((n) => `-u ${q(n)}`).join(" ")} ${q(real)} "$@"\n`);
    chmodSync(git, 0o755);
    return { dir, git };
  }),
  (made) => Effect.sync(() => { if (made) rmSync(made.dir, { recursive: true, force: true }); }),
);

export const ToolsLive = Layer.effect(Tools, Effect.gen(function* () {
  const out = yield* Out;
  const made = yield* gitWithoutSecrets;
  const lintLayer = made
    ? Layer.mergeAll(FsLive, Layer.succeed(Git, gitLive(GIT_TIMEOUT_S, made.git)), ClockLive, CompilerLive, AgentsLive, TsCompilerLive)
    : undefined;
  return toolsLive(out, lintLayer);
}));

// ------------------------------------------------------------------------------------------------------------- Db
/** A probe's answer: the server took `SELECT 1`; or it did not, either because nothing answered (unreachable: load-db
 *  is skipped) or because a server answered with an error (a missing database, an unknown role, a bad password: a
 *  misconfigured $DATABASE_URL, which fails the step rather than silently dropping the load). */
export type Probe = { readonly ok: true } | { readonly ok: false; readonly answered: boolean; readonly reason: string };
export interface DbApi {
  readonly probe: (url: string, timeoutMs?: number) => Effect.Effect<Probe>;
}
export class Db extends Context.Service<Db, DbApi>()("tdd/rev/Db") {}

export const DbLive = Layer.succeed(Db, {
  probe: (url, timeoutMs = 5000) =>
    Effect.promise(async (): Promise<Probe> => {
      let db: SQL | null = null;
      let timer: ReturnType<typeof setTimeout> | undefined;
      try {
        db = new SQL({ url, max: 1, connectionTimeout: Math.ceil(timeoutMs / 1000) });
        const timeout = new Promise<never>((_, reject) => { timer = setTimeout(() => reject(new Error(`no answer in ${timeoutMs} ms`)), timeoutMs); });
        await Promise.race([db`SELECT 1`, timeout]);
        return { ok: true };
      } catch (e) {
        const x = e as { code?: unknown; errno?: unknown; message?: unknown };
        const reason = (typeof x.message === "string" && x.message ? x.message : String(e)).split("\n")[0]!;
        const answered = x.code === "ERR_POSTGRES_SERVER_ERROR";
        return { ok: false, answered, reason: answered && typeof x.errno === "string" ? `${reason} (SQLSTATE ${x.errno})` : reason };
      } finally {
        if (timer) clearTimeout(timer);
        if (db) await db.close({ timeout: 1 }).catch(() => undefined);
      }
    }),
});
