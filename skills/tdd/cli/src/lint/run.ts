// Run the rules in RULES order against one packet. Each rule is an Effect that needs the Packet and whichever services
// it reads; a rule that dies (a defect, a thrown error) becomes a RULE/check-failed finding and the run goes on
// (rule isolation). The packet's temporary compile folder lives in the run's scope and is removed when it closes.
import { Cause, Effect, Layer } from "effect";
import { Finding } from "./finding";
import { Packet, makePacket, type LintOptions, type LintPacket } from "./packet";
import { RULES, type RuleId } from "./patterns";
import { pyException, type PyDict } from "./pyops";
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
import { Agents, Clock, Compiler, Fs, Git, ServicesLive } from "./services";

export type Services = Fs | Git | Clock | Compiler | Agents;
export type Rule = Effect.Effect<void, never, Packet | Services>;

export const RULE_EFFECTS: Record<(typeof RULES)[number], Rule> = {
  STRUCT: struct, DNUM: dnum, REFS: refs, CITES: cites, DIAG: diag, SCHEMA: schema, FILES: files, META: meta, AGENTS: agents, XDOC: xdoc,
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

/** A rule whose fiber dies reports RULE/check-failed (error) instead of ending the run. */
export const isolate = (id: RuleId, rule: Rule): Rule =>
  rule.pipe(
    Effect.catchCause((cause) =>
      Effect.gen(function* () {
        const p = yield* Packet;
        const [name, msg] = pyException(Cause.squash(cause));
        p.add(id, "check-failed", "error", `the ${id} rule raised ${name}: ${msg}; the other rules ran (a linter defect: please report it)`, null);
      }),
    ),
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
export const runLint = (opts: LintOptions, layer: Layer.Layer<Services> = ServicesLive): Promise<LintResult> =>
  Effect.runPromise(Effect.provide(lintEffect(opts), layer));
