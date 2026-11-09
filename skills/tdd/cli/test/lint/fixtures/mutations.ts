// The lint mutation corpus: each case copies the built base packet (fixtures/base, built once per run), applies one
// mutation, and names the check ids it must fire. Python and TypeScript must then agree byte for byte (corpus.test.ts).
// Between them the cases fire every check id lint_packet.py can emit (inventory.test.ts reads the ids from the source).
import { chmodSync, existsSync, mkdirSync, readFileSync, rmSync, symlinkSync, truncateSync, utimesSync, writeFileSync, copyFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { SCRIPTS, T0, buildPacket, git, skillCopy, type Repo } from "../harness";

export interface Ctx {
  /** the case folder (cwd of both runs); it holds demo-packet/, sibling-packet/ */
  readonly root: string;
  readonly pk: string;
  readonly build: string;
  readonly repo: Repo;
  readonly scripts: string;
}

export interface Case {
  readonly name: string;
  /** check ids the Python must report (and so the TypeScript too) */
  readonly expect: readonly string[];
  /** CLI args before --json; default ["demo-packet"] */
  readonly args?: (c: Ctx) => string[];
  readonly mutate?: (c: Ctx) => void;
  /** runs after every mtime is reset to T0 */
  readonly times?: (c: Ctx) => void;
  /** lint_packet.py to run (default scripts/lint_packet.py) and the TypeScript run's extra env */
  readonly pyScript?: (c: Ctx) => string;
  readonly env?: (c: Ctx) => Record<string, string>;
  readonly code?: number;
  /** the run ends as lint_packet.py's does (a traceback): this is its last line, and there is no report or lint.json */
  readonly crash?: string;
}

const P = (c: Ctx, f: string) => join(c.pk, f);
export const read = (c: Ctx, f: string): string => readFileSync(P(c, f), "utf8");
export const write = (c: Ctx, f: string, s: string): void => {
  mkdirSync(dirname(P(c, f)), { recursive: true });
  writeFileSync(P(c, f), s);
};
export const edit = (c: Ctx, f: string, fn: (s: string) => string): void => {
  const before = read(c, f);
  const after = fn(before);
  if (after === before) throw new Error(`mutation did not change ${f}`);
  write(c, f, after);
};
/** Replace one literal (it must be there). */
const sub = (c: Ctx, f: string, a: string, b: string): void => edit(c, f, (s) => {
  if (!s.includes(a)) throw new Error(`${f}: no ${JSON.stringify(a)}`);
  return s.split(a).join(b);
});
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const json = (c: Ctx, f: string, fn: (v: any) => any): void => {
  const v = JSON.parse(read(c, f));
  write(c, f, JSON.stringify(fn(v) ?? v, null, 1) + "\n");
};
const rm = (c: Ctx, f: string): void => rmSync(P(c, f), { recursive: true, force: true });
const rebuild = (c: Ctx): void => {
  rm(c, "build");
  buildPacket(c.pk, "demo-packet");
  write(c, "build/tdd.html", "<!doctype html><title>Demo Queue</title><main id=\"redesign\"></main>\n");
};
const sparse = (c: Ctx, f: string, bytes: number): void => {
  write(c, f, "");
  truncateSync(P(c, f), bytes);
};
const touch = (c: Ctx, f: string, t: number): void => utimesSync(P(c, f), t, t);

/** What the corpus's build_tdd.py does after the real compile (PARITY's own cases need the two compilers to differ):
 * content: one more newline at the end of content.json; manifest: one more manifest warning; fail-fresh: as content,
 * and it fails outright when PARITY runs it (--no-copy). */
type Perturb = "content" | "manifest" | "fail-fresh";
const PERTURB_MAIN = `def _corpus_main():
    # the lint corpus: build_tdd.py with a deliberate difference (scripts/perturb.txt), so PARITY has something to find
    mode = open(os.path.join(HERE, "perturb.txt"), encoding="utf-8").read().strip()
    if mode == "fail-fresh" and "--no-copy" in sys.argv:
        sys.exit("build_tdd.py: fails on purpose (lint corpus)")
    a = parse_args(sys.argv[1:])
    Compiler(a).build()
    if mode in ("content", "fail-fresh"):
        with open(os.path.join(a.out, "content.json"), "a", encoding="utf-8") as f:
            f.write("\\n")
    elif mode == "manifest":
        p = os.path.join(a.out, "content.files.json")
        m = json.load(open(p, encoding="utf-8"))
        m["warnings"] = list(m.get("warnings") or []) + ["a warning only this build_tdd.py gives (lint corpus)"]
        with open(p, "w", encoding="utf-8") as f:
            json.dump(m, f, indent=1)

if __name__ == "__main__":
    _corpus_main()
`;

/** The skill beside the case (harness.skillCopy) with a build_tdd.py that differs from the TypeScript compiler on purpose.
 * The packet is rebuilt with it; lint_packet.py runs from the copy (pyScript) and tdd lint is pointed at it with
 * TDD_SCRIPTS (env), so both linters run the same build_tdd.py. */
const perturbed = (c: Ctx, mode: Perturb): string => {
  const scripts = join(c.root + "-skill", "tdd", "scripts");
  if (!existsSync(scripts)) {
    skillCopy(c.root + "-skill");
    const bt = join(scripts, "build_tdd.py");
    const src = readFileSync(bt, "utf8");
    const tail = "if __name__ == \"__main__\":\n    Compiler(parse_args(sys.argv[1:])).build()\n";
    if (!src.endsWith(tail)) throw new Error("build_tdd.py no longer ends with its main block: update the corpus's perturbed copy");
    writeFileSync(bt, src.slice(0, -tail.length) + PERTURB_MAIN);
  }
  writeFileSync(join(scripts, "perturb.txt"), mode + "\n");
  return scripts;
};
/** A case whose build comes from the perturbed build_tdd.py, linted by both linters with it. */
const perturbedCase = (name: string, mode: Perturb, expect: string[], extra: string[] = []): Case => ({
  name, expect, args: () => ["demo-packet", ...extra],
  mutate: (c) => {
    const scripts = perturbed(c, mode);
    rm(c, "build");
    buildPacket(c.pk, "demo-packet", scripts);
    write(c, "build/tdd.html", "<!doctype html><title>Demo Queue</title><main id=\"redesign\"></main>\n");
  },
  pyScript: (c) => join(perturbed(c, mode), "lint_packet.py"),
  env: (c) => ({ TDD_SCRIPTS: perturbed(c, mode) }),
});
const MD = "demo-packet.md";
const HS = "diagrams/flow.hotspots.json";
const SCHEMA = "schema/demo_table.json";
const CONTENT = "build/content.json";
const MANIFEST = "build/content.files.json";
const MB = 1024 * 1024;
const MARKER = "PLACEHOLDER · REPLACE ME";

export const CASES: readonly Case[] = [
  // ------------------------------------------------------------------ the base and the run modes
  { name: "base", expect: [] },
  { name: "compile-flag", expect: [], args: () => ["demo-packet", "--compile"] },
  { name: "no-build-compiles", expect: [], mutate: (c) => rm(c, "build") },
  { name: "skip-rule-and-check", expect: [], args: () => ["demo-packet", "--skip", "CITES, REFS/title-token,", "--strict"],
    mutate: (c) => json(c, HS, (h) => void (h.boxes[0].note = "Holds the jobs (D1).")) },
  { name: "strict-warnings", expect: ["SCHEMA/unused"], args: () => ["demo-packet", "--strict"], code: 1,
    mutate: (c) => write(c, "schema/spare_table.json", JSON.stringify({ table: "spare_table", state: "existing", columns: [{ name: "id", type: "uuid" }] })) },
  { name: "build-flag", expect: [], args: (c) => ["demo-packet", "--build", join(c.pk, "build")] },

  // ------------------------------------------------------------------ STRUCT
  { name: "struct-no-source", expect: ["STRUCT/no-source"], mutate: (c) => { rm(c, MD); rm(c, "build"); } },
  { name: "struct-no-content-compile-fails", expect: ["STRUCT/no-content"], mutate: (c) => { rm(c, "build"); json(c, "meta.json", (m) => void (m.people = 7)); } },
  // a compile that fails in hotspots(): each linter reports its own compiler's exception, and the texts are CPython's
  { name: "struct-no-content-viewbox-strings", expect: ["STRUCT/no-content"], args: () => ["demo-packet", "--compile"],
    mutate: (c) => json(c, HS, (h) => void (h.viewBox = ["0", "0", "400", "200.04"])) },
  { name: "struct-no-content-viewbox-zero", expect: ["STRUCT/no-content"], args: () => ["demo-packet", "--compile"],
    mutate: (c) => json(c, HS, (h) => void (h.viewBox = [0, 0, 0, 200])) },
  { name: "struct-no-content-box-no-x", expect: ["STRUCT/no-content"], args: () => ["demo-packet", "--compile"],
    mutate: (c) => json(c, HS, (h) => void delete h.boxes[0].x) },
  { name: "struct-missing-section", expect: ["STRUCT/missing-section"],
    mutate: (c) => { sub(c, MD, "## Front end {#frontend; lead: no}", "## Screens {#screens; lead: no}"); rebuild(c); } },
  { name: "struct-section-order", expect: ["STRUCT/section-order"],
    mutate: (c) => { edit(c, MD, (s) => {
      const a = s.indexOf("## Front end"), b = s.indexOf("## Rule coverage"), e = s.indexOf("## End-to-end flow");
      return s.slice(0, a) + s.slice(b, e) + s.slice(a, b) + s.slice(e);
    }); rebuild(c); } },
  { name: "struct-numbering-num", expect: ["STRUCT/numbering"], mutate: (c) => json(c, CONTENT, (j) => void (j.sections[1].num = 7)) },
  { name: "struct-numbering-count", expect: ["STRUCT/numbering"], mutate: (c) => edit(c, MD, (s) => s + "\n## Appendix {#appendix}\n\nMore.\n") },
  { name: "struct-numbering-title", expect: ["STRUCT/numbering"], mutate: (c) => sub(c, MD, "## Rule coverage {#coverage", "## Rule coverage and limits {#coverage") },
  { name: "struct-heading-id", expect: ["STRUCT/heading-id"], mutate: (c) => { sub(c, MD, "## End-to-end flow {#flow}", "## End-to-end flow"); rebuild(c); } },
  { name: "struct-id-collision-twice", expect: ["STRUCT/id-collision"],
    mutate: (c) => { sub(c, MD, "The helper in", "First note. {#twice}\n\nSecond note. {#twice}\n\nThe helper in"); rebuild(c); } },
  { name: "struct-id-collision-dropped", expect: ["STRUCT/id-collision"],
    mutate: (c) => { sub(c, MD, "## End-to-end flow {#flow}\n", "## End-to-end flow {#flow}\n\n- an item {#ghost}\n- another\n"); rebuild(c); } },
  { name: "struct-raw-markdown", expect: ["STRUCT/raw-markdown"],
    mutate: (c) => { sub(c, "prs/pr-01.md", "Adds the table (D2).", "Adds the table (D2); see [the flow](#flow) and ![x](mockups/shot.png)."); rebuild(c); } },
  { name: "struct-raw-markdown-content", expect: ["STRUCT/raw-markdown"],
    mutate: (c) => json(c, CONTENT, (j) => {
      j.sections[3].blocks.push({ type: "p", html: "Literal **bold** and [a link](#flow), [diagram: x] <code>**not this**</code> and<br>::: cards" });
      const card = (function find(n: unknown): Record<string, string> | null {
        if (Array.isArray(n)) { for (const x of n) { const r = find(x); if (r) return r; } return null; }
        if (n && typeof n === "object") { const o = n as Record<string, unknown>; if ("bodyHtml" in o && "n" in o) return o as Record<string, string>; for (const v of Object.values(o)) { const r = find(v); if (r) return r; } }
        return null;
      })(j.sections);
      if (!card) throw new Error("no PR card in content.json");
      card.bodyHtml += "<p>See [the queue](#redesign) and \u00e9t\u00e9 **x\u00e9**.</p>";
    }) },
  { name: "struct-bad-json", expect: ["STRUCT/bad-json"], mutate: (c) => write(c, CONTENT, "{\"sections\": [\n") },

  // ------------------------------------------------------------------ DNUM
  { name: "dnum-none", expect: ["DNUM/none"],
    mutate: (c) => { edit(c, MD, (s) => s.slice(0, s.indexOf("::: decisions")) + "We decided D1 and D-3 and D10 in the meeting.\n\n" /* no two tokens with the same number (D2, D-2): Python orders such ties by hash seed */ + s.slice(s.indexOf("## For agents"))); rebuild(c); } },
  { name: "dnum-duplicate", expect: ["DNUM/duplicate"], mutate: (c) => { sub(c, MD, "decision D2 · Add", "decision D1 · Add"); rebuild(c); } },
  { name: "dnum-bad-id", expect: ["DNUM/bad-id"], mutate: (c) => { sub(c, MD, "decision D2 · Add", "decision Retry · Add"); rebuild(c); } },
  { name: "dnum-no-status", expect: ["DNUM/no-status"], mutate: (c) => { sub(c, MD, " {status: Owner, 2026-01-03}", ""); rebuild(c); } },
  { name: "dnum-done-no-date", expect: ["DNUM/done-no-date"], mutate: (c) => { sub(c, MD, "{status: Owner, 2026-01-03}", "{status: Owner; state: done}"); rebuild(c); } },
  { name: "dnum-outside-section", expect: ["DNUM/outside-section"],
    mutate: (c) => { sub(c, MD, "notes live in `docs/notes.md`.\n", "notes live in `docs/notes.md`.\n\n::: decisions\n:::: decision D3 · Batch the retries? {status: Owner, 2026-01-04}\nNo.\n::::\n:::\n"); rebuild(c); } },
  { name: "dnum-gap", expect: ["DNUM/gap"], mutate: (c) => { sub(c, MD, "decision D2 · Add", "decision D4 · Add"); sub(c, MD, "(D2)", "(D4)"); sub(c, "prs/pr-01.md", "(D2)", "(D4)"); rebuild(c); } },

  // ------------------------------------------------------------------ REFS
  { name: "refs-unlinked", expect: ["REFS/unlinked"], mutate: (c) => { sub(c, MD, "every job keeps its order.", "every job keeps its order (R9, Q4)."); rebuild(c); } },
  { name: "refs-broken-anchor", expect: ["REFS/broken-anchor"], mutate: (c) => { sub(c, MD, "every job keeps its order.", "every job keeps its order ([see](#nowhere))."); rebuild(c); } },
  { name: "refs-hotspot-anchor", expect: ["REFS/hotspot-anchor", "DIAG/hotspot-table"],
    mutate: (c) => { json(c, HS, (h) => { h.boxes[0].href = "#nowhere"; h.boxes[1].table = "ghost_table"; }); rebuild(c); } },
  { name: "refs-family", expect: ["REFS/family"], mutate: (c) => json(c, "meta.json", (m) => void (m.refs = { OS: "#nowhere", LN: 7 })) },
  { name: "refs-missed-link", expect: ["REFS/missed-link"],
    mutate: (c) => json(c, CONTENT, (j) => void j.sections[3].blocks.push({ type: "callout", text: { html: "See D9 for the lane." } })) },
  { name: "refs-title-token", expect: ["REFS/title-token"],
    mutate: (c) => { json(c, HS, (h) => void (h.boxes[0].note = "Holds the jobs (D1).")); rebuild(c);
      json(c, CONTENT, (j) => void j.sections[3].blocks.push({ type: "p", extra: "see D2 and Q7" })); } },

  // ------------------------------------------------------------------ CITES
  { name: "cites-no-repo-path", expect: ["CITES/no-repo"], mutate: (c) => json(c, "meta.json", (m) => void (m.repo_path = null)) },
  { name: "cites-no-repo-checkout", expect: ["CITES/no-repo"], mutate: (c) => json(c, "meta.json", (m) => void (m.repo_path = c.root)) },
  { name: "cites-no-repo-ls-tree", expect: ["CITES/no-repo"],
    mutate: (c) => {
      const r = join(c.root, "brokenrepo");
      const out = Bun.spawnSync(["cp", "-R", c.repo.path, r]);
      if (out.exitCode !== 0) throw new Error("cp -R failed");
      rmSync(join(r, ".git", "objects", c.repo.tree.slice(0, 2), c.repo.tree.slice(2)), { force: true });
      json(c, "meta.json", (m) => void (m.repo_path = r));
    } },
  { name: "cites-repo-url", expect: ["CITES/repo-url"], mutate: (c) => json(c, "meta.json", (m) => void (m.repo_url = "https://example.com/other/queue")) },
  { name: "cites-sha-date-wrong", expect: ["CITES/sha-date"], mutate: (c) => json(c, "meta.json", (m) => void (m.sha_date = "2026-01-09")) },
  { name: "cites-sha-date-none", expect: ["CITES/sha-date"], mutate: (c) => json(c, "meta.json", (m) => void delete m.sha_date) },
  { name: "cites-sha-unknown", expect: ["CITES/sha-unknown", "CITES/working-tree"],
    mutate: (c) => { json(c, "meta.json", (m) => void (m.sha = "deadbeef00")); sub(c, MD, "reads the job;", "reads the job (`SRC/untracked.ts:1-2`);"); rebuild(c); } },
  { name: "cites-no-sha", expect: ["CITES/sha-unknown"], mutate: (c) => json(c, "meta.json", (m) => void delete m.sha) },
  { name: "cites-paths", expect: ["CITES/short-path", "CITES/path-unknown", "CITES/line-range", "CITES/no-line"],
    mutate: (c) => { sub(c, MD, "reads the job;", "reads the job (`deep/util.ts:2`, `util.ts`, `SRC/gone.ts:4`, `SRC/gone.ts`, `SRC/app.ts:50-60`, `SRC/app.ts`, `SRC/app.ts:0`);"); rebuild(c); } },
  { name: "cites-packet-copy", expect: ["CITES/line-range"],
    mutate: (c) => { write(c, "notes/local.md", "one\ntwo\n"); sub(c, MD, "reads the job;", "reads the job (`notes/local.md:9`);"); rebuild(c); } },

  // ------------------------------------------------------------------ DIAG
  { name: "diag-duplicate", expect: ["DIAG/duplicate"], mutate: (c) => { sub(c, MD, "## End-to-end flow {#flow}\n", "## End-to-end flow {#flow}\n\n[diagram: flow · Again]\n"); rebuild(c); } },
  { name: "diag-missing", expect: ["DIAG/missing"], mutate: (c) => { sub(c, MD, "## End-to-end flow {#flow}\n", "## End-to-end flow {#flow}\n\n[diagram: ghost | A ghost]\n"); rebuild(c); } },
  { name: "diag-stale-svg", expect: ["DIAG/stale-svg"], mutate: (c) => write(c, "diagrams/flow.mmd", "graph LR; a-->b\n"),
    times: (c) => touch(c, "diagrams/flow.mmd", T0 + 5) },
  { name: "diag-viewbox", expect: ["DIAG/viewbox"], mutate: (c) => json(c, HS, (h) => void (h.viewBox = [0, 0, 500.5, 200])) },
  { name: "diag-viewbox-short", expect: ["DIAG/viewbox"], mutate: (c) => json(c, HS, (h) => void (h.viewBox = [0, 0, 400])) },
  { name: "diag-hotspot-label", expect: ["DIAG/hotspot-label"], mutate: (c) => json(c, HS, (h) => void (h.boxes[0].label = "Old queue")) },
  { name: "diag-hotspot-image", expect: ["DIAG/hotspot-image"], mutate: (c) => json(c, HS, (h) => void (h.boxes[1].image = "mockups/none.png")) },
  { name: "diag-legend-tone", expect: ["DIAG/legend-tone"],
    mutate: (c) => json(c, CONTENT, (j) => { const d = j.sections[0].blocks.find((b: { type: string }) => b.type === "diagram"); d.legend[0].tone = "green green"; d.legend[1].tone = "pink"; }) },
  { name: "diag-bad-json", expect: ["DIAG/bad-json"], mutate: (c) => write(c, HS, "{\"boxes\": [}") },
  { name: "diag-svg-not-utf8", expect: [], mutate: (c) => {
    const b = readFileSync(P(c, "diagrams/flow.svg"));
    const at = b.indexOf("holds the jobs");
    writeFileSync(P(c, "diagrams/flow.svg"), Buffer.concat([b.subarray(0, at), Buffer.from([0xff]), b.subarray(at)]));
  } },

  // ------------------------------------------------------------------ SCHEMA
  { name: "schema-shape", expect: ["SCHEMA/shape"], mutate: (c) => write(c, "schema/list.json", "[1, 2]\n") },
  { name: "schema-required-state", expect: ["SCHEMA/required", "SCHEMA/state"],
    mutate: (c) => { write(c, "schema/a_table.json", JSON.stringify({ table: "a_table", columns: {} })); write(c, "schema/b_table.json", JSON.stringify({ state: "maybe", columns: [] })); } },
  { name: "schema-column", expect: ["SCHEMA/column"],
    mutate: (c) => { json(c, SCHEMA, (t) => { t.columns.push({ type: "text" }, { name: "plain" }, { name: "odd", type: "text", change: "moved" }, { name: "was_less", type: "text", change: "changed" }, { name: "kept", type: "text", change: "unchanged" }); });
      write(c, "schema/c_table.json", JSON.stringify({ table: "c_table", state: "changed", columns: [{ name: "x", type: "int" }] }, null, 1)); } },
  { name: "schema-sample-rows", expect: ["SCHEMA/sample-rows"],
    mutate: (c) => json(c, SCHEMA, (t) => { t.sample_rows.push(7, { id: "b", nope: 1, also: 2 }); delete t.sample_source; }) },
  { name: "schema-measured", expect: ["SCHEMA/measured"],
    mutate: (c) => json(c, SCHEMA, (t) => { t.measured.bad = 3; t.measured.reads = { value: 12.5 }; t.measured.writes = { value: "40", source: "a query", date: "Jan 2" }; t.measured.none = { value: null }; }) },
  { name: "schema-expected", expect: ["SCHEMA/expected"],
    mutate: (c) => json(c, SCHEMA, (t) => { t.expected.bad = "x"; t.expected.rows = { value: 1000 }; t.expected.reads = { value: "about 3/s", note: " " }; t.expected.size = { value: "large" }; }) },
  { name: "schema-unused", expect: ["SCHEMA/unused"], mutate: (c) => write(c, "schema/spare_table.json", JSON.stringify({ table: "spare_table", state: "existing", columns: [] })) },
  { name: "schema-bad-json", expect: ["SCHEMA/bad-json"], mutate: (c) => write(c, "schema/broken.json", "{\n \"table\": \"broken\",\n}\n") },
  { name: "schema-not-utf8", expect: ["SCHEMA/bad-json"], mutate: (c) => writeFileSync(P(c, "schema/latin.json"), Buffer.from([0x7b, 0x22, 0xe9, 0x22, 0x3a, 0x31, 0x7d])) },
  { name: "schema-missing-file", expect: ["SCHEMA/missing-file"], mutate: (c) => mkdirSync(P(c, "schema/folder.json")) },

  // ------------------------------------------------------------------ FILES
  { name: "files-no-manifest", expect: ["FILES/no-manifest"], mutate: (c) => rm(c, MANIFEST) },
  { name: "files-bad-json", expect: ["FILES/bad-json", "FILES/no-manifest"], mutate: (c) => { write(c, MANIFEST, "nope"); write(c, "page.files.json", "{\"files\": 1,}"); } },
  { name: "files-missing-file", expect: ["FILES/missing-file"], mutate: (c) => mkdirSync(P(c, "share.files.json")) },
  { name: "files-missing", expect: ["FILES/missing"],
    mutate: (c) => json(c, MANIFEST, (m) => { m.missing = ["mockups/shot.png", "mockups/gone.png"]; m.files["mockups/lost.png"] = join(c.pk, "mockups", "lost.png"); m.files["mockups/null.png"] = null; }) },
  { name: "files-too-big", expect: ["FILES/too-big"],
    mutate: (c) => { sparse(c, "mockups/huge.png", 16 * MB); sparse(c, "build/blob.bin", 15 * MB + 1);
      json(c, MANIFEST, (m) => { m.files["blob.bin"] = join(c.pk, "build", "blob.bin"); m.over_15mb = ["blob.bin", "other.mp4"]; }); } },
  { name: "files-page-size", expect: ["FILES/page-size"], mutate: (c) => { sparse(c, "build/tdd.html", 17 * MB); json(c, MANIFEST, (m) => void (m.page_bytes = 17.5 * MB)); } },
  { name: "files-page-not-built", expect: [], mutate: (c) => rm(c, "build/tdd.html") },
  { name: "files-orphan", expect: ["FILES/orphan"],
    mutate: (c) => { write(c, "mockups/unused.png", "x"); write(c, "diagrams/sub/old.svg", "<svg/>"); write(c, "mockups/Upper.JPG", "x"); sub(c, MD, "The operator sees the retry count.", "The operator sees the retry count (mockups/unused.png)."); } },
  { name: "files-pr-unloaded", expect: ["FILES/pr-unloaded"],
    mutate: (c) => { write(c, "prs/pr-xx.md", "# not a PR\n"); write(c, "prs/pr-02.md", "---\ntitle: Later\n---\n# PR 2: Later\n"); } },
  // FILES/placeholder: a file the page publishes (the manifest's files: the build's copies, or with --compile the packet's
  // own files) still carries the scaffold's marker; the finding's line is the first marker's, counted in bytes
  { name: "files-placeholder", expect: ["FILES/placeholder"],
    mutate: (c) => {
      // CRLF line ends in the SVG copy; in the PNG copy the marker comes twice after IEND (its signature holds a \n)
      edit(c, "build/diagrams/flow.svg", (s) => s.split("\n").join("\r\n").split("</svg>").join(`<text>${MARKER}</text>\r\n</svg>`));
      writeFileSync(P(c, "build/mockups/shot.png"), Buffer.concat([readFileSync(P(c, "build/mockups/shot.png")), Buffer.from(`\n\n${MARKER}\n${MARKER}\n`)]));
    } },
  // the packet named through a link: the build's paths are resolved, so the file is named relative to the resolved packet
  { name: "files-placeholder-linked-path", expect: ["FILES/placeholder"], args: (c) => [join(c.root + "-link", "demo-packet")],
    mutate: (c) => { symlinkSync(c.root, c.root + "-link"); edit(c, "build/diagrams/flow.svg", (s) => `<!-- ${MARKER} -->\n` + s); } },
  { name: "files-placeholder-compiled", expect: ["FILES/placeholder"], args: () => ["demo-packet", "--compile"],
    mutate: (c) => writeFileSync(P(c, "mockups/shot.png"), Buffer.concat([readFileSync(P(c, "mockups/shot.png")), Buffer.from(`\n${MARKER}\n`)])) },
  { name: "files-placeholder-unreadable", expect: [],
    mutate: (c) => writeFileSync(P(c, "build/mockups/shot.png"), Buffer.concat([readFileSync(P(c, "build/mockups/shot.png")), Buffer.from(MARKER)])),
    times: (c) => chmodSync(P(c, "build/mockups/shot.png"), 0o000) },
  // FILES/placeholder-text: a line of the source or of status.md still carries one: the template's prompts, the
  // YYYY-MM-DD date, the marker; fenced code counts, one-line comments do not, nor a prompt right after a word character
  { name: "files-placeholder-text", expect: ["FILES/placeholder-text"],
    mutate: (c) => {
      sub(c, MD, "The worker loop.", "<What stays exactly as it is today.>");
      sub(c, MD, "reads the job; notes live in `docs/notes.md`.\n", "reads the job; notes live in `docs/notes.md`.\n\n" + [
        "<!-- a one-line comment with <Role> and YYYY-MM-DD: the compiler drops it -->",
        "   <!-- <Owner> in an indented one-line comment -->",
        "A hook such as useState<State>, Array<Role> or _<Owner> is code; <name>, <path> and <n> are notation; é<Moment> too.",
        "**<State>:** <what the screen shows in that state, and why.> And <State> once more, then (<Role>) and -<Moment>. {small}",
        `Measured at <YYYY-MM-DD hh:mm>, on <YYYY-MM-DD> and on YYYY-MM-DD; ${MARKER}`,
        "",
        "```sql",
        "CREATE TABLE <new_table> (",
        "  <new_column> text  -- <what it holds>",
        ");",
        "```",
        "",
      ].join("\n"));
      write(c, "status.md", [
        "# <Title: the finding, not the topic>: status (the one living doc)",
        "",
        "artifact_url: (not published yet)",
        "",
        "## Decisions (the owner's words)",
        "- (none yet. Each answer gets the next D-number: D<n> (YYYY-MM-DD, who): \"<the owner's words>\", then one line on what it means.)",
        "- D1 (2026-01-02, Owner): \"Keep the worker loop.\"",
        "<!-- <Owner> -->",
        "",
      ].join("\r\n"));
    } },
  { name: "files-placeholder-text-no-manifest", expect: ["FILES/no-manifest", "FILES/placeholder-text"],
    mutate: (c) => { rm(c, MANIFEST); write(c, "status.md", "Waiting on <Owner> since YYYY-MM-DD.\n"); } },
  { name: "files-placeholder-skip-one", expect: ["FILES/placeholder-text"], args: () => ["demo-packet", "--skip", "FILES/placeholder"],
    mutate: (c) => {
      edit(c, "build/diagrams/flow.svg", (s) => s + `<!-- ${MARKER} -->\n`);
      write(c, "status.md", `${MARKER}\n`);
    } },

  // ------------------------------------------------------------------ META
  { name: "meta-missing", expect: ["META/missing"], mutate: (c) => rm(c, "meta.json") },
  { name: "meta-bad-json", expect: ["META/bad-json"], mutate: (c) => write(c, "meta.json", "{\"title\": \"x\"\n\"rev\": 1}") },
  { name: "meta-fields", expect: ["META/field"],
    mutate: (c) => json(c, "meta.json", (m) => { m.title = " "; m.people = [{ role: "Reviewer", name: "B" }, { role: "author" }]; m.updated = "Jan 4"; m.rev = "2"; m.status = ""; }) },
  { name: "meta-rev-float", expect: ["META/field"], mutate: (c) => sub(c, "meta.json", "\"rev\": 1,", "\"rev\": 1.0,") },
  { name: "meta-stale-build", expect: ["META/stale-build", "META/rev-not-moved"],
    times: (c) => { touch(c, MD, T0 + 7200); touch(c, "schema", T0 + 60); touch(c, "meta.json", T0 + 30); touch(c, "prs/pr-01.md", T0 + 7200); touch(c, "diagrams/flow.svg", T0 + 90); } },
  { name: "meta-stale-build-rev-moved", expect: ["META/stale-build"],
    mutate: (c) => json(c, "meta.json", (m) => void (m.rev = 2)), times: (c) => touch(c, MD, T0 + 100) },
  { name: "meta-artifact-url", expect: ["META/artifact-url"], mutate: (c) => json(c, "meta.json", (m) => void (m.status = "Reviewed")) },
  { name: "meta-artifact-url-status-md", expect: [],
    mutate: (c) => { json(c, "meta.json", (m) => void (m.status = "As built")); write(c, "status.md", "artifact_url: https://example.com/a/1\n"); } },

  // ------------------------------------------------------------------ AGENTS
  { name: "agents-problem", expect: ["AGENTS/problem"], mutate: (c) => mkdirSync(P(c, "build/agents")) },
  { name: "agents-stale-lane", expect: ["AGENTS/problem"],
    mutate: (c) => { write(c, "build/agents/README.md", "<!-- agents lane · content 2020-01-01T00:00:00Z page none · write_agents/0 -->\n");
      write(c, "build/agents/tokens.json", JSON.stringify({ files: { "README.md": { sha256: "0" }, "gone.md": { sha256: "1" } } })); write(c, "build/agents/extra.md", "x"); } },
  { name: "agents-check-failed", expect: ["AGENTS/check-failed"],
    mutate: (c) => { write(c, "build/agents/README.md", "header\n"); write(c, "build/agents/locked.md", "secret\n");
      write(c, "build/agents/tokens.json", JSON.stringify({ files: { "locked.md": { sha256: "0" } } })); },
    times: (c) => chmodSync(P(c, "build/agents/locked.md"), 0o000) },
  // a malformed tokens.json: write_agents.check raises (it catches only OSError and ValueError), lint_packet.py reports it
  ...([
    ["files-list", { files: ["README.md"] }],
    ["root-list", [1, 2]],
    ["root-null", null],
    ["record-str", { files: { "README.md": "nope" } }],
    ["files-str", { files: "README.md" }],
  ] as const).map(([tag, tokens]): Case => ({
    name: `agents-tokens-${tag}`, expect: ["AGENTS/check-failed"],
    mutate: (c) => { write(c, "build/agents/README.md", "<!-- agents lane · content 2020-01-01T00:00:00Z page none · write_agents/0 -->\n");
      write(c, "build/agents/tokens.json", JSON.stringify(tokens)); },
  })),
  { name: "agents-tokens-skip-check-failed", expect: [], args: () => ["demo-packet", "--skip", "AGENTS/check-failed"], code: 0,
    mutate: (c) => { write(c, "build/agents/README.md", "<!-- agents lane · content 2020-01-01T00:00:00Z page none · write_agents/0 -->\n");
      write(c, "build/agents/tokens.json", JSON.stringify({ files: ["README.md"] })); } },

  // ------------------------------------------------------------------ XDOC
  { name: "xdoc-glossary-conflict", expect: ["XDOC/glossary-conflict"],
    mutate: (c) => writeFileSync(join(c.root, "sibling-packet", "sibling-packet.md"), "# Sibling\n\n::: glossary\n- **Lane** — a worker pool with a shared budget\n- **Other** - unrelated\n:::\n") },
  { name: "xdoc-no-glossary", expect: [], mutate: (c) => { edit(c, MD, (s) => s.slice(0, s.indexOf(":::: fold Glossary")) + ":::\n"); rebuild(c); } },

  // ------------------------------------------------------------------ PARITY (a build_tdd.py that differs on purpose)
  perturbedCase("parity-content", "content", ["PARITY/content"]),
  perturbedCase("parity-content-compiled", "content", ["PARITY/content"], ["--compile"]),
  perturbedCase("parity-manifest", "manifest", ["PARITY/manifest"]),
  perturbedCase("parity-fresh-fails", "fail-fresh", ["PARITY/content"]),
  { name: "parity-stale-build", expect: [], mutate: (c) => sub(c, MD, "The helper in", "A sentence the build has not seen. The helper in") },
  { name: "parity-both-fail", expect: [], mutate: (c) => json(c, "meta.json", (m) => void (m.people = 7)) },
  { name: "parity-manifest-only-under-build", expect: [], mutate: (c) => json(c, MANIFEST, (m) => void (m.warnings = ["an old warning"])) },
  { name: "parity-manifest-unreadable", expect: ["FILES/bad-json"], args: () => ["demo-packet", "--skip", "FILES/no-manifest"],
    mutate: (c) => write(c, MANIFEST, "{") },

  // ------------------------------------------------------------------ fail closed: a JSON root of the wrong type
  // lint_packet.py calls .get on these roots unguarded and ends with a traceback (exit 1, no report, no lint.json)
  { name: "crash-content-list", expect: [], mutate: (c) => write(c, CONTENT, "[]\n"), crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "crash-content-str", expect: [], mutate: (c) => write(c, CONTENT, "\"x\"\n"), crash: "AttributeError: 'str' object has no attribute 'get'" },
  { name: "crash-content-int", expect: [], mutate: (c) => write(c, CONTENT, "1\n"), crash: "AttributeError: 'int' object has no attribute 'get'" },
  { name: "crash-content-defs-int", expect: [], mutate: (c) => json(c, CONTENT, (v) => void (v.defs = 3)), crash: "TypeError: 'int' object is not iterable" },
  { name: "content-null", expect: [], mutate: (c) => write(c, CONTENT, "null\n") },
  { name: "crash-manifest-list", expect: [], mutate: (c) => write(c, MANIFEST, "[]\n"), crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "crash-manifest-bool", expect: [], mutate: (c) => write(c, MANIFEST, "true\n"), crash: "AttributeError: 'bool' object has no attribute 'get'" },
  { name: "crash-manifest-list-parity", expect: [], args: () => ["demo-packet", "--skip", "FILES"], mutate: (c) => write(c, MANIFEST, "[]\n"),
    crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "crash-manifest-files-list", expect: [], mutate: (c) => json(c, MANIFEST, (m) => void (m.files = ["x"])),
    crash: "AttributeError: 'list' object has no attribute 'items'" },
  { name: "crash-sibling-manifest", expect: [], mutate: (c) => write(c, "page.files.json", "[1]\n"), crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "sibling-manifest-empty-list", expect: [], mutate: (c) => write(c, "page.files.json", "[]\n") },
  { name: "crash-packet-json", expect: [], mutate: (c) => { json(c, "meta.json", (m) => void (m.status = "Reviewed")); write(c, "packet.json", "[]\n"); },
    crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "packet-json-draft", expect: [], mutate: (c) => write(c, "packet.json", "[]\n") },
  { name: "crash-meta-list", expect: [], mutate: (c) => write(c, "meta.json", "[]\n"), crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "crash-meta-truthy-list", expect: [], mutate: (c) => write(c, "meta.json", "[1]\n"), crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "crash-hotspots-list", expect: [], mutate: (c) => write(c, HS, "[1]\n"), crash: "AttributeError: 'list' object has no attribute 'get'" },
  { name: "crash-cite-record", expect: [], mutate: (c) => json(c, CONTENT, (v) => void v.cites.push("x")), crash: "AttributeError: 'str' object has no attribute 'get'" },
];

/** Usage cases: argv only, no packet needed beyond the template copy. */
export const USAGE: readonly { name: string; args: string[] }[] = [
  { name: "no-args", args: [] },
  { name: "two-args", args: ["demo-packet", "sibling-packet"] },
  { name: "not-a-dir", args: ["nowhere"] },
  { name: "unknown-option", args: ["demo-packet", "--fast"] },
  { name: "needs-a-value", args: ["demo-packet", "--json"] },
  { name: "help", args: ["--help"] },
  { name: "h-after-bad", args: ["-h", "--fast"] },
];

export const existsAt = (c: Ctx, f: string): boolean => existsSync(P(c, f));
export { git };
