// The scaffold's placeholders (FILES/placeholder, FILES/placeholder-text). The token list is one list in two linters:
// lint_packet.py's and src/lint/patterns.ts's must be equal, and both must be what a fresh scaffold writes (the
// <angle-bracket> prompts outside one-line comments, all-lower-case one-word notation aside). The pattern must match
// as CPython's does. A fresh scaffold, from either scaffold, then lints with these errors in both linters, byte for
// byte, and with none of them once the placeholders are replaced.
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { COMMENT, PLACEHOLDER, PLACEHOLDER_DATE, PLACEHOLDER_MARK, PLACEHOLDER_PROMPTS } from "../../src/lint/patterns";
import { cpLen } from "../../src/py";
import { py, ts } from "../hotspots/_harness";
import { GENERATED, HAVE_PYTHON, SCRIPTS, SKILL, pyLint, rmrf, tempDir, tsLint } from "./harness";

const dec = new TextDecoder();
function python(code: string, input: string): string {
  const r = Bun.spawnSync(["python3", "-I", "-B", "-c", code], { stdin: new TextEncoder().encode(input), stdout: "pipe", stderr: "pipe" });
  if (r.exitCode !== 0) throw new Error(dec.decode(r.stderr));
  return dec.decode(r.stdout);
}
const LOAD = `import importlib.util, json, sys
spec = importlib.util.spec_from_file_location("lint_packet", ${JSON.stringify(join(SCRIPTS, "lint_packet.py"))})
lp = importlib.util.module_from_spec(spec); spec.loader.exec_module(lp)
`;

/** What the check reads as a prompt: an <angle-bracket> token on a line that is not a one-line comment, unless it is
 * all-lower-case one-word notation (<name>, <path>, <n>). */
function prompts(text: string): string[] {
  const out: string[] = [];
  for (const line of text.split("\n")) {
    if (COMMENT.match(line)) continue;
    for (const m of line.matchAll(/<[^<>\n]*>/g)) if (!/^<[a-z]+>$/.test(m[0]) && !out.includes(m[0])) out.push(m[0]);
  }
  return out;
}

const TODAY = "2026-11-08";
let T = "";
let env: Record<string, string | undefined> = {};
let repo = "";
function git(cwd: string, ...args: string[]) {
  const r = Bun.spawnSync({ cmd: ["git", ...args], cwd, env: { ...process.env, ...env, GIT_AUTHOR_NAME: "C", GIT_AUTHOR_EMAIL: "c@example.com",
    GIT_COMMITTER_NAME: "C", GIT_COMMITTER_EMAIL: "c@example.com", GIT_AUTHOR_DATE: "2026-01-02T03:04:05Z", GIT_COMMITTER_DATE: "2026-01-02T03:04:05Z" } });
  if (r.exitCode !== 0) throw new Error(`git ${args.join(" ")}: ${r.stderr}`);
}
beforeAll(() => {
  T = tempDir("placeholders");
  const home = join(T, "home");
  mkdirSync(home);
  env = { HOME: home, XDG_CONFIG_HOME: join(home, ".config"), GIT_CONFIG_NOSYSTEM: "1", TDD_TODAY: TODAY, SYSTEM_DESIGN_OUT: "",
    TDD_GENERATED: GENERATED, COLUMNS: undefined, LINES: undefined };
  repo = join(T, "repo");
  mkdirSync(repo);
  git(repo, "init", "-q", "-b", "main");
  git(repo, "config", "user.name", "Test Author");
  writeFileSync(join(repo, "f"), "x\n");
  git(repo, "add", "f");
  git(repo, "commit", "-qm", "init");
});
afterAll(() => rmrf(T));

/** Scaffold with one implementation (no --title, so the title is a placeholder too); the packet folder. */
function scaffold(impl: "py" | "ts", slug: string): string {
  const root = join(T, `root-${slug}`);
  const argv = [slug, "--repo", repo, "--root", root];
  const r = impl === "py" ? py("new_packet.py", argv, T, env) : ts("new", argv, T, env);
  if (r.code !== 0) throw new Error(`${impl} scaffold failed: ${r.err}`);
  return join(root, "repo", "system-design", slug);
}

describe.skipIf(!HAVE_PYTHON)("the scaffold's placeholders", () => {
  test("lint_packet.py and src/lint/patterns.ts hold the same list and the same pattern", () => {
    const got = JSON.parse(python(LOAD + `print(json.dumps([lp.PLACEHOLDER_MARK, lp.PLACEHOLDER_DATE, list(lp.PLACEHOLDER_PROMPTS),
      lp.PLACEHOLDER.pattern, lp.COMMENT.pattern]))`, ""));
    expect(got).toEqual([PLACEHOLDER_MARK, PLACEHOLDER_DATE, [...PLACEHOLDER_PROMPTS], PLACEHOLDER.pattern, COMMENT.pattern]);
    expect(new Set(PLACEHOLDER_PROMPTS).size).toBe(PLACEHOLDER_PROMPTS.length);
  });

  test("the pattern matches as CPython's does", () => {
    const lines = [
      "a <Role> · <Moment> and useState<State>, Array<Role>, _<Owner>, é<Moment>, 1<Caption> but (<Role>) and -<Moment>",
      "<YYYY-MM-DD hh:mm> <YYYY-MM-DD> YYYY-MM-DD YYYY-MM-DDT10 xYYYY-MM-DD",
      `${PLACEHOLDER_MARK} and >${PLACEHOLDER_MARK}< and ${PLACEHOLDER_MARK.toLowerCase()}`,
      "<Caption: the state this shot shows> <Caption> <Caption <What the shot shows> <What the shot shows, with the real names and numbers on it>",
      "<name> <path> <n> <owner> <the owner's words> <Today's screen, as it is in production>",
      "𐐀<Role>  <Role>  <Role>",
      ...PLACEHOLDER_PROMPTS.map((t) => `x ${t} y`),
    ];
    const want = JSON.parse(python(LOAD + "print(json.dumps([[[m.start(), m.group(0)] for m in lp.PLACEHOLDER.finditer(l)] for l in json.load(sys.stdin)]))",
      JSON.stringify(lines)));
    // Python counts code points, the kernel's Match UTF-16 units: compare in code points
    const got = lines.map((l) => [...PLACEHOLDER.finditer(l)].map((m) => [cpLen(l.slice(0, m.start())), m.group(0)]));
    expect(got).toEqual(want);
    for (const [i, t] of PLACEHOLDER_PROMPTS.entries()) expect(got[6 + i]).toEqual([[2, t]]);
    const comments = ["<!-- x -->", "  <!-- <Role> -->  ", "<!-- a --> b", "x <!-- y -->", "<!--", "<!-- multi"];
    const wantC = JSON.parse(python(LOAD + "print(json.dumps([bool(lp.COMMENT.match(l)) for l in json.load(sys.stdin)]))", JSON.stringify(comments)));
    expect(comments.map((l) => COMMENT.match(l) !== null)).toEqual(wantC);
  });

  test("the list is what a fresh scaffold writes, and every prompt line of the source is caught", () => {
    const packet = scaffold("py", "drift");
    const md = readFileSync(join(packet, "drift.md"), "utf8");
    const status = readFileSync(join(packet, "status.md"), "utf8");
    expect(prompts(md + "\n" + status)).toEqual([...PLACEHOLDER_PROMPTS]);
    expect(status).toContain(PLACEHOLDER_DATE);
    // every placeholder image the scaffold copies carries the marker
    const tpl = join(SKILL, "templates", "packet");
    for (const sub of readdirSync(tpl)) for (const f of readdirSync(join(tpl, sub))) expect(readFileSync(join(tpl, sub, f), "utf8")).toContain(PLACEHOLDER_MARK);
    // no source line that holds a prompt slips through (a line with only <name>-style notation would)
    for (const line of md.split("\n")) {
      if (COMMENT.match(line) || !/<[^<>\n]*>/.test(line)) continue;
      expect([line, PLACEHOLDER.search(line) !== null]).toEqual([line, true]);
    }
  });

  for (const impl of ["py", "ts"] as const) {
    test(`${impl === "py" ? "new_packet.py" : "tdd new"}: both linters report the placeholders identically, and none once replaced`, () => {
      const slug = `fresh-${impl}`;
      const packet = scaffold(impl, slug);
      const build = () => {
        const r = py("build_tdd.py", [`${slug}.md`, "build"], packet, env);
        expect(r.code).toBe(0);
      };
      const lint = (tag: string) => {
        const scratch = join(T, `lint-${slug}-${tag}`);
        mkdirSync(scratch, { recursive: true });
        const p = pyLint([packet], T, scratch, { env });
        const t = tsLint([packet], T, scratch, { env });
        expect(p.err).toBe("");
        expect([t.code, t.out, t.err, t.json]).toEqual([p.code, p.out, p.err, p.json]);
        return { code: p.code, findings: JSON.parse(p.json!).findings as { check: string; file: string; line: number }[] };
      };
      build();
      const fresh = lint("fresh");
      expect(fresh.code).toBe(1);
      const count = (check: string) => fresh.findings.filter((f) => f.check === check).length;
      expect(count("FILES/placeholder")).toBe(7);
      // every source line with a prompt, the title included, plus status.md's title and its decisions guide line
      const mdLines = readFileSync(join(packet, `${slug}.md`), "utf8").split("\n").filter((l) => !COMMENT.match(l) && PLACEHOLDER.search(l) !== null);
      expect(count("FILES/placeholder-text")).toBe(mdLines.length + 2);
      expect(fresh.findings.filter((f) => f.file === "status.md").map((f) => f.line)).toEqual([1, 18]);
      expect(fresh.findings.every((f) => f.check === "FILES/placeholder" || f.check === "FILES/placeholder-text")).toBe(true);

      // the author replaces every placeholder image and every prompt; the scaffold's notation (<name>, <n>) may stay
      for (const sub of ["diagrams", "mockups"]) {
        for (const f of readdirSync(join(packet, sub))) {
          writeFileSync(join(packet, sub, f), `<svg xmlns="http://www.w3.org/2000/svg" width="360" height="240" viewBox="0 0 360 240"><text x="10" y="20">${sub}/${f}</text></svg>\n`);
        }
      }
      for (const f of [`${slug}.md`, "status.md"]) {
        const real = (line: string) => COMMENT.match(line) ? line : line.split(PLACEHOLDER_DATE).join(TODAY)
          .replace(/<([^<>\n]*)>/g, (m, inner: string) => (/^[a-z]*$/.test(inner) ? m : inner));
        writeFileSync(join(packet, f), readFileSync(join(packet, f), "utf8").split("\n").map(real).map(real).join("\n"));
      }
      build();
      const replaced = lint("replaced");
      expect(replaced.findings.filter((x) => x.check.startsWith("FILES/placeholder"))).toEqual([]);
      expect([replaced.code, replaced.findings]).toEqual([0, []]);
    }, 120_000);
  }
});
