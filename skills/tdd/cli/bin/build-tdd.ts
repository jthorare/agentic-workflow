#!/usr/bin/env bun
// tdd build: build_tdd.py in TypeScript, with the Python's argv, usage text, stdout line, stderr warnings and exit codes
// (0; 1 on bad input through sys.exit, or on any warning with --strict). Usage: the DOC in src/compile/args.ts.
import { compile, isSystemExit, parse_args } from "../src/compile";
import { PyError, qualifiedName } from "../src/py/errors";

export async function main(argv: string[]): Promise<number> {
  try {
    const a = parse_args(argv);
    const r = compile(a);
    for (const w of r.warnings) process.stderr.write(`warning: ${w}\n`);
    process.stdout.write(`${r.line}\n`);
    if (a.strict && r.warnings.length) {
      process.stderr.write(`--strict: ${r.warnings.length} warning(s)\n`);
      return 1;
    }
    return 0;
  } catch (e) {
    if (isSystemExit(e)) {
      if (e.message) process.stderr.write(`${e.message}\n`);
      return typeof e.code === "number" ? e.code : 1;
    }
    // an uncaught Python exception: CPython prints a traceback whose last line is "<type>: <message>", exit 1
    if (e instanceof PyError) {
      process.stderr.write(`Traceback (most recent call last):\n  (build-tdd.ts)\n${qualifiedName(e.type)}: ${e.message}\n`);
      return 1;
    }
    throw e;
  }
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
