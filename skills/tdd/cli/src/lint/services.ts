// The linter's services (research/bun-port-2026.md section 4.2). Each is a Context.Service with a live layer; tests
// swap a layer for a fixture (a Git that answers from a table, a Clock in a pinned zone, an Agents check that raises).
//   Fs        the file system with Python's semantics (os.listdir order, getmtime's float, realpath, os.walk)
//   Git       read-only `git -C <repo> ...`, 60 s timeout, failures as (1, the text Python's subprocess gives)
//   Clock     the local-time stamp in META/stale-build (datetime.fromtimestamp: TZ decides, so tests pin TZ)
//   Compiler  the compile of a packet with no build: src/compile in-process, --no-copy, into a scoped temporary folder
//   Agents    write_agents.check(build) in-process (src/agents)
// The Packet service (the linted packet and the findings sink) is in packet.ts.
import { lstatSync, mkdtempSync, readdirSync, readlinkSync, rmSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { Context, Effect, Layer, type Scope } from "effect";
import * as py from "../py";
import { check as agentsCheck } from "../agents/write";
import { compile, isSystemExit, parse_args } from "../compile";
import { PyError, qualifiedName } from "../py/errors";
import { pyErrText, repr } from "./pyops";

// ---------------------------------------------------------------------------------------------------------- Fs
export interface FsApi {
  isfile(p: string): boolean;
  isdir(p: string): boolean;
  /** os.listdir(p): the file system's order; throws when p cannot be listed. */
  listdir(p: string): string[];
  /** open(p, "rb").read() */
  readBytes(p: string): Uint8Array;
  /** open(p, encoding="utf-8").read(): strict UTF-8, universal newlines. */
  readText(p: string): string;
  getsize(p: string): number;
  /** os.path.getmtime(p): CPython's float, sec + nsec * 1e-9. */
  getmtime(p: string): number;
  /** os.path.realpath(p) (strict=False). */
  realpath(p: string): string;
  /** os.walk(top) top-down, followlinks=False. */
  walk(top: string): Iterable<[string, string[], string[]]>;
}

function mtime(p: string): number {
  const ns = statSync(p, { bigint: true }).mtimeNs;
  const sec = ns >= 0n ? ns / 1_000_000_000n : -((-ns + 999_999_999n) / 1_000_000_000n);
  const nsec = ns - sec * 1_000_000_000n;
  return Number(sec) + Number(nsec) * 1e-9;
}

/** posixpath.realpath(filename, strict=False) as CPython 3.13 walks it. */
export function realpath(filename: string): string {
  const rest: (string | null)[] = filename.split("/").reverse();
  let partCount = rest.length;
  let path = filename.startsWith("/") ? "/" : process.cwd();
  const seen = new Map<string, string | null>();
  while (partCount) {
    const name = rest.pop()!;
    if (name === null) {
      seen.set(rest.pop() as string, path);
      continue;
    }
    partCount -= 1;
    if (!name || name === ".") continue;
    if (name === "..") {
      path = path.slice(0, path.lastIndexOf("/")) || "/";
      continue;
    }
    const newpath = path === "/" ? path + name : path + "/" + name;
    let target: string;
    try {
      const st = lstatSync(newpath);
      if (!st.isSymbolicLink()) {
        path = newpath;
        continue;
      }
      if (seen.has(newpath)) {
        const cached = seen.get(newpath)!;
        path = cached !== null ? cached : newpath;
        continue;
      }
      target = readlinkSync(newpath);
    } catch {
      path = newpath;
      continue;
    }
    if (target.startsWith("/")) path = "/";
    seen.set(newpath, null);
    rest.push(newpath, null);
    const parts = target.split("/").reverse();
    rest.push(...parts);
    partCount += parts.length;
  }
  return path;
}

function* osWalk(top: string): Generator<[string, string[], string[]]> {
  const stack = [top];
  while (stack.length) {
    const d = stack.pop()!;
    let names: string[];
    try {
      names = readdirSync(d);
    } catch {
      continue;
    }
    const dirs: string[] = [], files: string[] = [];
    for (const n of names) (py.io.isdir(py.path.join(d, n)) ? dirs : files).push(n);
    yield [d, dirs, files];
    for (let i = dirs.length - 1; i >= 0; i--) {
      const p = py.path.join(d, dirs[i]!);
      let link = false;
      try {
        link = lstatSync(p).isSymbolicLink();
      } catch {
        link = false;
      }
      if (!link) stack.push(p);
    }
  }
}

export const liveFs: FsApi = {
  isfile: py.io.isfile,
  isdir: py.io.isdir,
  listdir: (p) => readdirSync(p),
  readBytes: py.io.readBytes,
  readText: py.io.readText,
  getsize: (p) => statSync(p).size,
  getmtime: mtime,
  realpath,
  walk: osWalk,
};

export class Fs extends Context.Service<Fs, FsApi>()("tdd/lint/Fs") {}
export const FsLive = Layer.succeed(Fs, liveFs);

// ---------------------------------------------------------------------------------------------------------- Git
/** git(repo, *args) -> [returncode, stdout when 0 else stderr]; never fails. */
export interface GitApi {
  readonly run: (repo: string, args: readonly string[]) => Effect.Effect<readonly [number, Uint8Array]>;
}

const enc = new TextEncoder();
export const GIT_TIMEOUT_S = 60;

/** str(subprocess.TimeoutExpired(cmd, timeout)) */
export const timeoutText = (cmd: readonly string[], seconds: number): string =>
  `Command '${repr([...cmd])}' timed out after ${seconds} seconds`;

/** The live Git: `git -C repo ...` through Bun.spawn, killed after `timeoutS` seconds (lint_packet.git). */
export function gitLive(timeoutS = GIT_TIMEOUT_S, gitBin = "git"): GitApi {
  return {
    run: (repo, args) =>
      Effect.promise(async (): Promise<readonly [number, Uint8Array]> => {
        const cmd = [gitBin, "-C", repo, ...args];
        let proc: ReturnType<typeof Bun.spawn>;
        try {
          proc = Bun.spawn(cmd, { stdin: "ignore", stdout: "pipe", stderr: "pipe" });
        } catch (e) {
          return [1, enc.encode(pyErrText(withPath(e, gitBin)))] as const;
        }
        // subprocess.run(timeout=60): the whole exchange (exit and both pipes) races the clock; on expiry the child is
        // killed and waited for, and its pipes are not drained (a grandchild may hold them open)
        const done = Promise.all([
          new Response(proc.stdout as ReadableStream).arrayBuffer(),
          new Response(proc.stderr as ReadableStream).arrayBuffer(),
          proc.exited,
        ]);
        let timer: ReturnType<typeof setTimeout> | undefined;
        const clock = new Promise<null>((resolve) => { timer = setTimeout(() => resolve(null), timeoutS * 1000); });
        const r = await Promise.race([done, clock]);
        clearTimeout(timer);
        if (r === null) {
          proc.kill("SIGKILL");
          await proc.exited;
          return [1, enc.encode(timeoutText(cmd, timeoutS))] as const;
        }
        const [out, err, code] = r;
        return [code, new Uint8Array(code === 0 ? out : err)] as const;
      }),
  };
}

function withPath(e: unknown, path: string): unknown {
  const x = e as { code?: string; path?: string };
  if (x && typeof x === "object" && x.code === "ENOENT" && x.path === undefined) {
    try {
      x.path = path;
    } catch {
      /* frozen error: keep it */
    }
  }
  return e;
}

export class Git extends Context.Service<Git, GitApi>()("tdd/lint/Git") {}
export const GitLive = Layer.succeed(Git, gitLive());

// ---------------------------------------------------------------------------------------------------------- Clock
export interface ClockApi {
  /** f"{datetime.fromtimestamp(t):%Y-%m-%d %H:%M}" in the process's zone ($TZ). */
  readonly localMinute: (seconds: number) => string;
}
export class Clock extends Context.Service<Clock, ClockApi>()("tdd/lint/Clock") {}
export const ClockLive = Layer.succeed(Clock, { localMinute: (s: number) => py.time.formatLocalMinute(s) });

// ---------------------------------------------------------------------------------------------------------- Compiler
/** One compile of a source into an out folder with --no-copy: null, or how it failed ([exit code, last output line]). */
export type CompileFailure = readonly [number, string] | null;

/** A compile of the packet source into a fresh scoped temporary folder: the folder, and the failure's last line or null. */
export type CompileResult = { readonly dir: string; readonly error: string | null };

export interface CompilerApi {
  /** lint_packet.Lint.compile: `build_tdd.py <src> <tmp> --no-copy` in cwd; the folder is removed when the scope closes. */
  readonly compile: (src: string, cwd: string, prefix: string) => Effect.Effect<CompileResult, never, Scope.Scope>;
  /** Whether there is a compiler to run (lint_packet: build_tdd.py beside the script). */
  readonly available: () => boolean;
}

/** skills/tdd/scripts, the Python scripts' folder ($TDD_SCRIPTS overrides, for tests). */
export const SCRIPTS = (): string => process.env.TDD_SCRIPTS || py.path.normpath(py.path.join(import.meta.dir, "..", "..", "..", "scripts"));

/** A scoped temporary folder (tempfile.mkdtemp(prefix=...)), removed with its contents when the scope closes. */
export const tempDir = (prefix: string) =>
  Effect.acquireRelease(
    Effect.sync(() => mkdtempSync(py.path.join(tmpdir(), prefix))),
    (d) => Effect.sync(() => rmSync(d, { recursive: true, force: true })),
  );

const dec = (b: Uint8Array): string => py.io.universalNewlines(py.io.decodeUtf8(b, "replace"));

/** The last line of a failed run: (p.stderr or p.stdout).strip().splitlines()[-1:] or [fallback]. */
export function tailLine(stderr: string, stdout: string, fallback: string): string {
  const lines = py.str.splitlines(py.str.strip(stderr || stdout));
  return lines.length ? lines[lines.length - 1]! : fallback;
}

/** build_tdd.py through python3 -I as a subprocess (env adds to the inherited environment). */
export function pythonCompileOnce(python: string, src: string, cwd: string, out: string, env: Record<string, string> = {}, timeoutS = 0): CompileFailure {
  const full: Record<string, string> = {};
  for (const [k, v] of Object.entries({ ...process.env, ...env })) if (v !== undefined) full[k] = v;
  const r = Bun.spawnSync([python, "-I", py.path.join(SCRIPTS(), "build_tdd.py"), src, out, "--no-copy"],
    { cwd, env: full, stdin: "ignore", stdout: "pipe", stderr: "pipe", ...(timeoutS ? { timeout: timeoutS * 1000 } : {}) });
  const code = r.exitCode ?? 1;
  if (code !== 0 || !py.io.isfile(py.path.join(out, "content.json"))) return [code, tailLine(dec(r.stderr), dec(r.stdout), "")];
  return null;
}

/** The TypeScript compiler (src/compile) in-process, run as `build_tdd.py <src> <out> --no-copy` would run in cwd, with
 * TDD_GENERATED set to `generated` when one is given; cwd and the variable are restored afterwards. A SystemExit is the
 * Python's sys.exit (its message on stderr, its code); any other error is an uncaught exception (traceback, exit 1). */
export function tsCompileOnce(src: string, cwd: string, out: string, generated: string | null = null): CompileFailure {
  const here = process.cwd();
  const had = Object.hasOwn(process.env, "TDD_GENERATED"), was = process.env["TDD_GENERATED"];
  try {
    process.chdir(cwd);
    if (generated !== null) process.env["TDD_GENERATED"] = generated;
    compile(parse_args([src, out, "--no-copy"]));
    return py.io.isfile(py.path.join(out, "content.json")) ? null : [0, ""];
  } catch (e) {
    if (isSystemExit(e)) return [typeof e.code === "number" ? e.code : 1, tailLine(e.message, "", "")];
    if (e instanceof PyError) return [1, tailLine(`${qualifiedName(e.type)}: ${e.message}`, "", "")];
    const err = e as { name?: string; message?: string };
    return [1, tailLine(`${err?.name ?? "Error"}: ${err?.message ?? String(e)}`, "", "")];
  } finally {
    process.chdir(here);
    if (generated !== null) {
      if (had) process.env["TDD_GENERATED"] = was;
      else delete process.env["TDD_GENERATED"];
    }
  }
}

/** The live Compiler: src/compile in-process (the Python linter runs build_tdd.py; the two compilers agree byte for
 * byte, which the PARITY rule checks). */
export const tsCompiler: CompilerApi = {
  available: () => true,
  compile: (src, cwd, prefix) =>
    Effect.gen(function* () {
      const dir = yield* tempDir(prefix);
      const f = tsCompileOnce(src, cwd, dir);
      return { dir, error: f === null ? null : f[1] || "no output" };
    }),
};

/** build_tdd.py through python3 as the Compiler (tests, and a fallback while the two compilers are compared). */
export function pythonCompiler(python = "python3"): CompilerApi {
  return {
    available: () => py.io.isfile(py.path.join(SCRIPTS(), "build_tdd.py")),
    compile: (src, cwd, prefix) =>
      Effect.gen(function* () {
        const dir = yield* tempDir(prefix);
        if (Bun.which(python) === null) return { dir, error: `${python} is not on PATH` };
        const f = pythonCompileOnce(python, src, cwd, dir);
        return { dir, error: f === null ? null : f[1] || "no output" };
      }),
  };
}

export class Compiler extends Context.Service<Compiler, CompilerApi>()("tdd/lint/Compiler") {}
export const CompilerLive = Layer.succeed(Compiler, tsCompiler);

// ---------------------------------------------------------------------------------------------------------- Agents
export interface AgentsApi {
  /** write_agents.check(build_dir): problems, empty when the lane is current; may throw (reported as AGENTS/check-failed). */
  readonly check: (build: string) => readonly unknown[];
}
export class Agents extends Context.Service<Agents, AgentsApi>()("tdd/lint/Agents") {}
export const AgentsLive = Layer.succeed(Agents, { check: (b: string) => agentsCheck(b) });

/** Every live service but Packet. */
export const ServicesLive = Layer.mergeAll(FsLive, GitLive, ClockLive, CompilerLive, AgentsLive);
