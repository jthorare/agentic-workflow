// What each step runs, and what it reads and writes (for --reuse and the resume guard). The commands are SKILL.md's
// step 5, from the same folders: the compile, agents and lint in-process from the packet folder; the app build and
// the hydrate check from app/; the eval, load-db and artifact_check from the packet folder.
import { existsSync, readFileSync, rmSync, statSync } from "node:fs";
import { join } from "node:path";
import { Context, Effect, Option, Redacted } from "effect";
import { DEFAULT_DATABASE_URL } from "../db/load";
import type { RevArgs } from "./args";
import { RevOpts, RevPaths } from "./context";
import { evalStep, revBun } from "./eval";
import type { Part } from "./hash";
import { artifactCheckPath, type Paths } from "./paths";
import { evalPushKeys, Secrets, type SecretsApi } from "./secrets";
import { Db, Proc, Tools, type ProcResult } from "./services";
import { Fail, lastLine, Skip, tail, type Done, type StepName } from "./steps";

/** What one run remembers between steps: the eval's run, so the push step can read its push instead of pushing again. */
export interface EvalRun {
  readonly push: boolean;
  readonly code: number;
  readonly out: string;
  readonly err: string;
  readonly started: number;
}
export interface MemoApi {
  eval: EvalRun | null;
}
export class RunMemo extends Context.Service<RunMemo, MemoApi>()("tdd/rev/RunMemo") {}

export type StepEnv = RevPaths | RevOpts | Secrets | Proc | Tools | Db | RunMemo;

export interface StepDef {
  readonly name: StepName;
  /** What the step reads; null: never reused, never guarded (the push). */
  // `s`: the run's environment snapshot (the runner withdraws the keys from process.env while it runs)
  readonly inputs: ((p: Paths, a: RevArgs, s: Pick<SecretsApi, "databaseUrl" | "braintrust">) => readonly Part[]) | null;
  /** What the step writes, checked unchanged before a reuse (empty: nothing to check). */
  readonly outputs: (p: Paths) => readonly Part[];
  readonly run: Effect.Effect<Done, Skip | Fail, StepEnv>;
}

const BUN = revBun;
const MIN = 60_000;
/** Never inputs: version control and the agent's own folders. */
const VCS = [".git", ".claude"];
/** What the compile reads from the packet: everything but the build, artifact_check's shots, the run logs of a DAG
 *  and status.md (build_tdd.py reads none of them). An edit to any other file reruns the compile. */
const COMPILE_SKIPS = ["build", "shots", "dag-runs", "status.md", ...VCS];

const failed = (what: string, r: ProcResult): Fail =>
  new Fail({ error: r.timedOut ? `${what} timed out` : `${what} exited ${r.code}`, tail: tail(r.err || r.out) });

const mtimeMs = (p: string): number => {
  try { return statSync(p).mtimeMs; } catch { return -1; }
};
/** Written by a run that started at `started` (2 s of slack for file systems with coarse times). */
const freshSince = (p: string, started: number): boolean => mtimeMs(p) >= started - 2000;

const cliTrees = (p: Paths, ...rel: string[]): Part[] => rel.map((r) => (r.endsWith(".ts") ? { file: join(p.cli, r) } : { tree: join(p.cli, r) }));

/** The database url (from the environment only) and its label without credentials. */
export function dbTarget(url: Option.Option<Redacted.Redacted<string>>): { url: string; label: string } {
  const u = Option.match(url, { onNone: () => DEFAULT_DATABASE_URL, onSome: (r) => Redacted.value(r) });
  try {
    const p = new URL(u);
    return { url: u, label: `${p.hostname}${p.port ? ":" + p.port : ""}${p.pathname}` };
  } catch {
    return { url: u, label: "(the database url)" };
  }
}

// ---------------------------------------------------------------------------------------------------------- eval
export const evalArgs = (a: RevArgs, push: boolean): string[] =>
  [...(a.noShots ? ["--no-shots"] : []), ...(a.noGh ? ["--no-gh"] : []), ...(push ? ["--push"] : ["--skip", "linear,aws"])];

/** Whether this run's eval pushes: --push with the key in the environment. */
export const evalPushes = (a: RevArgs, s: Pick<SecretsApi, "braintrust">): boolean => a.push && Option.isSome(s.braintrust);

/** Run the eval (evalStep) once: offline (--skip linear,aws, no keys) unless `push`. */
const runEval = (push: boolean): Effect.Effect<EvalRun, Skip | Fail, StepEnv> =>
  Effect.gen(function* () {
    const p = yield* RevPaths, a = yield* RevOpts, proc = yield* Proc, secrets = yield* Secrets, memo = yield* RunMemo;
    const c = evalStep(p, push).command(p, evalArgs(a, push));
    if ("notRun" in c) return yield* new Skip({ reason: c.notRun });
    const started = Date.now();
    const env = secrets.childEnv(push ? evalPushKeys(a.noGh) : []);
    const r = yield* proc.run({ cmd: c.cmd, cwd: p.packet, env, timeoutMs: 30 * MIN });
    const run: EvalRun = { push, code: r.code, out: r.out, err: r.err, started };
    memo.eval = run;
    if (r.timedOut) return yield* failed("eval", r);
    return run;
  });

const SUMMARY = /^scored \d+ · metrics \d+ · not run \d+ · errors \d+/m;

// --------------------------------------------------------------------------------------------------------- steps
export const STEP_DEFS: readonly StepDef[] = [
  {
    name: "compile",
    inputs: (p) => [
      { tree: p.packet, exclude: COMPILE_SKIPS },
      ...cliTrees(p, "src/compile", "src/sd", "src/py", "src/vocab.ts"),
      { label: "TDD_GENERATED", value: process.env["TDD_GENERATED"] ?? "" },
    ],
    outputs: (p) => [{ file: join(p.build, "content.json") }],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, tools = yield* Tools;
      if (!p.source) return yield* new Fail({ error: `no source markdown in the packet (expected ${p.slug}.md)`, tail: "" });
      const r = yield* tools.compile([p.source, "build"]);
      if (r.code !== 0) return yield* new Fail({ error: `tdd build exited ${r.code}`, tail: tail(r.err || r.out) });
      const warnings = r.err.split("\n").filter((l) => l.startsWith("warning: ")).length;
      return { detail: lastLine(r.out) + (warnings ? ` (${warnings} warning${warnings === 1 ? "" : "s"} on stderr)` : "") };
    }),
  },
  {
    name: "app-build",
    inputs: (p) => [
      { file: join(p.build, "content.json") }, { tree: join(p.app, "src") }, { file: join(p.app, "build.ts") },
      { file: join(p.app, "package.json") }, { file: join(p.app, "bun.lock") }, { file: join(p.skill, "templates", "tdd-system", "tdd.css") },
    ],
    outputs: (p) => [{ file: join(p.build, "tdd.html") }, { file: join(p.build, "content.files.json") }],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, proc = yield* Proc, secrets = yield* Secrets;
      if (!existsSync(join(p.app, "node_modules"))) {
        return yield* new Fail({ error: "app/node_modules is absent: run `bun install` in the skill's app folder once (SKILL.md step 5)", tail: "" });
      }
      if (!existsSync(join(p.build, "content.json"))) return yield* new Fail({ error: "build/content.json is absent: the compile step has not run", tail: "" });
      const r = yield* proc.run({ cmd: [BUN(), "run", "build.ts", p.build], cwd: p.app, env: secrets.childEnv(), timeoutMs: 5 * MIN });
      if (r.code !== 0) return yield* failed("app/build.ts", r);
      const first = r.out.split("\n").find((l) => l.startsWith("wrote ")) ?? lastLine(r.out);
      return { detail: first.split(p.build + "/").join("build/") };
    }),
  },
  {
    name: "hydrate",
    inputs: (p) => [{ file: join(p.build, "tdd.html") }, { file: join(p.app, "check_hydrate.ts") }, { file: join(p.app, "package.json") }],
    outputs: () => [],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, proc = yield* Proc, secrets = yield* Secrets;
      const r = yield* proc.run({ cmd: [BUN(), "run", "check_hydrate.ts", join(p.build, "tdd.html")], cwd: p.app, env: secrets.childEnv(), timeoutMs: 5 * MIN });
      const line = /console errors \d+ \| console warnings \d+ \(hydration \d+\) \| failed steps \d+/.exec(r.out)?.[0];
      if (r.code !== 0) {
        const f = failed("app/check_hydrate.ts", r);
        return yield* (line ? new Fail({ error: `${f.error}: ${line}`, tail: f.tail }) : f);
      }
      return { detail: line ?? lastLine(r.out) };
    }),
  },
  {
    name: "agents",
    inputs: (p) => [
      { file: join(p.build, "content.json") }, { file: join(p.build, "content.files.json") }, { file: join(p.build, "tdd.html") },
      ...cliTrees(p, "src/agents", "src/py"),
    ],
    outputs: (p) => [{ tree: join(p.build, "agents") }],
    run: Effect.gen(function* () {
      const tools = yield* Tools;
      const r = yield* tools.agents(["build"]);
      if (r.code !== 0) return yield* new Fail({ error: `tdd agents exited ${r.code}`, tail: tail(r.err || r.out) });
      const line = lastLine(r.out);
      return { detail: line.length > 240 ? line.slice(0, 237) + "..." : line };
    }),
  },
  {
    name: "lint",
    inputs: (p) => [
      { tree: p.packet, exclude: ["build/lint.json", "build/eval", "build/.rev", "shots", ...VCS] },
      ...cliTrees(p, "src/lint", "src/compile", "src/agents", "src/hotspots", "src/sd", "src/py", "src/vocab.ts"),
      { file: join(p.skill, "scripts", "build_tdd.py") },
    ],
    outputs: (p) => [{ file: join(p.build, "lint.json") }],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, tools = yield* Tools;
      const json = join(p.build, "lint.json");
      rmSync(json, { force: true }); // a lint that cannot finish leaves no lint.json behind for load-db to trust
      const r = yield* tools.lint([p.packet, "--json", json]);
      if ((r.code === 0 || r.code === 1) && existsSync(json)) {
        let counts: { errors?: unknown; warnings?: unknown } = {};
        try { counts = (JSON.parse(readFileSync(json, "utf8")) as { counts?: typeof counts }).counts ?? {}; } catch { /* reported below */ }
        const data: Record<string, number> = {};
        if (typeof counts.errors === "number") data["lint_errors"] = counts.errors;
        if (typeof counts.warnings === "number") data["lint_warnings"] = counts.warnings;
        return { detail: `${String(counts.errors)} error(s), ${String(counts.warnings)} warning(s) -> build/lint.json`, data };
      }
      return yield* new Fail({ error: `tdd lint exited ${r.code}${existsSync(json) ? "" : " and wrote no build/lint.json"}`, tail: tail(r.err || r.out) });
    }),
  },
  {
    name: "eval",
    inputs: (p, a, s) => [
      // shots/ included: the eval captures the screenshots artifact-check just wrote
      { tree: p.packet, exclude: ["build/eval", "build/.rev", ...VCS] },
      ...evalStep(p, evalPushes(a, s)).code(p),
      { label: "eval", value: `${evalStep(p, evalPushes(a, s)).name} ${evalArgs(a, evalPushes(a, s)).join(" ")}` },
    ],
    outputs: (p) => [{ file: join(p.build, "eval", "run.json") }],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, a = yield* RevOpts, secrets = yield* Secrets;
      const push = evalPushes(a, secrets);
      const r = yield* runEval(push);
      const summary = SUMMARY.exec(r.out)?.[0] ?? lastLine(r.out);
      const wrote = freshSince(join(p.build, "eval", "run.json"), r.started);
      if (r.code === 0 && wrote) return { detail: summary };
      if (r.code === 1 && push && wrote) return { detail: `${summary}; its push failed (see the push step)` };
      return yield* new Fail({ error: `eval exited ${r.code}${wrote ? "" : " and wrote no build/eval/run.json"}`, tail: tail(r.err || r.out) });
    }),
  },
  {
    name: "load-db",
    inputs: (p, a, s) => [
      { file: join(p.packet, "meta.json") }, { file: join(p.build, "content.json") }, { file: join(p.build, "lint.json") },
      { tree: join(p.build, "eval"), exclude: ["shots", "history.jsonl"] }, ...cliTrees(p, "src/db", "sql"),
      { label: "database", value: dbTarget(s.databaseUrl).label },
      { label: "published", value: JSON.stringify(a.published) },
    ],
    outputs: () => [],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, a = yield* RevOpts, proc = yield* Proc, secrets = yield* Secrets, db = yield* Db;
      const { url, label } = dbTarget(secrets.databaseUrl);
      const probe = yield* db.probe(url);
      if (!probe.ok && !probe.answered) return yield* new Skip({ reason: `not run: Postgres is unreachable at ${label} (${probe.reason})` });
      if (!probe.ok) {
        return yield* new Fail({ error: `Postgres at ${label} refused the connection: ${probe.reason}; check $DATABASE_URL`, tail: "" });
      }
      const pub = a.published ? ["--published", a.published.surface, a.published.url] : [];
      const r = yield* proc.run({
        cmd: [BUN(), join(p.cli, "bin", "tdd.ts"), "load-db", p.packet, ...pub], cwd: p.packet,
        env: { ...secrets.childEnv(), DATABASE_URL: url }, timeoutMs: 5 * MIN,
      });
      if (r.code !== 0) return yield* failed("tdd load-db", r);
      return { detail: lastLine(r.out) };
    }),
  },
  {
    name: "push",
    inputs: null,
    outputs: () => [],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, a = yield* RevOpts, secrets = yield* Secrets, memo = yield* RunMemo;
      if (!a.push) return yield* new Skip({ reason: "not asked: --push not given (build/eval/run.json holds what a push would send)" });
      if (Option.isNone(secrets.braintrust)) {
        return yield* new Fail({ error: "--push needs BRAINTRUST_API_KEY in the environment (never in a file)", tail: "" });
      }
      // The push is the eval's own (eval_packet.py --push): read the eval step's run when it pushed, else run it now.
      const r = memo.eval?.push ? memo.eval : yield* runEval(true);
      const pushed = freshSince(join(p.build, "eval", "push.json"), r.started);
      if (r.code === 0 && pushed) {
        const ds = r.out.split("\n").find((l) => l.startsWith("dataset: "));
        return { detail: ds ? `pushed; ${ds}` : "pushed (build/eval/push.json)" };
      }
      return yield* new Fail({ error: `the eval's push failed (exit ${r.code})`, tail: tail(r.err || r.out) });
    }),
  },
  {
    name: "artifact-check",
    inputs: (p, a) => [
      { file: join(p.build, "tdd.html") }, { label: "artifact_check", value: artifactCheckPath(p.skill) ?? "" },
      ...(artifactCheckPath(p.skill) ? [{ file: artifactCheckPath(p.skill)! }] : []), { label: "no-shots", value: String(a.noShots) },
      { label: "node", value: Bun.which("node") ?? "" },
    ],
    outputs: () => [],
    run: Effect.gen(function* () {
      const p = yield* RevPaths, a = yield* RevOpts, proc = yield* Proc, secrets = yield* Secrets;
      const mjs = artifactCheckPath(p.skill);
      if (!mjs) return yield* new Skip({ reason: "not run: artifact_check.mjs is absent (set SYSTEM_DESIGN_SKILL to a system-design skill that has it)" });
      const node = Bun.which("node");
      if (!node) return yield* new Skip({ reason: "not run: node is not on PATH" });
      if (!existsSync(join(p.build, "tdd.html"))) return yield* new Fail({ error: "build/tdd.html is absent: the app-build step has not run", tail: "" });
      const r = yield* proc.run({
        cmd: [node, mjs, "build/tdd.html", "shots", ...(a.noShots ? ["--no-shots"] : [])], cwd: p.packet,
        env: secrets.childEnv(), timeoutMs: 10 * MIN,
      });
      if (r.code === 1) return yield* new Fail({ error: "artifact_check found contract failures (exit 1)", tail: tail(r.err || r.out) });
      if (r.code !== 0) return yield* failed("artifact_check.mjs", r);
      return { detail: lastLine(r.out) };
    }),
  },
];

export const stepDef = (name: StepName): StepDef => STEP_DEFS.find((d) => d.name === name)!;
