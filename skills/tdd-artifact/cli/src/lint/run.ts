// Run the rules in RULES order against one packet. Each rule is an Effect that needs the Packet and whichever services
// it reads; a rule that dies (a defect, a thrown error) becomes a RULE/check-failed finding and the run goes on
// (rule isolation). The packet's temporary compile folder lives in the run's scope and is removed when it closes.
import { Cause, Effect, Layer } from "effect";
import { Finding } from "./finding";
import { Packet, makePacket, type LintOptions, type LintPacket } from "./packet";
import { RULES, type RuleId } from "./patterns";
import { mirrorsPython, pyException, type PyDict } from "./pyops";
import { agents } from "./rules/agents";
import { cites } from "./rules/cites";
import { diag } from "./rules/diag";
import { dnum } from "./rules/dnum";
import { files } from "./rules/files";
import { meta } from "./rules/meta";
import { refs } from "./rules/refs";
import { schema } from "./rules/schema";
import { struct } from "./rules/struct";
import { xdoc } from "./rules/xdoc";
import { TsCompiler, TsCompilerLive, parity } from "./rules/parity";
import { Agents, Clock, Compiler, Fs, Git, ServicesLive } from "./services";

export type Services = Fs | Git | Clock | Compiler | Agents | TsCompiler;
export type Rule = Effect.Effect<void, never, Packet | Services>;

export const RULE_EFFECTS: Record<(typeof RULES)[number], Rule> = {
  STRUCT: struct, DNUM: dnum, REFS: refs, CITES: cites, DIAG: diag, SCHEMA: schema, FILES: files, META: meta, AGENTS: agents, XDOC: xdoc,
  PARITY: parity,
};

/** What a lint run leaves: the packet's state with its findings, notes and citations. */
export interface LintResult {
  readonly root: string;
  readonly build: string;
  readonly compiled: boolean;
  readonly findings: readonly Finding[];
  readonly notes: readonly string[];
  readonly citations: readonly PyDict[];
  readonly rules: readonly RuleId[];
}

/**
 * Rule isolation. An exception lint_packet.py raises too (a kernel PyError or a file-system error: the same input makes
 * the Python rule raise at the same place) ends the run as it ends lint_packet.py's: no report, no lint.json, the
 * exception on stderr, exit 1. Any other exception is a defect of this linter: RULE/check-failed (error, never filtered
 * by --skip RULE/check-failed), and the other rules run.
 */
export const isolate = (id: RuleId, rule: Rule): Rule =>
  rule.pipe(
    Effect.catchCause((cause) => {
      const e = Cause.squash(cause);
      if (mirrorsPython(e)) return Effect.failCause(cause);
      return Effect.gen(function* () {
        const p = yield* Packet;
        const [name, msg] = pyException(e);
        p.defect(id, `the ${id} rule raised ${name}: ${msg}; the other rules ran (a linter defect: please report it)`);
      });
    }),
  );

/** lint_packet's Lint(packet, build, compile, skip).run(): the rules not skipped, in order, isolated. */
export const lintEffect = (opts: LintOptions, rules: readonly RuleId[] = RULES, table: Partial<Record<RuleId, Rule>> = RULE_EFFECTS) =>
  Effect.scoped(
    Effect.gen(function* () {
      const p: LintPacket = yield* makePacket(opts);
      const ctx = Effect.provideService(Packet, p);
      for (const id of rules) {
        if (p.skip.has(id)) continue;
        const rule = table[id];
        if (rule) yield* ctx(isolate(id, rule));
      }
      const out: LintResult = {
        root: p.root, build: p.build, compiled: p.compiled, findings: p.findings, notes: p.notes, citations: p.citations, rules,
      };
      return out;
    }),
  );

/** Run a lint on the live services. */
export const AllLive: Layer.Layer<Services> = Layer.mergeAll(ServicesLive, TsCompilerLive);

export const runLint = (opts: LintOptions, layer: Layer.Layer<Services> = AllLive): Promise<LintResult> =>
  Effect.runPromise(Effect.provide(lintEffect(opts), layer));
