// ev_artifact.py: OUTPUT 1, the artifact page (build/tdd.html and the content.json it renders): contract, hydration,
// lint, linking, citations, structure, decisions, diagrams, schema coverage, reading time, owner review notes. The
// three tool scorers (artifact_check, check_hydrate, lint) are Effects on the Tools service so the program can run
// them side by side; the content scorers are plain functions.
import { mkdirSync, readdirSync, rmSync, statSync } from "node:fs";
import { Effect } from "effect";
import * as py from "../py";
import { pyStr } from "../py/repr";
import { TOKEN } from "../vocab";
import {
  type Py, PAGE_LIMIT, SCRIPTS, SKILL, get, getsize, list, oneLine, pyInt, readJson, readText, ratio, sqlFences, stripTags, tail, walk,
} from "./common";
import { type Input } from "./capture";
import { type Output, type ScoreRec, metric, notRun, output, score } from "./score";
import { Tools } from "./tools";

const re = py.re;
const path = py.path;

const COMPILER_LINK_KEYS = new Set(["html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback"]);
type Slot = [string, Set<string>, string];
/** Mirror of lint_packet.Lint.SLOTS (the house six); house_slots() reads the linter's own list when it can. */
export const HOUSE_SLOTS: Slot[] = [
  ["the redesign (overview)", new Set(["redesign", "overview"]), String.raw`\b(redesign|overview)\b`],
  ["front end", new Set(["frontend", "front-end"]), String.raw`\bfront[\s-]?end\b`],
  ["rule coverage or requirements", new Set(["coverage", "requirements", "rules"]), String.raw`\b(coverage|requirements)\b`],
  ["end-to-end flow", new Set(["flow", "end-to-end", "end-to-end-flow", "e2e"]), String.raw`\bend[\s-]to[\s-]end\b`],
  ["decisions and open questions", new Set(["questions", "decisions", "open-questions"]), String.raw`\bdecisions?\b|\bopen questions\b`],
  ["for agents", new Set(["more", "agents", "for-agents"]), String.raw`\bfor agents\b`],
];
const DDL_NAME = re.compile(String.raw`(?i)\b(?:CREATE\s+(?:UNLOGGED\s+)?TABLE(?:\s+IF\s+NOT\s+EXISTS)?|ALTER\s+TABLE(?:\s+IF\s+EXISTS)?(?:\s+ONLY)?)\s+("?[\w.]+"?)`);
const HYDRATE_LINE = re.compile(String.raw`console errors (\d+) \| console warnings (\d+) \(hydration (\d+)\) \| failed steps (\d+)`);

/** The tdd-skill venv's interpreter (references/eval-loop.md, Setup), the documented way to run eval_packet.py. */
export const VENV_PYTHON = "~/.local/share/tdd-skill/venv/bin/python";

/**
 * The interpreter the Python eval uses for lint_packet.py and pytest: its own `sys.executable`, an absolute path. The
 * TypeScript has no interpreter of its own, so it takes the one the Python eval would have been run with: $TDD_PYTHON
 * when set, else the tdd-skill venv's python when it exists, else python3 on PATH; each resolved on PATH to an absolute
 * path, as sys.executable is (symlinks kept).
 */
export function python(env: Record<string, string | undefined> = process.env): string {
  const home = env.HOME || "";
  const venv = home ? path.join(home, VENV_PYTHON.slice(2)) : "";
  const pick = env.TDD_PYTHON || (venv && py.io.isfile(venv) ? venv : "python3");
  if (path.isabs(pick)) return pick;
  return Bun.which(pick, { PATH: env.PATH ?? "" }) ?? pick;
}

/** Mirror of build_tdd.sd_root(): the first candidate that has scripts/artifact_check.mjs. */
export function artifactCheckPath(): string | null {
  const candidates = [process.env.SYSTEM_DESIGN_SKILL || null, path.normpath(path.join(SKILL, "..", "system-design")),
    path.expanduser("~/.claude/skills/system-design"), path.join(SKILL, "vendor", "system-design")];
  for (const c of candidates) {
    const p = c ? path.join(c, "scripts", "artifact_check.mjs") : null;
    if (p && py.io.isfile(p)) return p;
  }
  return null;
}

/** glob.glob(os.path.join(d, "*" + suffix)) in the file system's order. */
function glob(d: string, suffix: string): string[] {
  let names: string[];
  try {
    names = readdirSync(d);
  } catch {
    return [];
  }
  return names.filter((nm) => !nm.startsWith(".") && nm.endsWith(suffix)).map((nm) => path.join(d, nm));
}

export function sPage(page: string): ScoreRec[] {
  const src = "build/tdd.html";
  if (!py.io.isfile(page))
    return [score("page_exists", 0, "os.path.isfile(build/tdd.html)", src), notRun("page_size", "no page", "bytes <= 16 MB (16*1024*1024)", src)];
  const b = getsize(page);
  return [score("page_exists", 1, "os.path.isfile(build/tdd.html)", src),
    score("page_size", b <= PAGE_LIMIT ? 1 : 0, "1 when page bytes <= 16 MB (16*1024*1024), else 0", src, { bytes: b, limit: PAGE_LIMIT })];
}

export const AC = { name: "artifact_check", method: "node artifact_check.mjs <page> <out>/shots: exit 0 -> 1, exit 1 -> 0, exit 2 (bad usage, no Playwright) -> not run",
  source: "<system-design skill>/scripts/artifact_check.mjs" } as const;

export const sArtifactCheck = (page: string, out: string, noShots: boolean, skip: ReadonlySet<string>) => Effect.gen(function* () {
  const { name, method, source: src } = AC;
  if (skip.has(name)) return notRun(name, "skipped (--skip)", method, src);
  if (!py.io.isfile(page)) return notRun(name, "no build/tdd.html", method, src);
  const tools = yield* Tools;
  const tool = artifactCheckPath();
  const node = tools.which("node");
  if (!tool || !node) return notRun(name, !tool ? "artifact_check.mjs absent" : "node not on PATH", method, src);
  const shots = path.join(out, "shots");
  mkdirSync(shots, { recursive: true });
  const r = yield* tools.run([node, tool, page, shots, ...(noShots ? ["--no-shots"] : [])], { timeout: 600 });
  const reps = glob(shots, "-report.json").map((p, i) => [p, statSync(p).mtimeMs, i] as const)
    .sort((a, b) => a[1] - b[1] || a[2] - b[2]).map((x) => x[0]);
  const report = reps.length ? readJson(reps[reps.length - 1]!) : null;
  const m: Record<string, Py> = { exit: r.code, seconds: r.seconds };
  if (py.truthy(report)) Object.assign(m, { renders: list(get(report, "renders", [])).length, failures: list(get(report, "failures", [])).length,
    warnings: list(get(report, "warnings", [])).length });
  const detail = {
    tail: tail(r.out + "\n" + r.err, 14), report: reps.length ? path.relpath(reps[reps.length - 1]!, out) : null,
    failures: list(get(py.truthy(report) ? report : new Map(), "failures", [])).slice(0, 20),
    shots: py.sorted(glob(shots, ".png").map((p) => path.relpath(p, out))),
  };
  if (r.code === 0) return score(name, 1, method, src, m, detail);
  if (r.code === 1) return score(name, 0, method, src, m, detail);
  return notRun(name, oneLine(r.err || "exit 2", 160) || "exit 2", method, src, m, detail);
});

export const HY = { name: "check_hydrate", method: "bun run app/check_hydrate.ts <page> (jsdom hydrate + interactions): exit 0 -> 1, exit 1 -> 0, exit 2 -> not run",
  source: "app/check_hydrate.ts" } as const;

export const sHydrate = (page: string, skip: ReadonlySet<string>) => Effect.gen(function* () {
  const { name, method, source: src } = HY;
  if (skip.has(name)) return notRun(name, "skipped (--skip)", method, src);
  const tool = path.join(SKILL, "app", "check_hydrate.ts");
  const app = path.join(SKILL, "app");
  if (!py.io.isfile(page)) return notRun(name, "no build/tdd.html", method, src);
  if (!py.io.isfile(tool)) return notRun(name, "app/check_hydrate.ts absent", method, src);
  const tools = yield* Tools;
  const bun = tools.which("bun");
  if (!bun) return notRun(name, "bun not on PATH", method, src);
  if (!py.io.isdir(path.join(app, "node_modules"))) return notRun(name, "app/node_modules absent (bun install)", method, src);
  const r = yield* tools.run([bun, "run", tool, page], { cwd: app, timeout: 300 });
  const m: Record<string, Py> = { exit: r.code, seconds: r.seconds };
  const hit = HYDRATE_LINE.search(r.out);
  if (hit) ["console_errors", "console_warnings", "hydration_warnings", "failed_steps"].forEach((k, i) => (m[k] = py.int(hit.group(i + 1)!)));
  const detail = { tail: tail(r.out + "\n" + r.err, 12) };
  if (r.code === 0) return score(name, 1, method, src, m, detail);
  if (r.code === 1) return score(name, 0, method, src, m, detail);
  return notRun(name, oneLine(r.err, 160) || `exit ${pyStr(r.code)}`, method, src, m, detail);
});

export const LINT = { name: "lint", source: "scripts/lint_packet.py",
  method: "python3 -I scripts/lint_packet.py <packet> --build <build> --json <out>/lint.json; value = rules with zero error-severity findings / rules that reported (STRUCT, DNUM, REFS, CITES, DIAG, SCHEMA, FILES, META, AGENTS, XDOC)" } as const;

export const sLint = (packet: string, build: string, out: string, skip: ReadonlySet<string>) => Effect.gen(function* () {
  const { name, method, source: src } = LINT;
  if (skip.has(name)) return notRun(name, "skipped (--skip)", method, src);
  const tool = path.join(SCRIPTS, "lint_packet.py");
  if (!py.io.isfile(tool)) return notRun(name, "scripts/lint_packet.py absent", method, src);
  const jpath = path.join(out, "lint.json");
  if (py.io.exists(jpath)) rmSync(jpath);
  const tools = yield* Tools;
  const r = yield* tools.run([python(), "-I", tool, packet, "--build", build, "--json", jpath], { timeout: 600 });
  const data = readJson(jpath);
  if (!py.truthy(data))
    return notRun(name, `no JSON written (exit ${pyStr(r.code)}): ${oneLine(r.err || r.out, 160)}`, method, src, { exit: r.code });
  const findings = list(get(data, "findings", []));
  const counts = py.truthy(get(data, "counts")) ? get(data, "counts") : new Map();
  const byRuleKeys = py.truthy(get(counts, "by_rule", new Map())) ? [...(get(counts, "by_rule") as Map<string, Py>).keys()] : [];
  const rules = py.sorted(new Set([...findings.map((f) => get(f, "rule") as string), ...byRuleKeys]));
  const byRule = new Map<string, { errors: number; warnings: number }>();
  for (const rl of rules) {
    const fs = findings.filter((f) => get(f, "rule") === rl);
    byRule.set(rl, { errors: fs.filter((f) => get(f, "severity") === "error").length, warnings: fs.filter((f) => get(f, "severity") !== "error").length });
  }
  const clean = [...byRule.values()].filter((v) => v.errors === 0).length;
  const m = new Map<string, Py>([["exit", r.code], ["errors", get(counts, "errors")], ["warnings", get(counts, "warnings")],
    ["rules", byRule.size], ["rules_clean", clean], ["seconds", r.seconds]]);
  for (const [rl, v] of byRule) {
    m.set(`${rl}.errors`, v.errors);
    m.set(`${rl}.warnings`, v.warnings);
  }
  const or = (v: Py) => (py.truthy(v) ? pyStr(v) : "");
  const detail = {
    by_rule: byRule,
    errors: findings.filter((f) => get(f, "severity") === "error")
      .map((f) => py.cpSlice(`${pyStr(get(f, "check"))} ${or(get(f, "file"))}:${or(get(f, "line"))} ${pyStr(get(f, "message"))}`, 0, 240)).slice(0, 40),
    notes: list(get(data, "notes", [])).slice(0, 20), json: "lint.json",
  };
  return score(name, byRule.size ? ratio(clean, byRule.size) : 1, method, src, m, detail);
});

export function unlinkedOccurrences(content: Py, tokens: Py[]): Map<Py, number> {
  const counts = new Map<Py, number>(tokens.map((t) => [t, 0]));
  if (!tokens.length) return counts;
  const visit = (node: Py): void => {
    if (node instanceof Map) {
      for (const [k, v] of node) {
        if (COMPILER_LINK_KEYS.has(k) && typeof v === "string" && v) {
          const txt = re.sub(String.raw`(?s)<a\b[^>]*>.*?</a>|<code\b[^>]*>.*?</code>`, " ", v);
          for (const m of TOKEN.finditer(stripTags(txt))) {
            const t = re.sub(String.raw`\s+`, " ", m.group(1)!);
            if (counts.has(t)) counts.set(t, counts.get(t)! + 1);
          }
        } else if ((v instanceof Map || Array.isArray(v)) && k !== "text") visit(v);
      }
    } else if (Array.isArray(node)) for (const x of node) visit(x);
  };
  visit(get(content, "sections", []));
  visit(get(content, "lede", []));
  return counts;
}

export function sRefs(content: Py): ScoreRec {
  const refs = py.truthy(get(content, "refs")) ? get(content, "refs") : new Map();
  const linked = pyInt(py.truthy(get(refs, "linked")) ? get(refs, "linked") : 0);
  const unlinked = py.truthy(get(refs, "unlinked")) ? [...get(refs, "unlinked")] : [];
  const occ = unlinkedOccurrences(content, unlinked);
  const sumOcc = [...occ.values()].reduce((a, b) => a + b, 0);
  const nUn = sumOcc || unlinked.length;
  const method = "linked / (linked + unlinked occurrences); linked = content.refs.linked (links the compiler wrote); unlinked occurrences = " +
    "reference tokens listed in content.refs.unlinked counted in the compiler's linkable HTML strings outside <a> and <code> " +
    "(falls back to the distinct count when none is found)";
  return score("refs_linked", linked + nUn ? ratio(linked, linked + nUn) : 1, method, "content.json refs + sections",
    { linked, unlinked_tokens: unlinked.length, unlinked_occurrences: nUn }, { unlinked: occ });
}

export function sCites(content: Py): ScoreRec {
  const cites = list(get(content, "cites"));
  const href = cites.filter((c) => py.truthy(get(c, "href"))).length;
  const noLine = cites.filter((c) => !py.truthy(get(c, "from"))).length;
  const withLine = cites.length - noLine;
  return score("cites_href", cites.length ? ratio(href, cites.length) : 1,
    "citations with an href (repo_url/blob/sha/path#Lx) / all citations; a citation without a line range gets no href by design (metrics split it out)",
    "content.json cites", { total: cites.length, with_href: href, without_href: cites.length - href, without_line: noLine,
      line_bearing_with_href: href, line_bearing: withLine });
}

/** Lint.SLOTS read from lint_packet.py's own source (the Python eval imports the module); the mirror when it cannot. */
export function houseSlots(): [Slot[], string] {
  const mirror: [Slot[], string] = [HOUSE_SLOTS, "ev_artifact.HOUSE_SLOTS (mirror of lint_packet.Lint.SLOTS)"];
  const src = readText(path.join(SCRIPTS, "lint_packet.py"));
  if (src === null) return mirror;
  const lines = py.str.splitlines(src);
  const start = lines.findIndex((l) => re.match(String.raw`\s+SLOTS = \[\s*$`, l) !== null);
  if (start < 0) return mirror;
  const slots: Slot[] = [];
  const row = re.compile(String.raw`\s*\("((?:[^"\\]|\\.)*)", \{([^}]*)\}, r"([^"]*)"\),?\s*$`);
  for (const l of lines.slice(start + 1)) {
    if (re.match(String.raw`\s*\]\s*$`, l)) return slots.length ? [slots, "lint_packet.Lint.SLOTS"] : mirror;
    const m = row.match(l);
    if (!m) return mirror;
    const ids = m.group(2)!.split(",").map((x) => py.str.strip(x)).filter((x) => x);
    if (!ids.every((x) => re.fullmatch(String.raw`"[^"\\]*"`, x))) return mirror;
    slots.push([m.group(1)!, new Set(ids.map((x) => x.slice(1, -1))), m.group(3)!]);
  }
  return mirror;
}

export function sSections(content: Py): ScoreRec {
  const [slots, origin] = houseSlots();
  const secs = list(get(content, "sections", []));
  const used = new Set<Py>();
  const hits: Record<string, Py>[] = [];
  for (const [name, ids, rx] of slots) {
    const hit = secs.find((s) => !used.has(get(s, "id")) && ids.has(get(s, "id"))) ??
      secs.find((s) => !used.has(get(s, "id")) && re.search(rx, py.lower(py.truthy(get(s, "titleText")) ? get(s, "titleText") : "")) !== null) ?? null;
    if (hit) used.add(get(hit, "id"));
    hits.push({ slot: name, section: hit ? get(hit, "id") : null });
  }
  const present = hits.filter((h) => py.truthy(h.section)).length;
  return score("sections_house", ratio(present, slots.length), `house sections matched by id or title regex / ${slots.length} (slots from ${origin})`,
    "content.json sections", { present, required: slots.length, sections: secs.length }, hits);
}

export function sDecisions(content: Py): ScoreRec {
  const ds = list(get(content, "decisions"));
  const withStatus = ds.filter((d) => py.str.strip(py.truthy(get(d, "status")) ? get(d, "status") : "") !== "").length;
  const states = new Map<Py, number>();
  for (const d of ds) {
    const k = py.truthy(get(d, "state")) ? get(d, "state") : "none";
    states.set(k, (states.get(k) ?? 0) + 1);
  }
  const m = new Map<string, Py>([["total", ds.length], ["with_status", withStatus]]);
  for (const [k, v] of states) m.set(`state_${pyStr(k)}`, v);
  if (!ds.length) return notRun("decisions_status", "no decisions in content.json", "decisions with a non-empty status / decisions", "content.json decisions", m);
  return score("decisions_status", ratio(withStatus, ds.length), "decisions with a non-empty status / decisions (states counted as metrics)", "content.json decisions", m);
}

export function sDiagrams(content: Py): ScoreRec {
  const ds = [...walk(get(content, "sections", []), new Set(["diagram"]))];
  if (!ds.length) return notRun("diagram_hotspots", "no diagrams", "diagrams with >= 1 hotspot / diagrams", "content.json diagram nodes");
  const annotated = ds.filter((d) => py.truthy(get(d, "hotspots")));
  const hs = ds.flatMap((d) => list(get(d, "hotspots", [])));
  return score("diagram_hotspots", ratio(annotated.length, ds.length), "diagrams with >= 1 hotspot / diagrams", "content.json diagram nodes",
    { diagrams: ds.length, annotated: annotated.length, hotspots: hs.length, with_table: hs.filter((h) => py.truthy(get(h, "table"))).length,
      with_note: hs.filter((h) => py.truthy(get(h, "note"))).length, with_image: hs.filter((h) => py.truthy(get(h, "image"))).length },
    ds.map((d) => ({ key: get(d, "key"), hotspots: list(get(d, "hotspots", [])).length })));
}

export function ddlTables(sql: Py): Set<string> {
  const out = new Set<string>();
  for (const m of DDL_NAME.finditer(py.truthy(sql) ? sql : "")) out.add(py.lower(py.str.strip(m.group(1)!, '"').split(".").at(-1)!));
  return out;
}

export function sSchema(content: Py): ScoreRec {
  const tables = list(get(content, "schema"));
  const fenceNames = new Set<string>();
  for (const f of sqlFences(content)) for (const t of ddlTables(get(f, "text"))) fenceNames.add(t);
  const ddlNames = new Set(fenceNames);
  for (const t of tables) for (const x of ddlTables(get(t, "ddl"))) ddlNames.add(x);
  const have = new Set(tables.map((t) => py.lower(py.truthy(get(t, "table")) ? get(t, "table") : "")));
  const method = "tables named in DDL (CREATE/ALTER TABLE in sql fences and schema[].ddl) that have a schema card / tables named in DDL";
  const covered = [...ddlNames].filter((x) => have.has(x)).length;
  const m = { schema_tables: tables.length, ddl_tables: ddlNames.size, covered, fence_ddl_tables: fenceNames.size,
    new: tables.filter((t) => get(t, "state") === "new").length, changed: tables.filter((t) => get(t, "state") === "changed").length,
    existing: tables.filter((t) => get(t, "state") === "existing").length };
  if (!ddlNames.size) return notRun("schema_ddl", "no DDL on the page", method, "content.json schema + sql fences", m);
  return score("schema_ddl", ratio(covered, ddlNames.size), method, "content.json schema + sql fences", m,
    { uncovered: py.sorted([...ddlNames].filter((x) => !have.has(x))) });
}

export function sReading(content: Py, inp: Input): ScoreRec {
  const st = py.truthy(get(content, "stats")) ? get(content, "stats") : new Map();
  const m: [string, Py][] = [["reading_minutes", get(st, "readingMinutes")], ["words", get(st, "words")], ["sections", get(st, "sections")],
    ["diagrams", get(st, "diagrams")], ["prs", get(st, "prs")], ["decisions", get(st, "decisions")]];
  if (inp.source) m.push(["source_words", inp.source.words]);
  return metric("reading", new Map(m.filter(([, v]) => v !== null)), "content.json stats (words / 230 per minute, as build_tdd computes it)", "content.json stats");
}

export function sOwnerReview(inp: Input, target: string): ScoreRec {
  const notes = inp.labels.review_notes.filter((r) => get(r, "target") === target);
  const method = "owner review notes for this target: pass / (pass + fail + needs-work); 'question' notes are counted, not scored";
  const src = inp.labels.review_notes_source || "labels.jsonl";
  if (!notes.length) return notRun("owner_review", "no review notes for this target (--labels)", method, src);
  const p = notes.filter((r) => get(r, "verdict") === "pass").length;
  const f = notes.filter((r) => ["fail", "needs-work"].includes(get(r, "verdict"))).length;
  const q = notes.filter((r) => get(r, "verdict") === "question").length;
  const m = { notes: notes.length, pass: p, fail_or_needs_work: f, question: q };
  if (p + f === 0) return notRun("owner_review", "only question notes", method, src, m);
  return score("owner_review", ratio(p, p + f), method, src, m,
    notes.map((r) => ({ anchor: get(r, "anchor"), verdict: get(r, "verdict"), note: get(r, "note"), date: get(r, "date") })));
}

export const CONTENT_SCORERS = ["refs_linked", "cites_href", "sections_house", "decisions_status", "diagram_hotspots", "schema_ddl"];

/** ev_artifact.evaluate from the settled tool records and the judge's records, in the Python's order. */
export function assemble(page: string, content: Py, inp: Input, tools: { ac: ScoreRec; hy: ScoreRec; lint: ScoreRec }, judge: ScoreRec[]): Output {
  const scores = sPage(page);
  scores.push(tools.ac, tools.hy, tools.lint);
  if (py.truthy(content))
    scores.push(sRefs(content), sCites(content), sSections(content), sDecisions(content), sDiagrams(content), sSchema(content), sReading(content, inp));
  else for (const nm of CONTENT_SCORERS) scores.push(notRun(nm, "no build/content.json", "see references/eval-loop.md", "content.json"));
  scores.push(...judge, sOwnerReview(inp, "artifact"));
  return output("artifact", inp.packet.key, scores);
}

