// PR 13 acceptance: from the same content.json and tdd.html, the TypeScript agents lane and strip equal the Python's
// byte for byte (file list and sha256 per file), and --check prints the same problem list on fresh and hand-edited lanes.
import { describe, expect, test } from "bun:test";
import { appendFileSync, copyFileSync, cpSync, mkdirSync, readFileSync, readdirSync, rmSync, unlinkSync, writeFileSync } from "node:fs";
import { basename, dirname, join } from "node:path";
import { HAVE_PYTHON, PY_AGENTS, PY_STRIP, builds, label, pages, py, tmp, tree, treeDiff, ts } from "./harness";

const BUILDS = HAVE_PYTHON ? builds() : [];
const PAGES = HAVE_PYTHON ? pages() : [];
const T = 60_000;

describe.skipIf(!BUILDS.length)("agents lane parity", () => {
  for (const b of BUILDS) {
    for (const opts of [[], ["--link-images", "--budget-tokens", "3000"]]) {
      test(`${label(b)} ${opts.join(" ") || "(defaults)"}: identical tree, summary line and warnings`, () => {
        const d = tmp("lane");
        try {
          const p = py(PY_AGENTS, [b, "--out", join(d, "py"), ...opts]);
          const t = ts("agents", [b, "--out", join(d, "ts"), ...opts]);
          expect(p.code).toBe(0);
          expect(t.code).toBe(0);
          const pt = tree(join(d, "py")), tt = tree(join(d, "ts"));
          expect(pt.size).toBeGreaterThan(20);
          expect(treeDiff(pt, tt)).toEqual([]);
          expect(t.out.replaceAll(join(d, "ts"), "OUT")).toBe(p.out.replaceAll(join(d, "py"), "OUT"));
          expect(t.err).toBe(p.err);
        } finally {
          rmSync(d, { recursive: true, force: true });
        }
      }, T);
    }

    // Paths the packets do not take as built: schema read raw from the packet, no tdd.html and no manifest, bare meta.
    const VARIANTS: Record<string, string> = {
      "no schema in content.json (packet schema/ fallback)": "c.pop('schema', None)",
      "no tdd.html, no content.files.json": "",
      "bare meta, no lede, refs or generated": "c['meta'] = {'status': 'draft'}; c.pop('lede', None); c.pop('refs', None); c.pop('generated', None)",
    };
    for (const [why, edit] of Object.entries(VARIANTS)) {
      test(`${label(b)}: ${why}`, () => {
        const d = tmp("variant");
        try {
          const vb = join(d, "build");
          cpSync(b, vb, { recursive: true, filter: (src) => !["agents", "eval"].includes(basename(src)) || dirname(src) !== b });
          if (edit) {
            const r = Bun.spawnSync(["python3", "-I", "-B", "-c", `import json, sys\np = sys.argv[1]\nc = json.load(open(p, encoding='utf-8'))\n${edit}\njson.dump(c, open(p, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)`, join(vb, "content.json")]);
            expect(r.exitCode).toBe(0);
          } else {
            rmSync(join(vb, "tdd.html"));
            rmSync(join(vb, "content.files.json"), { force: true });
          }
          const packet = dirname(b);
          const p = py(PY_AGENTS, [vb, "--packet", packet, "--out", join(d, "py")]);
          const t = ts("agents", [vb, "--packet", packet, "--out", join(d, "ts")]);
          expect(p.code).toBe(0);
          expect(t.code).toBe(0);
          expect(treeDiff(tree(join(d, "py")), tree(join(d, "ts")))).toEqual([]);
          expect(t.out.replaceAll(join(d, "ts"), "OUT")).toBe(p.out.replaceAll(join(d, "py"), "OUT"));
          expect(t.err).toBe(p.err);
        } finally {
          rmSync(d, { recursive: true, force: true });
        }
      }, T);
    }

    test(`${label(b)}: tdd.html fallback lane (no content.json) is identical`, () => {
      const d = tmp("fallback");
      try {
        for (const side of ["py", "ts"]) {
          mkdirSync(join(d, side, "build"), { recursive: true });
          copyFileSync(join(b, "tdd.html"), join(d, side, "build", "tdd.html"));
        }
        const p = py(PY_AGENTS, [join(d, "py", "build")]);
        const t = ts("agents", [join(d, "ts", "build")]);
        expect(t.code).toBe(p.code);
        expect(treeDiff(tree(join(d, "py", "build", "agents")), tree(join(d, "ts", "build", "agents")))).toEqual([]);
        expect(t.out.replaceAll(join(d, "ts"), "D")).toBe(p.out.replaceAll(join(d, "py"), "D"));
      } finally {
        rmSync(d, { recursive: true, force: true });
      }
    }, T);

    test(`${label(b)}: --check gives the same problems on a fresh lane and on hand-edited ones`, () => {
      const d = tmp("check");
      const out = join(d, "agents");
      try {
        expect(py(PY_AGENTS, [b, "--out", out]).code).toBe(0);
        const same = (why: string, expectCode: number) => {
          const p = py(PY_AGENTS, [b, "--check", "--out", out]);
          const t = ts("agents", [b, "--check", "--out", out]);
          expect({ why, code: t.code, out: t.out, err: t.err }).toEqual({ why, code: p.code, out: p.out, err: p.err });
          expect(p.code).toBe(expectCode);
          return p.out;
        };
        expect(same("fresh", 0)).toContain("agents lane current:");
        const slice = readdirSync(join(out, "sections")).sort()[0]!;
        appendFileSync(join(out, "sections", slice), "hand edit\n");
        unlinkSync(join(out, "refs.json"));
        writeFileSync(join(out, "zz-stray.md"), "x\n");
        writeFileSync(join(out, "sql", "aa-stray.sql"), "y\n");
        mkdirSync(join(out, "new", "deep"), { recursive: true });
        writeFileSync(join(out, "new", "deep", "q.txt"), "z\n");
        const edited = same("hand-edited", 1);
        expect(edited).toContain(`agents/sections/${slice}: changed since it was generated (hand edit?)`);
        expect(edited).toContain("agents/refs.json: listed in tokens.json but missing");
        expect(edited).toContain("agents/new/deep/q.txt: not generated by write_agents.py");
        const readme = join(out, "README.md");
        writeFileSync(readme, readFileSync(readme, "utf8").replace(/content \S+/, "content 2020-01-01T00:00:00Z").replace("write_agents/1", "write_agents/0"));
        expect(same("stale header", 1)).toContain("README header has 'content 2020-01-01T00:00:00Z'");
        unlinkSync(join(out, "tokens.json"));
        expect(same("no tokens.json", 1)).toContain("tokens.json: missing or unreadable");
        unlinkSync(readme);
        expect(same("no README", 1)).toContain("README.md: missing");
      } finally {
        rmSync(d, { recursive: true, force: true });
      }
    }, T);

    test(`${label(b)}: --check on the build's own lane matches (read-only)`, () => {
      const p = py(PY_AGENTS, [b, "--check"]);
      const t = ts("agents", [b, "--check"]);
      expect({ code: t.code, out: t.out, err: t.err }).toEqual({ code: p.code, out: p.out, err: p.err });
    }, T);
  }
});

describe.skipIf(!PAGES.length)("strip_page parity", () => {
  for (const page of PAGES) {
    test(`${label(page)}: plain.html, plain.txt, outline.json and the summary line are identical`, () => {
      const d = tmp("strip");
      try {
        const p = py(PY_STRIP, [page, "--out-dir", join(d, "py")]);
        const t = ts("strip", [page, "--out-dir", join(d, "ts")]);
        expect(p.code).toBe(0);
        expect(t.code).toBe(0);
        const pt = tree(join(d, "py"));
        expect([...pt.keys()]).toEqual(["outline.json", "plain.html", "plain.txt"]);
        expect(treeDiff(pt, tree(join(d, "ts")))).toEqual([]);
        expect(t.out.replaceAll(join(d, "ts"), "OUT")).toBe(p.out.replaceAll(join(d, "py"), "OUT"));
      } finally {
        rmSync(d, { recursive: true, force: true });
      }
    }, T);
  }
});

describe.skipIf(!HAVE_PYTHON)("command-line errors match", () => {
  const cases: [string, string[]][] = [
    ["usage", []], ["unknown option", ["x", "--bogus"]], ["no such directory", ["/nonexistent-tdd-dir"]], ["missing value", ["x", "--out"]],
  ];
  for (const [why, args] of cases) {
    test(`agents: ${why}`, () => {
      const p = py(PY_AGENTS, args), t = ts("agents", args);
      expect({ code: t.code, out: t.out, err: t.err }).toEqual({ code: p.code, out: p.out, err: p.err });
    });
  }
  for (const [why, args] of [["usage", []], ["unknown option", ["x", "--bogus"]], ["no such file", ["/nonexistent-tdd.html"]], ["missing value", ["x", "--out-dir"]]] as [string, string[]][]) {
    test(`strip: ${why}`, () => {
      const p = py(PY_STRIP, args), t = ts("strip", args);
      expect({ code: t.code, out: t.out, err: t.err }).toEqual({ code: p.code, out: p.out, err: p.err });
    });
  }
  test("agents: a build with neither content.json nor tdd.html, and an --out that is not a lane", () => {
    const d = tmp("err");
    try {
      mkdirSync(join(d, "empty"));
      for (const args of [[join(d, "empty")], [join(d, "empty"), "--check"]]) {
        const p = py(PY_AGENTS, args), t = ts("agents", args);
        expect({ code: t.code, out: t.out, err: t.err }).toEqual({ code: p.code, out: p.out, err: p.err });
      }
      const b = BUILDS[0];
      if (b) {
        mkdirSync(join(d, "notlane"));
        writeFileSync(join(d, "notlane", "keep.txt"), "keep\n");
        const args = [b, "--out", join(d, "notlane")];
        const p = py(PY_AGENTS, args), t = ts("agents", args);
        expect({ code: t.code, err: t.err }).toEqual({ code: p.code, err: p.err });
        expect(p.code).toBe(1);
      }
    } finally {
      rmSync(d, { recursive: true, force: true });
    }
  }, T);
});
