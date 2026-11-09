// UPSTREAM.json pins the system-design files src/sd reimplements. When one changes, this fails and says which ported
// ranges moved, so the port is re-checked (test/sd oracle suites) before the new hashes are written. No Python needed.
import { describe, expect, test } from "bun:test";
import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { CLI, UPSTREAM, VENDOR } from "./harness";

const sha = (b: Uint8Array | string) => createHash("sha256").update(b).digest("hex");

/** The ported ranges whose text no longer hashes as pinned (or that a line shift moved). */
function moved(file: string, raw: Buffer): string[] {
  const lines = raw.toString("latin1").split("\n");
  const out: string[] = [];
  for (const r of UPSTREAM.files[file]!.ranges) {
    const [a, b] = r.lines;
    const text = Buffer.from(lines.slice(a - 1, b).join("\n") + "\n", "latin1");
    if (sha(text) !== r.sha256) out.push(`lines ${a}-${b} (${r.names.join(", ")})`);
  }
  return out;
}

function check(dir: string): string[] {
  const problems: string[] = [];
  for (const [file, pin] of Object.entries(UPSTREAM.files)) {
    const p = join(dir, "scripts", file);
    if (!existsSync(p)) {
      problems.push(`${p}: missing`);
      continue;
    }
    const raw = readFileSync(p);
    if (sha(raw) === pin.sha256) continue;
    const m = moved(file, raw);
    problems.push(`${p}: sha256 ${sha(raw)} is not the pinned ${pin.sha256}; ` + (m.length
      ? `ported ranges changed: ${m.join("; ")}. Port the change in src/sd, re-run test/sd, then update UPSTREAM.json.`
      : "the ported ranges are unchanged (the edit is elsewhere): re-run test/sd and update UPSTREAM.json's file hash."));
  }
  return problems;
}

describe("UPSTREAM.json", () => {
  test("names the vendor copy and the 372 ported lines", () => {
    expect(UPSTREAM.source).toBe("skills/tdd/vendor/system-design/scripts");
    const total = Object.values(UPSTREAM.files).flatMap((f) => f.ranges).reduce((n, r) => n + r.lines[1] - r.lines[0] + 1, 0);
    expect(total).toBe(372);
    const names = Object.values(UPSTREAM.files).flatMap((f) => f.ranges.flatMap((r) => r.names));
    for (const n of ["MARK", "parse_blocks", "split_marker", "slug", "Inline", "strip_tags", "img_size", "Assets", "published_for",
      "STATUS_WORDS", "page_title", "load_json", "esc", "FILE_LIMIT", "front", "first_sentence", "inline", "markdown"]) expect(names).toContain(n);
  });

  test("the vendor build_doc.py and build_prs.py hash as pinned", () => {
    expect(check(VENDOR)).toEqual([]);
  });

  test("the copy build_tdd.py would load (its sd_root) hashes as pinned", () => {
    // build_tdd.sd_root(): $SYSTEM_DESIGN_SKILL, the sibling skills/system-design, ~/.claude/skills/system-design, vendor
    const cands = [process.env.SYSTEM_DESIGN_SKILL, join(CLI, "..", "..", "system-design"), join(homedir(), ".claude", "skills", "system-design"), VENDOR];
    const root = cands.find((c) => c && existsSync(join(c, "scripts", "build_doc.py")))!;
    expect(check(root)).toEqual([]);
  });

  test("a changed file is reported with the ranges that moved", () => {
    const raw = readFileSync(join(VENDOR, "scripts", "build_doc.py"));
    const edited = Buffer.from(raw.toString("latin1").replace(/def slug\(s\):/, "def slug(s):  # edited"), "latin1");
    expect(sha(edited)).not.toBe(UPSTREAM.files["build_doc.py"]!.sha256);
    expect(moved("build_doc.py", edited)).toEqual([expect.stringContaining("lines 193-356 (MARK")]);
  });
});
