// The eval step's one boundary. The eval is `tdd eval` (src/eval, bin/eval-packet.ts), run as a child process of this
// bun with eval_packet.py's argv; it prints the same table and summary line, so the step reads it as it read the
// Python. It runs as a child, not in-process, so that its own children (git, gh, node artifact_check, bun
// check_hydrate, python lint_packet.py) inherit the environment the runner hands it, without the keys: src/eval spawns
// them with Bun.spawn and no `env`, which in this process would be the environment it started with, keys included.
// eval_packet.py in the eval venv (SKILL.md step 5) stays the fallback, taken only when src/eval lacks what this run
// needs: bin/eval-packet.ts is absent, or a --push finds no `braintrust` package next to src/eval while the venv
// exists (the venv has its Python SDK). `evalStep` picks one per run.
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { python as evalLintPython } from "../eval/artifact";
import type { Part } from "./hash";
import type { Paths } from "./paths";
import { homeRel } from "./paths";

export interface EvalImpl {
  readonly name: string;
  /** The child's argv for `args` (eval_packet.py's flags after the packet), or why it cannot run on this machine. */
  readonly command: (p: Paths, args: readonly string[]) => { readonly cmd: readonly string[] } | { readonly notRun: string };
  /** The eval's own code, part of the eval step's fingerprint (so a swap also moves --reuse and the guard). */
  readonly code: (p: Paths) => readonly Part[];
}

/** The bun that runs `tdd eval`, app/build.ts, check_hydrate.ts and tdd load-db: this one ($TDD_REV_BUN overrides it,
 *  for tests). */
export const revBun = (): string => process.env["TDD_REV_BUN"] || process.execPath;

/** The eval venv's Python ($TDD_EVAL_PYTHON overrides it, for tests). */
export const evalPython = (): string =>
  process.env["TDD_EVAL_PYTHON"] || join(homedir(), ".local", "share", "tdd-skill", "venv", "bin", "python");

export const pythonEval: EvalImpl = {
  name: "scripts/eval_packet.py (the eval venv)",
  command: (p, args) => {
    const py = evalPython();
    if (!existsSync(py)) return { notRun: `not run: the eval venv is absent (${homeRel(py)}); create it as SKILL.md step 5 says` };
    const script = join(p.skill, "scripts", "eval_packet.py");
    if (!existsSync(script)) return { notRun: "not run: scripts/eval_packet.py is absent" };
    return { cmd: [py, "-I", script, p.packet, ...args] };
  },
  code: (p) => [
    { tree: join(p.skill, "scripts", "eval") }, { file: join(p.skill, "scripts", "eval_packet.py") },
    { file: join(p.skill, "scripts", "lint_packet.py") }, { label: "python", value: evalPython() },
  ],
};

const tsEntry = (p: Paths): string => join(p.cli, "bin", "eval-packet.ts");

export const bunEval: EvalImpl = {
  name: "tdd eval (src/eval)",
  command: (p, args) => existsSync(tsEntry(p))
    ? { cmd: [revBun(), join(p.cli, "bin", "tdd.ts"), "eval", p.packet, ...args] }
    : { notRun: "not run: bin/eval-packet.ts is absent" },
  code: (p) => [
    { tree: join(p.cli, "src", "eval") }, { file: tsEntry(p) }, { tree: join(p.cli, "src", "py") },
    // its lint scorer runs lint_packet.py with the interpreter the Python eval would use
    { file: join(p.skill, "scripts", "lint_packet.py") }, { label: "python", value: evalLintPython() },
  ],
};

/** Whether `spec` resolves from the cli folder (src/eval imports its optional SDKs from there). */
const resolves = (spec: string, from: string): boolean => {
  try {
    Bun.resolveSync(spec, from);
    return true;
  } catch {
    return false;
  }
};

/** The eval this run takes (`push`: the eval will push): `tdd eval`, else the Python fallback, named with its reason. */
export function evalStep(p: Paths, push: boolean): EvalImpl {
  const venv = existsSync(evalPython());
  if (!existsSync(tsEntry(p)) && venv) return { ...pythonEval, name: `${pythonEval.name}; bin/eval-packet.ts is absent` };
  if (push && venv && !resolves("braintrust", p.cli)) {
    return { ...pythonEval, name: `${pythonEval.name}; the push needs the braintrust package, absent for tdd eval` };
  }
  return bunEval;
}
