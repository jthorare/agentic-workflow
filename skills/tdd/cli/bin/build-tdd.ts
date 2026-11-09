#!/usr/bin/env bun
// tdd build: build_tdd.py in TypeScript, with the Python's argv, usage text, stdout line, stderr warnings and exit codes
// (0; 1 on bad input through sys.exit, or on any warning with --strict). Usage: the DOC in src/compile/args.ts.
// An exception CPython would let escape ends as its traceback does: the last line "<type>: <message>", exit 1, never a
// bun stack dump (set TDD_DEBUG=1 to see the JavaScript stack as well).
import { PyOSError, compile, isSystemExit, osError, parse_args } from "../src/compile";
import { PyError, qualifiedName } from "../src/py/errors";

function traceback(e: unknown): string {
  if (e instanceof PyError) return `${qualifiedName(e.type)}: ${e.message}`;
  const os = e instanceof PyOSError ? e : osError(e);
  if (os) return `${os.type}: ${os.message}`;
  const t = (e as { type?: unknown } | null)?.type; // src/sd's SdError: struct.error, shutil.SameFileError, RecursionError
  if (e instanceof Error) return `${typeof t === "string" ? t : e.name}: ${e.message}`;
  return `Exception: ${String(e)}`;
}

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
    process.stderr.write(`Traceback (most recent call last):\n  (build-tdd.ts)\n${traceback(e)}\n`);
    if (process.env.TDD_DEBUG && e instanceof Error && e.stack) process.stderr.write(`${e.stack}\n`);
    return 1;
  }
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
