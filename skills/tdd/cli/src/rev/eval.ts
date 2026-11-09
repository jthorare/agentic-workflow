// The eval step's one boundary. Today the eval is eval_packet.py, run as a child process with the eval venv's Python
// (SKILL.md step 5) when the venv exists, else the step is recorded as not run with the reason. When lane N8's
// `tdd eval` (src/eval, bin/eval-packet.ts) merges, the swap is the one line at the bottom: `evalStep = bunEval`.
// Both take eval_packet.py's argv and print its table and summary line, so the step reads either the same way.
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import type { Part } from "./hash";
import type { Paths } from "./paths";
import { homeRel } from "./paths";

export interface EvalImpl {
  readonly name: string;
  /** The child's argv for `args` (eval_packet.py's flags after the packet), or why it cannot run on this machine. */
  readonly command: (p: Paths, args: readonly string[]) => { readonly cmd: readonly string[] } | { readonly notRun: string };
  /** The eval's own code, part of the eval step's fingerprint (so the swap below also moves --reuse and the guard). */
  readonly code: (p: Paths) => readonly Part[];
}

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

export const bunEval: EvalImpl = {
  name: "tdd eval (src/eval)",
  command: (p, args) => ({ cmd: [process.execPath, join(p.cli, "bin", "tdd.ts"), "eval", p.packet, ...args] }),
  code: (p) => [
    { tree: join(p.cli, "src", "eval") }, { file: join(p.cli, "bin", "eval-packet.ts") }, { tree: join(p.cli, "src", "lint") },
    { tree: join(p.cli, "src", "py") },
  ],
};

/** The eval the runner runs. Lane N8's merge changes this line to `bunEval`. */
export const evalStep: EvalImpl = pythonEval;
