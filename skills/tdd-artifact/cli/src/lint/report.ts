// The text report (lint_packet.report) and lint.json (lint_packet.main's json.dump, indent 1, ensure_ascii False).
import * as py from "../py";
import type { PyValue } from "../py/value";
import type { LintResult } from "./run";

const s = (n: number): string => (n === 1 ? "" : "s");

export interface Counts {
  readonly errors: number;
  readonly warnings: number;
}

export function counts(r: LintResult): Counts {
  const errors = r.findings.filter((f) => f.severity === "error").length;
  return { errors, warnings: r.findings.length - errors };
}

/** The report lint_packet prints on stdout, newline-terminated lines. */
export function report(r: LintResult): string {
  const { errors, warnings } = counts(r);
  const out: string[] = [];
  out.push(`lint_packet: ${r.root}`);
  out.push(`  build: ${r.compiled ? "(compiled to a temp folder)" : r.build}`);
  for (const rule of r.rules) {
    const fs = r.findings.filter((f) => f.rule === rule);
    if (!fs.length) continue;
    const e = fs.filter((f) => f.severity === "error").length;
    out.push(`\n${rule}  ${e} error${s(e)}, ${fs.length - e} warning${s(fs.length - e)}`);
    const ordered = [...fs.filter((f) => f.severity === "error"), ...fs.filter((f) => f.severity !== "error")];
    for (const f of ordered) {
      const where = (f.file ?? "") + (f.file && f.line ? `:${f.line}` : "");
      out.push(`  ${py.str.ljust(f.severity, 5)} ${py.str.ljust(f.check, 26)} ${where ? where + ": " : ""}${f.message}`);
    }
  }
  if (r.notes.length) {
    out.push("\nnotes:");
    for (const n of r.notes) out.push(`  - ${n}`);
  }
  out.push(`\n${errors} error${s(errors)}, ${warnings} warning${s(warnings)}`);
  return out.join("\n") + "\n";
}

/** lint.json: {packet, build, compiled, findings, notes, citations, counts{errors, warnings, by_rule}}. */
export function lintJson(r: LintResult): string {
  const { errors, warnings } = counts(r);
  const byRule = new Map<string, PyValue>(r.rules.map((rule) => [rule, r.findings.filter((f) => f.rule === rule).length]));
  const doc = new Map<string, PyValue>([
    ["packet", r.root],
    ["build", r.compiled ? null : r.build],
    ["compiled", r.compiled],
    ["findings", r.findings.map((f) => f.toPy())],
    ["notes", [...r.notes]],
    ["citations", [...r.citations]],
    ["counts", new Map<string, PyValue>([["errors", errors], ["warnings", warnings], ["by_rule", byRule]])],
  ]);
  return py.json.dumps(doc, { indent: 1, ensureAscii: false });
}

/** The exit code: 1 on any error, or on any warning with --strict; else 0. */
export const exitCode = (r: LintResult, strict: boolean): number => {
  const { errors, warnings } = counts(r);
  return errors || (strict && warnings) ? 1 : 0;
};
