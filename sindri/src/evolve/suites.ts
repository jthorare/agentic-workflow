import path from "node:path";

import type { Deps } from "../deps.js";
import type { ProcessRunner } from "../index/io.js";
import { withHeavyLock } from "../index/heavy-lock.js";
import { makeScrubber, type Scrubber } from "../scrub/scrub.js";

// A suite runs repo code, so it gets a clean environment: HOME and a few harmless variables only.
export function cleanEnvArgv(deps: Deps, argv: string[]): string[] {
  const kept = ["PATH", "TMPDIR", "LANG", "TERM"].flatMap((k) => (deps.env[k] === undefined ? [] : [`${k}=${deps.env[k]}`]));
  return ["env", "-i", `HOME=${deps.home}`, ...kept, ...argv];
}

// A module's eval suite is its existing tests (spec §7.7 table). Suites are heavy: each takes a
// slot of the box-wide heavy-job lock, and one suite never runs twice at once. They wait for a slot for at most 10 minutes.
export async function runSuite(
  deps: Deps, run: ProcessRunner, base: string, a: { id: string; suite: { argv: string[]; cwd: string } }, scrubber: Scrubber = makeScrubber(),
): Promise<{ ok: boolean; exitCode: number; ms: number; tail: string }> {
  return withHeavyLock(deps, `suite:${a.id}`, 600_000, async () => {
    const started = deps.now().getTime();
    const r = await run.run(cleanEnvArgv(deps, a.suite.argv), { cwd: path.join(base, a.suite.cwd), timeoutMs: 1_800_000 });
    const tail = scrubber.scrub(`${r.stdout}\n${r.stderr}`.trim().split("\n").slice(-20).join("\n")).text;
    return { ok: r.code === 0, exitCode: r.code, ms: deps.now().getTime() - started, tail };
  });
}
