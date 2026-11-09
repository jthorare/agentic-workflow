// External commands as a service (ev_common.run / which): git, gh, node artifact_check.mjs, bun check_hydrate.ts,
// python3 lint_packet.py and a repo's test command. The live layer spawns with Bun under a time limit and returns the
// Python's record {code, out, err, seconds, timeout} (code None when the command could not start or timed out); an
// interrupted scorer kills its child. Tests provide a layer that answers from fixtures.
import { Context, Effect, Layer } from "effect";
import { decodeUtf8, universalNewlines } from "../py/io";
import { strRepr } from "../py/repr";
import * as py from "../py";
import { type PyFloat, fl } from "./common";
import { secs } from "./score";

export interface RunResult { code: number | null; out: string; err: string; seconds: PyFloat; timeout: boolean }
export interface RunOpts { readonly cwd?: string | null; readonly timeout?: number }

export class Tools extends Context.Service<Tools, {
  /** subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout), as ev_common.run records it. */
  readonly run: (cmd: readonly string[], opts?: RunOpts) => Effect.Effect<RunResult>;
  /** shutil.which(name) */
  readonly which: (name: string) => string | null;
}>()("tdd/eval/Tools") {}

const elapsed = (t0: number): PyFloat => fl(py.round((performance.now() - t0) / 1000, 2));
const text = (b: Uint8Array): string => universalNewlines(decodeUtf8(b, "replace"));

function spawnRun(cmd: readonly string[], opts: RunOpts = {}): Effect.Effect<RunResult> {
  const timeout = opts.timeout ?? 300;
  return Effect.callback<RunResult>((resume) => {
    const t0 = performance.now();
    let proc: ReturnType<typeof Bun.spawn>;
    try {
      proc = Bun.spawn([...cmd], { ...(opts.cwd ? { cwd: opts.cwd } : {}), stdin: "ignore", stdout: "pipe", stderr: "pipe" });
    } catch (e) {
      const msg = (e as Error).message ?? String(e);
      // Bun sets code ENOENT for a missing executable ("Executable not found in $PATH") and a missing path alike
      const missing = (e as { code?: string }).code === "ENOENT" || msg.includes("ENOENT");
      resume(Effect.succeed({
        code: null, out: "", seconds: elapsed(t0), timeout: false,
        err: missing ? `FileNotFoundError: [Errno 2] No such file or directory: ${strRepr(cmd[0] ?? "")}` : `OSError: ${msg}`,
      }));
      return;
    }
    let timedOut = false;
    let done = false;
    const timer = setTimeout(() => {
      timedOut = true;
      proc.kill("SIGKILL");
    }, timeout * 1000);
    const stdout = new Response(proc.stdout as ReadableStream).arrayBuffer();
    const stderr = new Response(proc.stderr as ReadableStream).arrayBuffer();
    Promise.all([stdout, stderr, proc.exited]).then(([o, e, code]) => {
      done = true;
      clearTimeout(timer);
      if (timedOut) {
        // subprocess.TimeoutExpired carries the raw bytes read so far; ev_common decodes them with "replace"
        resume(Effect.succeed({ code: null, out: decodeUtf8(new Uint8Array(o), "replace"), err: `timed out after ${secs(timeout)}s`, seconds: elapsed(t0), timeout: true }));
      } else {
        resume(Effect.succeed({ code, out: text(new Uint8Array(o)), err: text(new Uint8Array(e)), seconds: elapsed(t0), timeout: false }));
      }
    });
    // interruption (a scorer's own time limit): kill the child
    return Effect.sync(() => {
      clearTimeout(timer);
      if (!done) proc.kill("SIGKILL");
    });
  });
}

export const ToolsLive = Layer.succeed(Tools)({ run: spawnRun, which: (name: string) => Bun.which(name) });
