// tdd rev: the revision runner (PR 16, research/bun-port-2026.md section 4.2). The program is run.ts; the live layer
// below wires it to the file system, child processes, the in-process tools and the environment.
import { Layer } from "effect";
import type { RevArgs } from "./args";
import { RevOpts, RevPaths } from "./context";
import { JournalLive } from "./journal";
import type { Paths } from "./paths";
import { SecretsLive } from "./secrets";
import { DbLive, outLive, ProcLive, ToolsLive } from "./services";

export { parseArgs, USAGE, type RevArgs } from "./args";
export { bunEval, evalPython, evalStep, pythonEval, revBun } from "./eval";
export { resolvePaths, type Paths } from "./paths";
export { asJson, table } from "./report";
export { plan, Refused, revProgram, signalExit, type Interrupt, type Report } from "./run";
export { STEPS, type StepName } from "./steps";

export function liveLayer(paths: Paths, args: RevArgs) {
  const base = Layer.mergeAll(Layer.succeed(RevPaths, paths), Layer.succeed(RevOpts, args), outLive(args.json), SecretsLive, DbLive);
  return Layer.mergeAll(ProcLive, ToolsLive, JournalLive).pipe(Layer.provideMerge(base));
}
