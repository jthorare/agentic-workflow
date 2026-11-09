#!/usr/bin/env bun
// tdd lint: lint_packet.py in TypeScript (src/lint). Same argv, usage text, report, lint.json and exit codes:
// 0 clean or warnings only, 1 on any error (or any warning with --strict), 2 on bad usage.
import * as py from "../src/py";
import { LINT_PACKET_DOC as DOC } from "../src/lint/doc";
import { pyException } from "../src/lint/pyops";
import { PyError, qualifiedName } from "../src/py/errors";
import { exitCode, lintJson, report } from "../src/lint/report";
import { runLint, type Services } from "../src/lint/run";
import type { Layer } from "effect";

export interface LintArgs {
  packet: string;
  build: string | null;
  json: string | null;
  skip: string[];
  strict: boolean;
  compile: boolean;
}

export type Parsed = { args: LintArgs } | { code: number; out: string; err: string };

/** lint_packet.main's argument loop: the same messages, streams and exit codes. */
export function parseArgs(argv: readonly string[]): Parsed {
  const args: string[] = [];
  const opts = { build: "", json: "", skip: "", strict: false, compile: false };
  let i = 0;
  while (i < argv.length) {
    const x = argv[i]!;
    if (x === "--build" || x === "--json" || x === "--skip") {
      if (i + 1 >= argv.length) return { code: 2, out: "", err: `${x} needs a value\n\n${DOC}\n` };
      opts[x.slice(2) as "build" | "json" | "skip"] = argv[i + 1]!;
      i += 2;
    } else if (x === "--strict" || x === "--compile") {
      opts[x.slice(2) as "strict" | "compile"] = true;
      i += 1;
    } else if (x === "-h" || x === "--help") {
      return { code: 0, out: DOC + "\n", err: "" };
    } else if (x.startsWith("--")) {
      return { code: 2, out: "", err: `unknown option ${x}\n\n${DOC}\n` };
    } else {
      args.push(x);
      i += 1;
    }
  }
  if (args.length !== 1 || !py.io.isdir(py.path.expanduser(args[0]!))) {
    return { code: 2, out: "", err: (args.length !== 1 ? DOC : `${args[0]}: not a directory`) + "\n" };
  }
  return {
    args: {
      packet: args[0]!, build: opts.build || null, json: opts.json || null, strict: opts.strict, compile: opts.compile,
      skip: py.str.split(opts.skip, ",").filter((s) => py.str.strip(s)),
    },
  };
}

/** Run the linter: [exit code, stdout, stderr]; writes lint.json when --json is given. `layer` replaces the live
 *  services (tdd rev passes one whose git runs without the environment's keys). */
export async function lintMain(argv: readonly string[], layer?: Layer.Layer<Services>): Promise<{ code: number; out: string; err: string }> {
  const parsed = parseArgs(argv);
  if ("code" in parsed) return parsed;
  const a = parsed.args;
  let out = "";
  try {
    const r = await runLint({ packet: a.packet, build: a.build, compile: a.compile, skip: a.skip }, layer);
    out = report(r);
    if (a.json) py.io.writeText(a.json, lintJson(r));
    return { code: exitCode(r, a.strict), out, err: "" };
  } catch (e) {
    // the run ended as lint_packet.py's does on the same input: its traceback's last line, exit 1
    const [name, msg] = pyException(e);
    const shown = e instanceof PyError ? qualifiedName(e.type) : name;
    return { code: 1, out, err: `Traceback (most recent call last):\n  (tdd lint)\n${shown}: ${msg}\n` };
  }
}

export async function main(argv: string[]): Promise<number> {
  const r = await lintMain(argv);
  if (r.out) await Bun.write(Bun.stdout, r.out);
  if (r.err) await Bun.write(Bun.stderr, r.err);
  return r.code;
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
