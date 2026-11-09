// The Finding schema (research/bun-port-2026.md section 4.2): one lint result, in lint.json's key order
// {rule, check, severity, file, line, message}. A Finding is validated when it is made; `toPy` gives the ordered dict
// the JSON report dumps, so the key order is the schema's and never an accident of object construction.
import { Schema } from "effect";
import type { PyDict, PyValue } from "./pyops";

export const Severity = Schema.Literals(["error", "warn"]);
export type Severity = typeof Severity.Type;

export class Finding extends Schema.Class<Finding>("Finding")({
  /** The rule id (STRUCT, DNUM, ...). */
  rule: Schema.String,
  /** RULE/check, e.g. CITES/no-line. */
  check: Schema.String,
  severity: Severity,
  /** Relative to the packet (or ~-relative, or absolute), null when the finding has no place. */
  file: Schema.NullOr(Schema.String),
  /** 1-based, null when unknown. */
  line: Schema.NullOr(Schema.Number),
  /** One line: whitespace runs collapsed to one space. */
  message: Schema.String,
}) {
  /** The ordered dict lint.json holds for this finding. */
  toPy(): PyDict {
    return new Map<string, PyValue>([
      ["rule", this.rule], ["check", this.check], ["severity", this.severity],
      ["file", this.file], ["line", this.line], ["message", this.message],
    ]);
  }
}

/** A citation record (lint.json `citations[]`): how each distinct path:line resolved. */
export type Resolved = "sha" | "head" | "working_tree" | "packet" | "missing";
