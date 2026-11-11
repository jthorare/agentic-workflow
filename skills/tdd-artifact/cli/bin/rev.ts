#!/usr/bin/env bun
// tdd rev: SKILL.md's build steps for one packet as one program (compile, app build, hydrate check, agents, lint,
// eval, load-db, push only with --push, artifact_check), journaled under build/.rev/ so a stopped run resumes where it
// stopped. Usage: src/rev/args.ts (USAGE). Exit 0 when every step that ran is ok or skipped, 1 when one failed or the
// build is locked by another run, 2 on bad usage, 130 / 143 when interrupted.
import { statSync } from "node:fs";
import { Cause, Effect, Exit } from "effect";
import { asJson, liveLayer, parseArgs, Refused, resolvePaths, revProgram, signalExit, table, USAGE, type Interrupt } from "../src/rev";

export async function main(argv: string[]): Promise<number> {
  const a = parseArgs(argv);
  if (a === "help") {
    process.stdout.write(USAGE + "\n");
    return 0;
  }
  if ("error" in a) {
    process.stderr.write(`tdd rev: ${a.error}\n${USAGE}\n`);
    return 2;
  }
  let isDir = false;
  try { isDir = statSync(a.packet).isDirectory(); } catch { isDir = false; }
  if (!isDir) {
    process.stderr.write(`tdd rev: ${a.packet}: not a packet folder\n`);
    return 2;
  }
  const interrupt: Interrupt = { by: null };
  const ac = new AbortController();
  const handler = (sig: NodeJS.Signals) => () => {
    if (interrupt.by) return;
    interrupt.by = sig;
    ac.abort();
  };
  const onTerm = handler("SIGTERM"), onInt = handler("SIGINT");
  process.on("SIGTERM", onTerm);
  process.on("SIGINT", onInt);
  try {
    const program = revProgram(interrupt).pipe(Effect.provide(liveLayer(resolvePaths(a.packet), a)));
    const exit = await Effect.runPromiseExit(program, { signal: ac.signal });
    if (Exit.isSuccess(exit)) {
      process.stdout.write((a.json ? asJson(exit.value) : table(exit.value)) + "\n");
      return exit.value.exit;
    }
    if (Cause.hasInterruptsOnly(exit.cause)) {
      process.stderr.write(`tdd rev: interrupted by ${interrupt.by ?? "an interrupt"}; build/.rev/state.json records the step to resume at\n`);
      return signalExit(interrupt.by);
    }
    const e = Cause.squash(exit.cause);
    process.stderr.write(e instanceof Refused ? `tdd rev: ${e.message}\n` : `tdd rev: ${Cause.pretty(exit.cause)}\n`);
    return 1;
  } finally {
    process.off("SIGTERM", onTerm);
    process.off("SIGINT", onInt);
  }
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
