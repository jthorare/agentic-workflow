// eval_packet.main as an Effect program. Capture the input; run the independent scorers side by side, each settled
// to a typed outcome under its own time limit (artifact_check, check_hydrate, lint, the judge, the probe's model call,
// the repo match, the Linear and AWS sources); assemble the six outputs in the Python's order; write input.json,
// probe.json, output.*.json, run.json and one history.jsonl line; push only with --push (a lazily imported module);
// print the table or the JSON summary. Exit 0 after a run, 1 when --push fails.
import { appendFileSync, mkdirSync, readdirSync } from "node:fs";
import { Effect, Layer } from "effect";
import * as py from "../py";
import { pyStr } from "../py/repr";
import * as A from "./artifact";
import * as B from "./bugs";
import { type Input, capture } from "./capture";
import { type Py, SKILL, fixed, get, now, writeJson } from "./common";
import { type Keys, loadKeys } from "./config";
import * as I from "./impl";
import * as J from "./judge";
import type * as L from "./llm";
import * as K from "./packet";
import * as P from "./probe";
import { type Run, plan } from "./plan";
import { type Output, type ScoreRec, scorer, settle, unscored } from "./score";
import * as T from "./tokens";
import { Tools, ToolsLive } from "./tools";

const path = py.path;

export interface Opts {
  packet: string; build?: string; out?: string; push?: boolean; project?: string; experiment?: string; base?: string;
  with_judge?: boolean; with_agent_probe?: boolean; labels?: string; session?: string; run_tests?: boolean; no_gh?: boolean;
  aws_log_groups?: string; aws_alarm_prefix?: string; probe?: string; no_shots?: boolean; skip?: string; json?: boolean;
}

export interface Io { out: (s: string) => void; err: (s: string) => void }

/** Per-scorer time limits (seconds): above each tool's own subprocess limit, so the Python's record wins normally. */
export const LIMITS = { artifact_check: 660, check_hydrate: 330, lint: 660, judge: 900, probe_agent: 900, implementation: 1800, qa: I.QA_LIMIT, linear: 330, aws: 330 };
export type Limits = typeof LIMITS;

function short(s: ScoreRec): string {
  if (s.status !== "ok") return s.reason || "";
  const m = s.metrics ? (s.metrics instanceof Map ? [...s.metrics.entries()] : Object.entries(s.metrics)) : [];
  return m.filter(([k]) => !k.startsWith("exit") && !k.startsWith("seconds")).slice(0, 4).map(([k, v]) => `${k}=${pyStr(v)}`).join(", ");
}

export function table(outputs: Map<string, Output>): string {
  const lines = [`${py.str.ljust("output", 15)}${py.str.ljust("scorer", 26)}${py.str.rjust("value", 7)}  ${py.str.ljust("status", 8)} detail`, "-".repeat(110)];
  for (const [kind, out] of outputs)
    for (const s of out.scores) {
      const v = s.value === null ? "" : fixed(s.value, 3);
      lines.push(`${py.str.ljust(kind, 15)}${py.str.ljust(s.name, 26)}${py.str.rjust(v, 7)}  ${py.str.ljust(s.status, 8)} ${py.cpSlice(short(s), 0, 70)}`);
    }
  return lines.join("\n");
}

const sourceOutcome = (eff: Effect.Effect<Record<string, Py>>, limit: number) =>
  Effect.map(settle(eff, limit), (o) => (o._tag === "scored" ? o.value : o._tag === "not_run" ? { status: "not run", reason: o.reason } : { status: "error", reason: o.error }));

export const program = (o: Opts, keys: Keys, io: Io, limits: Limits = LIMITS) => Effect.gen(function* () {
  const packet = o.packet;
  const build = path.abspath(o.build || path.join(packet, "build"));
  const out = path.abspath(o.out || path.join(build, "eval"));
  mkdirSync(out, { recursive: true });
  const defLabels = path.join(packet, "eval", "labels.jsonl");
  const labels = o.labels || (py.io.isfile(defLabels) ? defLabels : null);
  const defProbe = path.join(packet, "eval", "probe-questions.json");
  const probePath = o.probe || (py.io.isfile(defProbe) ? defProbe : path.join(SKILL, "templates", "eval", "probe-questions.json"));
  const [inp, content, meta] = capture(packet, build, labels);
  writeJson(path.join(out, "input.json"), inp);
  const skip = new Set((o.skip || "").split(",").map((s) => py.str.strip(s)).filter((s) => s));
  const page = path.join(build, "tdd.html");
  const sessions = o.session ? o.session.split(",") : [];
  const ownerReview = A.sOwnerReview;
  const probe = P.answerablePart(content, build, probePath);
  const { linear, aws } = B.sources(inp, content, keys, { skip, awsLogGroups: o.aws_log_groups ?? null, awsAlarmPrefix: o.aws_alarm_prefix ?? null });

  // the independent scorers, side by side; each settles to a typed outcome and never fails the run
  const [ac, hy, lint, judged, agent, matched, lin, awsRes] = yield* Effect.all([
    scorer(A.AC, A.sArtifactCheck(page, out, !!o.no_shots, skip), limits.artifact_check),
    scorer(A.HY, A.sHydrate(page, skip), limits.check_hydrate),
    scorer(A.LINT, A.sLint(packet, build, out, skip), limits.lint),
    Effect.map(settle(J.judge(build, content, !!o.with_judge, keys), limits.judge), (r) => r._tag === "scored" ? r.value
      : { scores: J.NAMES.map((name) => unscored({ name, method: J.METHOD, source: "judge" }, r)), ledger: [] as L.LedgerRow[] }),
    Effect.map(settle(P.agentPart(probe.qs, probe.lane, !!o.with_agent_probe, keys), limits.probe_agent), (r) => r._tag === "scored" ? r.value
      : { score: unscored(P.AGENT_SPEC, r), ledger: [] as L.LedgerRow[] }),
    skip.has("implementation") ? Effect.succeed(I.skipped("skipped (--skip implementation)"))
      : Effect.map(settle(I.matchPrs(content, meta, inp.packet.slug, !!o.no_gh), limits.implementation),
        (r) => (r._tag === "scored" ? r.value : I.skipped(r._tag === "not_run" ? r.reason : r.error))),
    sourceOutcome(linear, limits.linear),
    sourceOutcome(aws, limits.aws),
  ], { concurrency: "unbounded" });

  const outputs = new Map<string, Output>();
  outputs.set("artifact", A.assemble(page, content, inp, { ac, hy, lint }, judged.scores));
  writeJson(path.join(out, "probe.json"), probe.rec);
  outputs.set("packet", K.evaluate(build, content, inp, [probe.answerable, agent.score], ownerReview));
  outputs.set("implementation", I.evaluateImpl(inp, meta, matched, ownerReview));
  outputs.set("qa", yield* I.evaluateQa(inp, matched, outputs.get("artifact")!, !!o.run_tests, ownerReview, limits.qa));
  outputs.set("bugs", B.evaluate(inp, lin, awsRes, ownerReview));
  outputs.set("tokens", T.evaluate(inp, build, meta, sessions, [...judged.ledger, ...agent.ledger], outputs.get("packet")!));
  for (const [kind, data] of outputs) writeJson(path.join(out, `output.${kind}.json`), data);

  const rev = get(meta, "rev");
  const stem = o.experiment || `${inp.packet.slug}-rev${pyStr(rev)}`;
  const run: Run = plan({ input: inp, meta, page, out, packet, build }, outputs, o.project || "tdd-skill", stem, o.base ?? null);
  run.pushed = false;
  writeJson(path.join(out, "run.json"), run);
  appendFileSync(path.join(out, "history.jsonl"), py.json.dumps(historyLine(inp, outputs), { ensureAscii: false }) + "\n");

  let pushed: Record<string, Py> | null = null;
  if (o.push) {
    const U = yield* Effect.promise(() => import("./push"));
    const res = yield* Effect.result(U.push(run, keys));
    if (res._tag === "Failure") {
      const e = res.failure;
      io.err(e._tag === "PushExit" ? e.message : `push failed: ${e.type}: ${e.message}`);
      return 1;
    }
    pushed = res.success;
    writeJson(path.join(out, "push.json"), pushed);
  }
  const files = py.sorted(readdirSync(out).filter((f) => f.endsWith(".json")));
  if (o.json) {
    const summary = new Map([...outputs].map(([k, v]) => [k, new Map(v.scores.map((s) => [s.name,
      { value: s.value, status: s.status, ...(s.reason ? { reason: s.reason } : {}) }]))]));
    io.out(py.json.dumps({ out, files, packet: inp.packet.key, summary, experiments: run.experiments.map((e) => e.name), pushed }, { indent: 1 }));
  } else {
    io.out(`eval ${inp.packet.key} -> ${out}`);
    io.out(table(outputs));
    const tot = { ok: 0, metric_only: 0, not_run: 0, error: 0 };
    for (const v of outputs.values()) for (const c of Object.keys(tot) as (keyof typeof tot)[]) tot[c] += v.counts[c];
    io.out("-".repeat(110));
    io.out(`scored ${tot.ok} · metrics ${tot.metric_only} · not run ${tot.not_run} · errors ${tot.error}  | files: ${files.join(", ")}`);
    const tok = outputs.get("tokens")!;
    io.out(pyStr(tok.data?.cost_line ?? ""));
    if (pushed) {
      io.out(`dataset: ${pyStr(pushed.dataset_url)}`);
      for (const e of pushed.experiments) io.out(`experiment ${e.name}: ${pyStr(e.url)} (compared to ${py.truthy(e.compared_to) ? e.compared_to : "none"})`);
    } else io.out(`not pushed: run.json holds the dataset record and ${run.experiments.length} experiments a --push would send`);
  }
  return 0;
});

function historyLine(inp: Input, outputs: Map<string, Output>): Record<string, Py> {
  return {
    at: now(), packet: inp.packet.key, digest: inp.digest, content_generated: inp.content ? inp.content.generated : null,
    scores: new Map([...outputs].map(([k, v]) => [k, new Map(v.scores.filter((s) => s.value !== null).map((s) => [s.name, s.value]))])),
    not_run: new Map([...outputs].map(([k, v]) => [k, py.sorted(v.scores.filter((s) => s.status !== "ok").map((s) => s.name))])),
  };
}

/** Run the program with the keys from the environment and the live tools (or the layer a test provides). */
export function runEval(o: Opts, io: Io, env: Record<string, string | undefined> = process.env, tools: Layer.Layer<Tools> = ToolsLive,
  limits: Limits = LIMITS): Promise<number> {
  const main = Effect.gen(function* () {
    const keys = yield* Effect.orDie(loadKeys(env));
    return yield* program(o, keys, io, limits);
  });
  return Effect.runPromise(Effect.provide(main, tools));
}
