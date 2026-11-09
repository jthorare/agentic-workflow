// A fresh packet lints clean (N14): new_packet.py and tdd new each scaffold a packet, the matching compiler builds it
// (build_tdd.py, tdd build), and both linters then report 0 errors and 0 warnings, --strict included, with identical
// stdout and lint.json. The compile's only warning is the empty prs/ folder. Every image the template references is
// a placeholder the scaffold wrote (templates/packet/), so nothing is missing from the publish manifest and nothing
// under diagrams/ or mockups/ is an orphan. D1 is dated the scaffold's day, so it renders decided; Q1 stays open.
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { py, rmrf, tempDir, ts, type Run } from "../hotspots/_harness";
import { GENERATED, HAVE_PYTHON, pyLint, tsLint } from "../lint/harness";

const TODAY = "2026-11-08";
const PLACEHOLDERS = ["diagrams/flow.svg", "diagrams/redesign.svg", "diagrams/schema.svg", "mockups/moment-a-1.svg",
  "mockups/moment-a-2.svg", "mockups/moment-a-3.svg", "mockups/today-moment-a.svg"];

let T = "";
let env: Record<string, string | undefined> = {};
let repo = "";

function git(cwd: string, ...args: string[]) {
  const r = Bun.spawnSync({ cmd: ["git", ...args], cwd, env: { ...process.env, ...env, GIT_AUTHOR_NAME: "C", GIT_AUTHOR_EMAIL: "c@example.com",
    GIT_COMMITTER_NAME: "C", GIT_COMMITTER_EMAIL: "c@example.com", GIT_AUTHOR_DATE: "2026-01-02T03:04:05Z", GIT_COMMITTER_DATE: "2026-01-02T03:04:05Z" } });
  if (r.exitCode !== 0) throw new Error(`git ${args.join(" ")}: ${r.stderr}`);
}

beforeAll(() => {
  T = tempDir("scaffold");
  const home = join(T, "home");
  mkdirSync(home);
  env = { HOME: home, XDG_CONFIG_HOME: join(home, ".config"), GIT_CONFIG_NOSYSTEM: "1", TDD_TODAY: TODAY, SYSTEM_DESIGN_OUT: "",
    TDD_GENERATED: GENERATED, COLUMNS: undefined, LINES: undefined };
  repo = join(T, "repo");
  mkdirSync(repo);
  git(repo, "init", "-q", "-b", "main");
  git(repo, "config", "user.name", "Test Author");
  git(repo, "remote", "add", "origin", "git@github.com:owner/demo-repo.git");
  writeFileSync(join(repo, "f"), "x\n");
  git(repo, "add", "f");
  git(repo, "commit", "-qm", "init");
});
afterAll(() => rmrf(T));

/** Scaffold with one implementation, compile with the same one, and return the packet path and the compile run. */
function scaffold(impl: "py" | "ts"): { packet: string; scaffoldRun: Run; compile: Run } {
  const root = join(T, `root-${impl}`);
  const argv = ["sample", "--title", "Ranking — the queue is the finding", "--ticket", "P-ABC-123", "--repo", repo, "--root", root];
  const scaffoldRun = impl === "py" ? py("new_packet.py", argv, T, env) : ts("new", argv, T, env);
  const packet = join(root, "owner-demo-repo", "system-design", "sample");
  const compile = impl === "py" ? py("build_tdd.py", ["sample.md", "build"], packet, env) : ts("build", ["sample.md", "build"], packet, env);
  return { packet, scaffoldRun, compile };
}

describe.skipIf(!HAVE_PYTHON)("a fresh scaffold lints clean", () => {
  for (const impl of ["py", "ts"] as const) {
    test(`${impl === "py" ? "new_packet.py + build_tdd.py" : "tdd new + tdd build"}: 0 errors, 0 warnings in both linters`, () => {
      const { packet, scaffoldRun, compile } = scaffold(impl);
      expect(scaffoldRun.code).toBe(0);
      expect(scaffoldRun.out).toContain(`todo:   replace the placeholder images before publishing: ${PLACEHOLDERS.join(", ")}\n`);

      // every placeholder is on disk, marked, and named by the source
      const md = readFileSync(join(packet, "sample.md"), "utf8");
      for (const p of PLACEHOLDERS) {
        const svg = readFileSync(join(packet, p), "utf8");
        expect(svg.startsWith("<svg ")).toBe(true);
        expect(svg).toContain("PLACEHOLDER · REPLACE ME");
        expect(svg).toContain(p);
        if (p.startsWith("mockups/")) expect(md).toContain(`](${p} "`);
      }
      expect(md).not.toContain("{{");
      expect(readdirSync(join(packet, "diagrams")).sort()).toEqual(["flow.svg", "redesign.svg", "schema.svg"]);

      // the compile: nothing missing, the only warning is the empty prs/ folder
      expect(compile.code).toBe(0);
      const warnings = compile.err.split("\n").filter((l) => l.startsWith("warning:"));
      expect(warnings).toHaveLength(1);
      expect(warnings[0]).toContain("no pr-NN.md files");
      expect(compile.out).toContain("7 files (0 missing)");
      const manifest = JSON.parse(readFileSync(join(packet, "build", "content.files.json"), "utf8"));
      expect(manifest.missing).toEqual([]);
      expect(Object.keys(manifest.files).sort()).toEqual(PLACEHOLDERS);
      const content = JSON.parse(readFileSync(join(packet, "build", "content.json"), "utf8"));
      const decisions = content.decisions.map((d: { id: string; state: string; status: string }) => [d.id, d.state, d.status]);
      expect(decisions).toEqual([["Q1", "open", "Open · waiting on <owner>"], ["D1", "done", `<Owner>, ${TODAY}`]]);
      expect(content.refs.unlinked).toEqual([]);

      // both linters, plain and --strict: clean, and the same report
      for (const strict of [[], ["--strict"]]) {
        const scratch = join(T, `lint-${impl}${strict.length ? "-strict" : ""}`);
        mkdirSync(scratch, { recursive: true });
        const p = pyLint([packet, ...strict], T, scratch, { env });
        const t = tsLint([packet, ...strict], T, scratch, { env });
        expect(p.err).toBe("");
        expect(p.code).toBe(0);
        expect(p.out).toContain("\n0 errors, 0 warnings\n");
        const lint = JSON.parse(p.json!);
        expect(lint.findings).toEqual([]);
        expect([lint.counts.errors, lint.counts.warnings]).toEqual([0, 0]);
        expect(Object.values(lint.counts.by_rule).every((n) => n === 0)).toBe(true);
        // PARITY really compared the two compilers (not a skip note)
        expect(lint.notes.some((n: string) => n.startsWith("PARITY:") && n.includes("reproduced build/content.json byte for byte"))).toBe(true);
        expect(t.code).toBe(p.code);
        expect(t.out).toBe(p.out);
        expect(t.err).toBe(p.err);
        expect(t.json).toBe(p.json);
      }
    }, 120_000);
  }
});
