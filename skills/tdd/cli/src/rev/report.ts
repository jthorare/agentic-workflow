// The final table (step · outcome · seconds · detail, with a failed step's stderr tail under it) and the same as JSON
// (plus each failed step's stderr_tail and the numbers a step reports, such as lint_errors).
import type { Report } from "./run";

const WIDTH = 110;

export function table(r: Report): string {
  const lines = [
    `tdd rev ${r.slug} · cycle ${r.cycle} · ${r.mode === "resume" ? `resumed at ${r.steps[0]}` : r.mode} · run ${r.run}`,
    `${"step".padEnd(16)}${"outcome".padEnd(13)}${"seconds".padStart(8)}  detail`,
    "-".repeat(WIDTH),
  ];
  for (const row of r.rows) {
    const secs = row.seconds === null ? "-" : row.ran ? row.seconds.toFixed(2) : `(${row.seconds.toFixed(2)})`;
    const detail = row.detail.split("\n")[0]!;
    lines.push(`${row.step.padEnd(16)}${row.outcome.padEnd(13)}${secs.padStart(8)}  ${detail.length > 72 ? detail.slice(0, 69) + "..." : detail}`);
    if (row.outcome === "failed") {
      if (detail.length > 72) lines.push(`${"".padEnd(39)}${detail}`);
      for (const t of (row.stderr_tail ?? "").split("\n").filter((l) => l.trim()).slice(-8)) lines.push(`${"".padEnd(37)}| ${t}`);
    }
  }
  lines.push("-".repeat(WIDTH));
  for (const n of r.notes) lines.push(`note: ${n}`);
  const ran = r.rows.filter((x) => x.ran);
  const count = (o: string) => ran.filter((x) => x.outcome === o).length;
  lines.push(`ran ${ran.length} · ok ${count("ok")} · skipped ${count("skipped")} · failed ${count("failed")} · exit ${r.exit} · journal build/.rev/journal.jsonl`);
  return lines.join("\n");
}

export function asJson(r: Report): string {
  return JSON.stringify({
    packet: r.packet, slug: r.slug, run: r.run, cycle: r.cycle, mode: r.mode, exit: r.exit, notes: r.notes,
    steps: r.rows.map((x) => ({
      step: x.step, outcome: x.outcome, seconds: x.seconds, ran: x.ran, detail: x.detail, run: x.run,
      ...(x.stderr_tail ? { stderr_tail: x.stderr_tail } : {}), ...(x.data ? { data: x.data } : {}),
    })),
  }, null, 1);
}
