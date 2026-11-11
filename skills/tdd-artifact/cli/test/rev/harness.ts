// A scripted world for the runner's program: a temporary packet and skill folder, Tools and Proc that write what the
// real tools would write (content.json, tdd.html, agents/, lint.json, eval/run.json) and record every command and
// environment they were given, a Db whose probe answers as told, Secrets with fake keys, and the real Journal on the
// temporary build/.rev. Each step's behaviour can be scripted per test.
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { Effect, Exit, Layer, Redacted } from "effect";
import { parseArgs, type RevArgs } from "../../src/rev/args";
import { RevOpts, RevPaths } from "../../src/rev/context";
import { JournalLive } from "../../src/rev/journal";
import type { Paths } from "../../src/rev/paths";
import { revProgram, type Interrupt, type Report } from "../../src/rev/run";
import { makeSecrets, Secrets } from "../../src/rev/secrets";
import { Db, Out, Proc, Tools, type Captured, type Probe, type ProcResult, type ProcSpec } from "../../src/rev/services";

export const CLI = join(import.meta.dir, "..", "..");
export const FAKE_KEY = "bt-fake-key-0000-not-a-real-key";
export const FAKE_ANTHROPIC = "sk-ant-fake-0000-not-a-real-key";

export interface World {
  readonly root: string;
  readonly paths: Paths;
  /** Every child command the steps started, with the environment it was given. */
  readonly spawned: ProcSpec[];
  /** In-process tool calls: [tool, argv]. */
  readonly called: [string, string[]][];
  /** Script: what a command returns (by a substring of its argv); default ok. */
  hydrate: "ok" | "fail" | "hang";
  artifactCheck: "ok" | "fail";
  probe: Probe;
  /** Appended to the compile's summary line (a tool that prints a key). */
  leak: string;
  evalCode: number;
  readonly out: string[];
  readonly err: string[];
}

const SAVED = { SYSTEM_DESIGN_SKILL: process.env["SYSTEM_DESIGN_SKILL"], TDD_EVAL_PYTHON: process.env["TDD_EVAL_PYTHON"] };
/** Put back the two variables makeWorld sets, so other test files see the real environment. */
export function restoreEnv(): void {
  for (const [k, v] of Object.entries(SAVED)) {
    if (v === undefined) delete process.env[k];
    else process.env[k] = v;
  }
}

export function makeWorld(slug = "demo-packet"): World {
  const root = mkdtempSync(join(tmpdir(), "tdd-rev-"));
  const packet = join(root, "house", "system-design", slug);
  mkdirSync(packet, { recursive: true });
  writeFileSync(join(packet, `${slug}.md`), "# Demo\n");
  writeFileSync(join(packet, "meta.json"), JSON.stringify({ title: "Demo", rev: 1, owner: "Owner" }));
  const skill = join(root, "skill");
  mkdirSync(join(skill, "app", "node_modules"), { recursive: true });
  mkdirSync(join(skill, "app", "src"), { recursive: true });
  writeFileSync(join(skill, "app", "build.ts"), "// fake\n");
  writeFileSync(join(skill, "app", "check_hydrate.ts"), "// fake\n");
  mkdirSync(join(skill, "scripts", "eval"), { recursive: true });
  writeFileSync(join(skill, "scripts", "eval_packet.py"), "# fake\n");
  const sd = join(root, "system-design");
  mkdirSync(join(sd, "scripts"), { recursive: true });
  writeFileSync(join(sd, "scripts", "artifact_check.mjs"), "// fake\n");
  writeFileSync(join(root, "python"), "#!/bin/sh\n");
  process.env["SYSTEM_DESIGN_SKILL"] = sd;
  process.env["TDD_EVAL_PYTHON"] = join(root, "python");
  const build = join(packet, "build");
  const paths: Paths = { packet, build, rev: join(build, ".rev"), slug, source: `${slug}.md`, skill, cli: CLI, app: join(skill, "app") };
  return { root, paths, spawned: [], called: [], hydrate: "ok", artifactCheck: "ok", probe: { ok: true }, leak: "", evalCode: 0, out: [], err: [] };
}

const ok = (out: string, code = 0): ProcResult => ({ code, out, err: "", timedOut: false });

function procFor(w: World) {
  const b = w.paths.build;
  return {
    run: (spec: ProcSpec) => {
      w.spawned.push(spec);
      const cmd = spec.cmd.join(" ");
      if (cmd.includes("build.ts")) {
        writeFileSync(join(b, "tdd.html"), "<html>page</html>");
        return Effect.succeed(ok(`wrote ${b}/tdd.html: 17 bytes (0.0 KB)\n`));
      }
      if (cmd.includes("check_hydrate.ts")) {
        if (w.hydrate === "hang") return Effect.never;
        return Effect.succeed(w.hydrate === "ok"
          ? ok("console errors 0 | console warnings 0 (hydration 0) | failed steps 0\n")
          : { code: 1, out: "console errors 2 | console warnings 0 (hydration 0) | failed steps 0\n", err: "TypeError: x is undefined\n", timedOut: false });
      }
      if (cmd.includes("eval_packet.py") || cmd.includes(" eval ")) {
        mkdirSync(join(b, "eval"), { recursive: true });
        writeFileSync(join(b, "eval", "run.json"), JSON.stringify({ pushed: false }));
        const push = spec.cmd.includes("--push");
        if (push && w.evalCode === 0) writeFileSync(join(b, "eval", "push.json"), "{}");
        const text = "scored 3 · metrics 1 · not run 2 · errors 0  | files: run.json\n" +
          (push ? "dataset: https://example.invalid/ds\n" : "not pushed: run.json holds the dataset record and 6 experiments a --push would send\n");
        return Effect.succeed(ok(text, w.evalCode));
      }
      if (cmd.includes("load-db")) return Effect.succeed(ok("tdd load-db house/demo-packet rev 1 -> localhost/tdd_system_test: packet 1\n"));
      if (cmd.includes("artifact_check.mjs")) {
        return Effect.succeed(w.artifactCheck === "ok" ? ok("artifact_check: ok (6 renders, 0 warning(s)); screenshots in shots\n")
          : { code: 1, out: "artifact_check: 1 failure(s)\n", err: "FAIL: contract\n", timedOut: false });
      }
      return Effect.succeed(ok("", 0));
    },
  };
}

function toolsFor(w: World) {
  const b = w.paths.build;
  const done = (out: string): Effect.Effect<Captured> => Effect.succeed({ code: 0, out, err: "" });
  return {
    compile: (argv: readonly string[]) => {
      w.called.push(["compile", [...argv]]);
      mkdirSync(b, { recursive: true });
      writeFileSync(join(b, "content.json"), JSON.stringify({ generated: "2026-11-08T12:00:00Z", source: `${w.paths.slug}.md` }));
      writeFileSync(join(b, "content.files.json"), JSON.stringify({ files: {} }));
      return done(`wrote ${b}/content.json: 1 sections${w.leak ? " " + w.leak : ""}\n`);
    },
    agents: (argv: readonly string[]) => {
      w.called.push(["agents", [...argv]]);
      mkdirSync(join(b, "agents"), { recursive: true });
      writeFileSync(join(b, "agents", "README.md"), "lane\n");
      return done(`wrote ${b}/agents (content.json): 1 files\n`);
    },
    lint: (argv: readonly string[]) => {
      w.called.push(["lint", [...argv]]);
      const json = argv[argv.indexOf("--json") + 1]!;
      writeFileSync(json, JSON.stringify({ counts: { errors: 2, warnings: 3 } }));
      return Effect.succeed({ code: 1, out: "2 errors, 3 warnings\n", err: "" });
    },
  };
}

export function layerFor(w: World, args: RevArgs, keys: Record<string, string> = {}) {
  const values = new Map(Object.entries(keys).map(([k, v]) => [k, Redacted.make(v)] as const));
  const env: Record<string, string> = { PATH: process.env["PATH"] ?? "", HOME: process.env["HOME"] ?? "", ...keys };
  const base = Layer.mergeAll(
    Layer.succeed(RevPaths, w.paths), Layer.succeed(RevOpts, args), Layer.succeed(Secrets, makeSecrets(values, env)),
    Layer.succeed(Out, { child: (s) => void w.out.push(s), childErr: (s) => void w.err.push(s), note: (s) => void w.err.push(s), result: (s) => void w.out.push(s) }),
    Layer.succeed(Proc, procFor(w)), Layer.succeed(Tools, toolsFor(w)), Layer.succeed(Db, { probe: () => Effect.succeed(w.probe) }),
  );
  return JournalLive.pipe(Layer.provideMerge(base));
}

export function args(w: World, ...rest: string[]): RevArgs {
  const a = parseArgs([w.paths.packet, ...rest]);
  if (a === "help" || "error" in a) throw new Error(`bad args ${rest.join(" ")}`);
  return a;
}

/** Run the program once; `signal` aborts it (an interrupt). */
export async function runOnce(w: World, a: RevArgs, opts: { keys?: Record<string, string>; signal?: AbortSignal; interrupt?: Interrupt } = {}) {
  const interrupt = opts.interrupt ?? { by: null };
  const exit = await Effect.runPromiseExit(revProgram(interrupt).pipe(Effect.provide(layerFor(w, a, opts.keys))), opts.signal ? { signal: opts.signal } : undefined);
  return { exit, report: Exit.isSuccess(exit) ? exit.value : (null as Report | null) };
}

export const journal = (w: World): Record<string, any>[] => {
  const p = join(w.paths.rev, "journal.jsonl");
  return existsSync(p) ? readFileSync(p, "utf8").trim().split("\n").map((l) => JSON.parse(l)) : [];
};
export const state = (w: World): Record<string, any> => JSON.parse(readFileSync(join(w.paths.rev, "state.json"), "utf8"));
export const outcomes = (r: Report | null) => Object.fromEntries((r?.rows ?? []).map((x) => [x.step, x.ran ? x.outcome : `(${x.outcome})`]));
