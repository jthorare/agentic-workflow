// External commands as a service (ev_common.run / which): git, gh, node artifact_check.mjs, bun check_hydrate.ts,
// python3 lint_packet.py and a repo's test command. The live layer spawns with Bun under a time limit and returns the
// Python's record {code, out, err, seconds, timeout} (code None when the command could not start or timed out); a time
// limit or an interrupted scorer kills the child's whole process group. Children inherit this process's environment,
// keys included, exactly as subprocess.run without env= does in the Python (the repo's test command under --run-tests
// sees them too). Tests provide a layer that answers from fixtures.
import { constants as osConstants } from "node:os";
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

// Children run in their own process group (detached: setsid), so a time limit or an interruption kills the command and
// everything it started: a test runner's workers, a `sleep | cat` pipeline. Groups still running when this process is
// interrupted or exits are killed too (the handlers are installed only while a group is live).
const liveGroups = new Set<number>();
const killGroup = (pid: number): void => {
  try {
    process.kill(-pid, "SIGKILL");
  } catch {
    // the group is gone (ESRCH): nothing left to kill
  }
};
const onExit = (): void => {
  for (const pid of liveGroups) killGroup(pid);
};
const onSignal = (sig: NodeJS.Signals): void => {
  onExit();
  liveGroups.clear();
  unhook();
  process.kill(process.pid, sig); // the default action, now that the children are gone
};
const onInt = () => onSignal("SIGINT");
const onTerm = () => onSignal("SIGTERM");
let hooked = false;
function hook(pid: number): void {
  liveGroups.add(pid);
  if (hooked) return;
  hooked = true;
  process.on("exit", onExit);
  process.on("SIGINT", onInt);
  process.on("SIGTERM", onTerm);
}
function unhook(pid?: number): void {
  if (pid !== undefined) liveGroups.delete(pid);
  if (!hooked || liveGroups.size) return;
  hooked = false;
  process.off("exit", onExit);
  process.off("SIGINT", onInt);
  process.off("SIGTERM", onTerm);
}

const concat = (parts: Uint8Array[]): Uint8Array => {
  const out = new Uint8Array(parts.reduce((a, p) => a + p.length, 0));
  let i = 0;
  for (const p of parts) {
    out.set(p, i);
    i += p.length;
  }
  return out;
};

/** subprocess's returncode: the exit status, or -N when signal N ended the child (Bun reports 128 + N). */
const returncode = (proc: { exitCode: number | null; signalCode: string | null }, fallback: number): number => {
  if (proc.signalCode) {
    const n = (osConstants.signals as Record<string, number>)[proc.signalCode];
    if (n) return -n;
  }
  return proc.exitCode ?? fallback;
};

/**
 * ev_common.run: subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout). The deadline holds
 * whatever the child does: at `timeout` seconds the child's process group is killed, the child is waited for, and the
 * record is made from the output read so far, without waiting for the pipes to close (a grandchild that escaped the
 * group may still hold them; subprocess.run does not drain them either). An interrupted scorer kills the group too.
 */
function spawnRun(cmd: readonly string[], opts: RunOpts = {}): Effect.Effect<RunResult> {
  const timeout = opts.timeout ?? 300;
  return Effect.callback<RunResult>((resume) => {
    const t0 = performance.now();
    let proc: ReturnType<typeof Bun.spawn>;
    try {
      proc = Bun.spawn([...cmd], { ...(opts.cwd ? { cwd: opts.cwd } : {}), stdin: "ignore", stdout: "pipe", stderr: "pipe", detached: true });
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
    const pid = proc.pid;
    hook(pid);
    const out: Uint8Array[] = [];
    const err: Uint8Array[] = [];
    const readers: ReadableStreamDefaultReader<Uint8Array>[] = [];
    const pump = (stream: ReadableStream<Uint8Array>, sink: Uint8Array[]): Promise<void> => {
      const reader = stream.getReader();
      readers.push(reader);
      return (async () => {
        for (;;) {
          const { done, value } = await reader.read();
          if (done) return;
          sink.push(value);
        }
      })().catch(() => undefined);
    };
    const pipes = Promise.all([pump(proc.stdout as ReadableStream<Uint8Array>, out), pump(proc.stderr as ReadableStream<Uint8Array>, err)]);
    let settled = false;
    const release = (): void => {
      for (const r of readers) r.cancel().catch(() => undefined);
    };
    const timer = setTimeout(() => {
      if (settled) return;
      settled = true;
      killGroup(pid);
      // process.kill(); process.wait(): a SIGKILLed child exits at once; the cap guards an unkillable (stopped) one
      void Promise.race([proc.exited, Bun.sleep(5000)]).then(() => {
        unhook(pid);
        const partial = concat(out);
        release();
        // subprocess.TimeoutExpired carries the raw bytes read so far; ev_common decodes them with "replace"
        resume(Effect.succeed({ code: null, out: decodeUtf8(partial, "replace"), err: `timed out after ${secs(timeout)}s`, seconds: elapsed(t0), timeout: true }));
      });
    }, timeout * 1000);
    void Promise.all([pipes, proc.exited]).then(([, code]) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      unhook(pid); // a process left in the group with its pipes closed keeps running, as under subprocess.run
      resume(Effect.succeed({ code: returncode(proc, code), out: text(concat(out)), err: text(concat(err)), seconds: elapsed(t0), timeout: false }));
    });
    // interruption (a scorer's own time limit): kill the group and stop reading
    return Effect.sync(() => {
      clearTimeout(timer);
      if (settled) return;
      settled = true;
      killGroup(pid);
      unhook(pid);
      release();
    });
  });
}

export const ToolsLive = Layer.succeed(Tools)({ run: spawnRun, which: (name: string) => Bun.which(name) });
