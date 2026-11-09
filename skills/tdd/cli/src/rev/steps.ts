// The revision runner's steps, in SKILL.md's order (step 5, "Build and check": artifact_check before the eval, so the
// eval scores this run's screenshots; load-db after the eval, whose scores it loads), and the typed outcome each one
// ends with. A step is an Effect that succeeds with Done (ok) or fails with Skip (skipped, with the reason) or Fail (failed,
// with the error and the tail of its stderr); run.ts recovers both into an Outcome and journals it.
import { Data } from "effect";

export const STEPS = ["compile", "app-build", "hydrate", "agents", "lint", "artifact-check", "eval", "load-db", "push"] as const;
export type StepName = (typeof STEPS)[number];

const ALIASES: Record<string, StepName> = {
  build: "compile", app: "app-build", "check-hydrate": "hydrate", "write-agents": "agents", db: "load-db",
  "artifact": "artifact-check",
};

/** A step named on the command line: the canonical name, with `_` read as `-` and a few aliases; null when unknown. */
export function stepName(s: string): StepName | null {
  const k = s.toLowerCase().split("_").join("-");
  if ((STEPS as readonly string[]).includes(k)) return k as StepName;
  return ALIASES[k] ?? null;
}

export const stepIndex = (s: StepName): number => STEPS.indexOf(s);

/** A step that did not run, and why (a missing tool, an unreachable database, a flag not given, a reuse). */
export class Skip extends Data.TaggedError("Skip")<{ readonly reason: string }> {}
/** A step that ran and failed: what went wrong, and the last lines its tool wrote to stderr (or stdout). */
export class Fail extends Data.TaggedError("Fail")<{ readonly error: string; readonly tail: string }> {}
/** A step that ran and succeeded: a one-line summary (the tool's own summary line where it prints one). */
export interface Done {
  readonly detail: string;
  /** Numbers an agent may gate on (lint: errors, warnings), also in the journal, the state and --json. */
  readonly data?: Readonly<Record<string, number>>;
}

export type Outcome =
  | { readonly outcome: "ok"; readonly detail: string; readonly data?: Readonly<Record<string, number>> }
  | { readonly outcome: "skipped"; readonly reason: string }
  | { readonly outcome: "failed"; readonly error: string; readonly stderr_tail: string };

/** The last `n` non-empty lines of a tool's output, at most `max` characters (the journal keeps tails, not logs). */
export function tail(text: string, n = 15, max = 4000): string {
  const lines = text.split("\n").filter((l) => l.trim() !== "");
  const t = lines.slice(-n).join("\n");
  return t.length > max ? t.slice(t.length - max) : t;
}

/** The last non-empty line of a tool's output. */
export const lastLine = (text: string): string => tail(text, 1, 1000);
