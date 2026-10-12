import { z } from "zod";

import { parseFlags } from "../../args.js";
import { awStateDir } from "../../deps.js";
import { SindriError } from "../../errors.js";
import { heavyLockState } from "../../index/heavy-lock.js";
import { success, type CommandResult, type ExitCode } from "../../output.js";
import { check, dirtyOf } from "./check.js";
import { correctCommand } from "./correct.js";
import { Budget } from "../../scope/model.js";
import { reflectCommand, reflectedBefore } from "./reflect.js";
import { stageReport } from "./stage.js";
import { stageProposals } from "../stage.js";
import { terminalSafe } from "../invisible.js";
import { telemetry } from "./telemetry.js";
import { repoConfig, type EvolveCtx } from "../ctx.js";
import { allowedAuthors, ghJson, ghRepoOf } from "../github.js";

const PrList = z.array(z.object({ number: z.number().int().positive(), author: z.object({ login: z.string() }) }));

const messageOf = (e: unknown): string => (e instanceof SindriError ? `${e.code} ${e.message}` : e instanceof Error ? e.message : String(e));

// The last line of a step's output that isn't a "Next:" pointer. Steps throw on error, so stdout is all there is.
function summaryOf(r: CommandResult): string {
  const lines = r.stdout.split("\n").filter((l) => l !== "" && !l.startsWith("Next:"));
  return lines[lines.length - 1];
}

type StepResult = { exitCode: ExitCode; line: string };
const fromCommand = (r: CommandResult): StepResult => ({ exitCode: r.exitCode, line: summaryOf(r) });

// Stage's digest line comes from its structured outcome (the held count included), not from its printed text. A held
// proposal (one at evolve.maxGeneralizeAttempts included: `held` lists those too, `gaveUp` names them) needs a person,
// so the step is `attn` and counts under "need attention".
async function stageStep(ctx: EvolveCtx): Promise<StepResult> {
  const o = await stageProposals(ctx);
  return { exitCode: o.held.length > 0 ? 1 : 0, line: terminalSafe(stageReport(o).summary) };
}

const WINDOW_DAYS = 14; // the marker dedupes, so a wider window only costs one list call and survives a missed week
const MAX_PRS_PER_RUN = 10;

async function mergedUnreflected(ctx: EvolveCtx): Promise<{ todo: number[]; skipped: number }> {
  const ghRepo = await ghRepoOf(ctx.deps.git, ctx.repo);
  const since = new Date(ctx.deps.now().getTime() - WINDOW_DAYS * 86_400_000).toISOString().slice(0, 10);
  const list = await ghJson(ctx.io.process, ["gh", "pr", "list", "--state", "merged", "--search", `merged:>=${since}`, "--base", repoConfig(ctx.loaded).defaultBranch, "--json", "number,author", "--limit", "100", "--repo", ghRepo], ctx.repo, PrList);
  const fresh = list.filter((p) => !reflectedBefore(ctx, p.number).reflected);
  if (fresh.length === 0) return { todo: [], skipped: 0 };
  const allowed = await allowedAuthors(ctx.io.process, ctx.repo, ctx.loaded.profile.evolve.prAuthors);
  const mine = fresh.filter((p) => allowed.includes(p.author.login));
  return { todo: mine.map((p) => p.number).sort((a, b) => a - b), skipped: fresh.length - mine.length };
}

const STATE: Record<number, "ok" | "attn" | "FAIL"> = { 0: "ok", 1: "attn", 2: "FAIL" };

const firstLine = (s: string): string => s.split("\n")[0];

async function reflectStep(ctx: EvolveCtx): Promise<CommandResult> {
  const { todo, skipped } = await mergedUnreflected(ctx);
  const skippedNote = skipped > 0 ? `${skipped} skipped (author)` : "";
  if (todo.length === 0) return success(`no merged PRs from the last ${WINDOW_DAYS} days need a reflection${skipped > 0 ? ` (${skippedNote})` : ""}`, {}, false);
  const budget = new Budget(ctx.loaded.profile.evolve.maxTokensPerJob); // one budget for the whole step
  const parts: string[] = [];
  let worst: ExitCode = 0;
  let done = 0;
  for (const n of todo.slice(0, MAX_PRS_PER_RUN)) {
    if (budget.exhausted()) break;
    done++;
    let code: ExitCode;
    let why = "";
    try {
      const r = await reflectCommand(["--pr", String(n)], ctx, budget);
      code = r.exitCode;
      why = firstLine(r.stdout.split("\n").find((l) => l.startsWith("Partial result:")) ?? "");
    } catch (e) {
      code = 2;
      why = firstLine(messageOf(e));
    }
    parts.push(`#${n} ${STATE[code]}${code === 0 ? "" : ` (${why})`}`);
    worst = Math.max(worst, code) as ExitCode;
  }
  if (skipped > 0) parts.push(skippedNote);
  if (todo.length > done) parts.push(`${todo.length - done} PR(s) left for next week`);
  return { exitCode: worst, stdout: `reflected on ${done} merged PR(s): ${parts.join(", ")}`, stderr: "" };
}

async function checkStep(ctx: EvolveCtx): Promise<CommandResult> {
  if (await dirtyOf(ctx)) return success("skipped: the working tree has uncommitted changes, so suites wouldn't match a commit", {}, false, 1);
  return check(["--changed"], ctx);
}

export async function weekly(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { "dry-run": { type: "boolean" }, json: { type: "boolean" } });
  const json = values.json === true;
  if (heavyLockState(awStateDir(ctx.deps), ctx.deps.now, ctx.deps.env).every((s) => s.held)) {
    return success("Skipped: heavy jobs hold every slot of the box-wide lock. Nothing ran.\nNext: rerun sindri evolve weekly later", { skipped: true }, json, 1);
  }
  if (values["dry-run"] === true) {
    let reflectPlan: string;
    let prs: number[] = [];
    try {
      prs = (await mergedUnreflected(ctx)).todo;
      reflectPlan = `reflect on ${prs.length} merged PR(s)${prs.length > 0 ? ` (${prs.map((n) => `#${n}`).join(", ")})` : ""}`;
    } catch (e) {
      reflectPlan = `reflect: couldn't list merged PRs (${messageOf(e)})`;
    }
    return success(`Weekly plan (dry run): telemetry --since 7d; ${reflectPlan}; correct --since 7d; check --changed; stage. Nothing ran.\nNext: sindri evolve weekly`, { dryRun: true, prs }, json);
  }
  const steps: { name: string; run: () => Promise<StepResult> }[] = [
    { name: "telemetry", run: async () => fromCommand(await telemetry(["--since", "7d"], ctx)) },
    { name: "reflect", run: async () => fromCommand(await reflectStep(ctx)) },
    { name: "correct", run: async () => fromCommand(await correctCommand(["--since", "7d"], ctx)) },
    { name: "check", run: async () => fromCommand(await checkStep(ctx)) },
    { name: "stage", run: () => stageStep(ctx) },
  ];
  const results: { name: string; state: "ok" | "attn" | "FAIL"; line: string }[] = [];
  for (const s of steps) {
    try {
      const r = await s.run();
      results.push({ name: s.name, state: STATE[r.exitCode], line: r.line });
    } catch (e) {
      results.push({ name: s.name, state: "FAIL", line: messageOf(e) });
    }
  }
  const ok = results.filter((r) => r.state === "ok").length;
  const staged = (ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals WHERE status = 'staged'").get() as { c: number }).c;
  const next = ok < results.length ? "fix the lines above, then rerun sindri evolve weekly (or just the failing step)" : staged > 0 ? "sindri evolve publish" : "sindri evolve proposals";
  const lines = [...results.map((r) => `${r.state.padEnd(4)} ${r.name}: ${r.line}`), `Weekly: ${results.length} steps, ${ok} ok, ${results.length - ok} need attention.`, `Next: ${next}`];
  return success(lines.join("\n"), { steps: results, ok, staged }, json, ok === results.length ? 0 : 1);
}
