// tdd annotate vs annotate_hotspots.py, spawned as python3 -I and bun bin/tdd.ts on copies of a packet: the proposal
// table (stdout), exit codes, and the files --apply rewrites. On the two real packets: dry run, --key, --apply,
// --apply --min-score 0.5 on top, then from freshly regenerated hotspot files (every field empty) --apply and a dry
// rerun that shows the kept fields. A synthetic packet covers the branches the real ones miss (a token in the label,
// two tokens, a table drawer, duplicate anchor ids, frames over half the viewBox) and the usage / error paths.
import { describe, expect, test } from "bun:test";
import { mkdirSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { copyPacket, packets, py, rmrf, snapshot, tempDir, ts } from "./_harness";

function both(A: string, B: string, args: string[]) {
  const p = py("annotate_hotspots.py", args, A), t = ts("annotate", args, B);
  expect(t).toEqual(p);
  expect(snapshot(B)).toEqual(snapshot(A));
  return p;
}
/** A Python traceback and its TS stand-in agree on the exit code, stdout and the final exception line. */
function bothTraceback(A: string, B: string, args: string[], line: string) {
  const p = py("annotate_hotspots.py", args, A), t = ts("annotate", args, B);
  const last = (s: string) => s.trimEnd().split("\n").pop();
  expect([t.code, t.out, last(t.err)]).toEqual([p.code, p.out, last(p.err)]);
  expect(last(p.err)).toBe(line);
  expect(p.err.startsWith("Traceback (most recent call last):\n") && t.err.startsWith("Traceback (most recent call last):\n")).toBe(true);
}
function regenerate(dir: string, side: "py" | "ts") {
  const d = join(dir, "diagrams");
  for (const n of readdirSync(d)) if (n.endsWith(".hotspots.json")) rmSync(join(d, n));
  for (const n of readdirSync(d).filter((x) => x.endsWith(".svg")).sort()) {
    const r = side === "py" ? py("diagram_hotspots.py", [join("diagrams", n), "--write"], dir) : ts("hotspots", [join("diagrams", n), "--write"], dir);
    expect(r.code).toBe(0);
  }
}

const PACKETS = packets();
describe.skipIf(!PACKETS.length)("real packets", () => {
  for (const pk of PACKETS) {
    test(`${pk.split("/").pop()}: proposal table and --apply results are identical`, () => {
      const root = tempDir("ann");
      try {
        const A = join(root, "py"), B = join(root, "ts");
        copyPacket(pk, A); copyPacket(pk, B);
        const dry = both(A, B, ["."]);
        expect(dry.code).toBe(0);
        expect(dry.out).toMatch(/^proposals \d+; applied 0 \(min-score 0\.6, dry run\)$/m);
        const first = readdirSync(join(A, "diagrams")).find((n) => n.endsWith(".hotspots.json"))!.replace(".hotspots.json", "");
        both(A, B, [".", "--key", first]);
        both(A, B, [".", "--apply"]);
        both(A, B, ["--apply", ".", "--min-score", "0.5"]);
        regenerate(A, "py"); regenerate(B, "ts");
        expect(snapshot(B)).toEqual(snapshot(A));
        const fresh = both(A, B, [".", "--apply"]);
        expect(Number(/applied (\d+)/.exec(fresh.out)![1])).toBeGreaterThan(0);
        expect(both(A, B, ["."]).out).toContain("[kept: already set]");
      } finally { rmrf(root); }
    }, 120_000);
  }
});

function synthetic(dir: string) {
  mkdirSync(join(dir, "diagrams"), { recursive: true });
  mkdirSync(join(dir, "build"), { recursive: true });
  const anchors = [
    ["d3", "decision", "D3 · Rank by seniority"], ["pr-7", "pr", "PR 7 · The award record"], ["r2", "row", "R2 · Ranked list"],
    ["tbl-open-shift", "table", "open_shift"], ["queue-health", "heading", "Queue health and the post request"],
    ["the-award", "section", "The award · record and list"], ["persona-x", "persona", "Scheduler"], ["dup", "fold", "First"],
    ["dup", "fold", "Second title here for the fold"], ["e1", "milestone", "E1 · Milestone one"], ["s-x", "figure", "Queue health"],
    ["tbl-profile", "table", "profile"], ["tbl-ordering-run", "table", "ordering_run"],
  ].map(([id, kind, title]) => ({ id, kind, title }));
  const content = {
    anchors,
    defs: {
      d3: { text: "<b>Rank</b> open shifts by seniority, then by distance; ties go to the earliest request, and the queue is rebuilt every night at two in the morning, local time, so a late post waits:" },
      "pr-7": { text: "short   text\twith spaces" }, r2: {}, "queue-health": { text: "" }, "the-award": { text: "x".repeat(170) },
      dup: { text: "The second fold’s text, with a quote' and · a dot." },
    },
    schema: [{ table: "profile", id: "tbl-profile" }, { table: "ordering_run", id: "tbl-ordering-run" }],
  };
  writeFileSync(join(dir, "build", "content.json"), JSON.stringify(content, null, 1));
  const box = (label: string, extra: Record<string, unknown> = {}) =>
    ({ label, kind: "box", tone: "grey", x: 1.0, y: 2.0, w: 100.5, h: 50.0, text: label, href: "", image: "", note: "", table: "", ...extra });
  const boxes = [
    box("D3 ranking rule"), box("PR 7 and PR 9"), box("D3 and R2 together"), box("profile"), box("profile seniority rules"),
    box("open_shift (posted)"), box("open_shift · 3 rows"), box("Queue health and post requests"), box("Award record"),
    box("1 · Award record and list"), box("Plain box", { text: "see E1 and E1 again" }), box("Two refs", { text: "D3 or R2" }),
    box("Big frame", { kind: "frame", w: 900.0, h: 900.0 }), box("Small frame on the award record", { kind: "frame", w: 10.0, h: 10.0 }),
    box("Text only queue health", { kind: "text" }), box("ordering_run", { table: "ordering_run" }),
    box("Queue health and the post request", { href: "#queue-health", note: "mine" }), box("Second title here for the fold"),
    box("Scheduler"), box("Rank by seniority"), box("ranked list"), box("The   award record and list"), box("x"),
    box("Queue requests and health post"),
  ];
  writeFileSync(join(dir, "diagrams", "a.hotspots.json"), JSON.stringify({ svg: "a.svg", viewBox: [0.5, 0, 1000, 1000], boxes }, null, 1));
  writeFileSync(join(dir, "diagrams", "b.hotspots.json"), JSON.stringify({ svg: "b.svg", boxes: [box("Award record list"), box("PR 7")] }));
  writeFileSync(join(dir, "diagrams", ".hidden.hotspots.json"), "not json");
  writeFileSync(join(dir, "a-source.md"), '# T\n\n![alt](mockups/the-award.jpg "The award record and the list")\n' +
    '![q](mockups/queue.jpg "Queue health")\n![y](other/z.png "Award record")\n![multi\nline](mockups/m.jpg)\n' +
    '![r](mockups/r.jpg "Ranked list shown here")\n![r2](mockups/r2.jpg "the ranked list")\n');
  writeFileSync(join(dir, "z-notes.md"), '![n](mockups/n.jpg "Plain box notes")\n');
}

describe("synthetic packet", () => {
  test("every proposal branch, --apply, and the usage and error paths match the Python", () => {
    const root = tempDir("ann-syn");
    try {
      const A = join(root, "py"), B = join(root, "ts");
      synthetic(A); synthetic(B);
      const dry = both(A, B, ["."]);
      for (const why of ["label names D3", "label names PR-7", "label is table profile", "label starts with table profile",
        "label is table open_shift", "only the box text mentions E1", "label ~ title", "caption 'The award record and the list'"]) {
        expect(dry.out).toContain(why);
      }
      expect(dry.out).not.toContain("Big frame");
      both(A, B, [".", "--apply", "--min-score", "0.55"]);
      both(A, B, [".", "--apply", "--min-score", "-1e9"]);
      both(A, B, [".", "--key", "b", "--apply", "--min-score", "1_0"]);
      both(A, B, []);
      both(A, B, [".", "x"]);
      bothTraceback(A, B, [".", "--min-score"], "StopIteration");
      bothTraceback(A, B, [".", "--min-score", "nope"], "ValueError: could not convert string to float: 'nope'");
      expect(both(A, B, ["."]).code).toBe(0);
      const missing = py("annotate_hotspots.py", [".", "--build", "nowhere"], A);
      expect(missing.code).toBe(2);
      expect(ts("annotate", [".", "--build", "nowhere"], A)).toEqual(missing);
    } finally { rmrf(root); }
  }, 60_000);
});
