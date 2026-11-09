// A fresh packet lints with only its placeholder errors (N14, N15): new_packet.py and tdd new each scaffold a packet, the
// matching compiler builds it (build_tdd.py, tdd build), and both linters then report the scaffold's placeholders and
// nothing else (FILES/placeholder for each placeholder image, FILES/placeholder-text for each line still carrying a
// template prompt; expected until the author replaces them), --strict included, with identical stdout and lint.json;
// with those two checks skipped the packet lints clean, and once every placeholder image, prompt and date is replaced
// and the packet rebuilt it lints 0 errors and 0 warnings in both linters. The compile's only warning is the empty
// prs/ folder. Every image the template references is a placeholder the scaffold wrote (templates/packet/), so nothing
// is missing from the publish manifest and nothing under diagrams/ or mockups/ is an orphan. D1 is dated the
// scaffold's day, so it renders decided; Q1 stays open.
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { COMMENT, PLACEHOLDER, PLACEHOLDER_DATE } from "../../src/lint/patterns";
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

describe.skipIf(!HAVE_PYTHON)("a fresh scaffold lints with only its placeholder errors, and clean once they are replaced", () => {
  for (const impl of ["py", "ts"] as const) {
    test(`${impl === "py" ? "new_packet.py + build_tdd.py" : "tdd new + tdd build"}: only the placeholder errors in both linters`, () => {
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

      // both linters, plain and --strict: the placeholder errors only, and the same report; with them skipped, clean
      const SKIP = ["--skip", "FILES/placeholder,FILES/placeholder-text"];
      for (const [tag, extra] of [["plain", []], ["strict", ["--strict"]], ["strict-skip", ["--strict", ...SKIP]]] as const) {
        const scratch = join(T, `lint-${impl}-${tag}`);
        mkdirSync(scratch, { recursive: true });
        const p = pyLint([packet, ...extra], T, scratch, { env });
        const t = tsLint([packet, ...extra], T, scratch, { env });
        expect(p.err).toBe("");
        const lint = JSON.parse(p.json!);
        if (tag === "strict-skip") {
          expect(p.code).toBe(0);
          expect(p.out).toContain("\n0 errors, 0 warnings\n");
          expect(lint.findings).toEqual([]);
          expect([lint.counts.errors, lint.counts.warnings]).toEqual([0, 0]);
          expect(Object.values(lint.counts.by_rule).every((n) => n === 0)).toBe(true);
        } else {
          type F = { check: string; file: string };
          expect(p.code).toBe(1);
          expect([...new Set(lint.findings.map((f: F) => f.check))].sort()).toEqual(["FILES/placeholder", "FILES/placeholder-text"]);
          expect(lint.findings.filter((f: F) => f.check === "FILES/placeholder").map((f: F) => f.file).sort()).toEqual(PLACEHOLDERS.map((p) => `build/${p}`));
          expect(lint.findings.filter((f: F) => f.check === "FILES/placeholder-text" && f.file === "status.md")).toHaveLength(1);
          expect(lint.counts.warnings).toBe(0);
          expect(Object.entries(lint.counts.by_rule).filter(([, n]) => n !== 0).map(([r]) => r)).toEqual(["FILES"]);
        }
        // PARITY really compared the two compilers (not a skip note)
        expect(lint.notes.some((n: string) => n.startsWith("PARITY:") && n.includes("reproduced build/content.json byte for byte"))).toBe(true);
        expect(t.code).toBe(p.code);
        expect(t.out).toBe(p.out);
        expect(t.err).toBe(p.err);
        expect(t.json).toBe(p.json);
      }

      // the author replaces every placeholder: a real image over each placeholder file, the real text for every prompt
      // and date in the source and status.md (all-lower-case notation such as <name> or <n> is not a placeholder)
      for (const p of PLACEHOLDERS) {
        writeFileSync(join(packet, p), `<svg xmlns="http://www.w3.org/2000/svg" width="360" height="240" viewBox="0 0 360 240"><text x="10" y="20">${p}</text></svg>\n`);
      }
      const real = (line: string): string => COMMENT.match(line) ? line : line.split(PLACEHOLDER_DATE).join(TODAY)
        .replace(/<([^<>\n]*)>/g, (m, inner: string) => (/^[a-z]*$/.test(inner) ? m : inner));
      for (const f of ["sample.md", "status.md"]) {
        // twice: one prompt holds another (the step line's `<ALIAS>/<path>.ts:<line>`)
        writeFileSync(join(packet, f), readFileSync(join(packet, f), "utf8").split("\n").map(real).map(real).join("\n"));
        for (const line of readFileSync(join(packet, f), "utf8").split("\n")) if (!COMMENT.match(line)) expect([f, line, PLACEHOLDER.search(line)]).toEqual([f, line, null]);
      }
      const rebuild = impl === "py" ? py("build_tdd.py", ["sample.md", "build"], packet, env) : ts("build", ["sample.md", "build"], packet, env);
      expect(rebuild.code).toBe(0);
      for (const [tag, extra] of [["replaced", []], ["replaced-strict", ["--strict"]]] as const) {
        const scratch = join(T, `lint-${impl}-${tag}`);
        mkdirSync(scratch, { recursive: true });
        const p = pyLint([packet, ...extra], T, scratch, { env });
        const t = tsLint([packet, ...extra], T, scratch, { env });
        expect(p.err).toBe("");
        expect(p.code).toBe(0);
        expect(p.out).toContain("\n0 errors, 0 warnings\n");
        const lint = JSON.parse(p.json!);
        expect(lint.findings).toEqual([]);
        expect([lint.counts.errors, lint.counts.warnings]).toEqual([0, 0]);
        expect(t.code).toBe(p.code);
        expect(t.out).toBe(p.out);
        expect(t.err).toBe(p.err);
        expect(t.json).toBe(p.json);
      }
    }, 120_000);
  }
});
