#!/usr/bin/env bun
// tdd annotate: annotate_hotspots.py in TypeScript (src/hotspots/annotate.ts). Same argv, output and exit codes.
import { run } from "../src/hotspots/annotate";

export async function main(argv: string[]): Promise<number> {
  const r = run(argv);
  if (r.out) await Bun.write(Bun.stdout, r.out);
  if (r.err) await Bun.write(Bun.stderr, r.err);
  return r.code;
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
