// The Packet service: the packet being linted (lint_packet.Lint's state and plumbing). It holds the source, meta.json,
// the PR files and the build (or a compile of the source into a scoped temporary folder), the page's ids and the house
// slots, the cached source lines, and the findings and notes sink. Rules read it and add to it; nothing here writes
// into the packet.
import { Context, Effect, Scope } from "effect";
import * as py from "../py";
import { Finding, type Severity } from "./finding";
import { MARK } from "./patterns";
import {
  type PyDict, type PyValue, dget, get, has, iter, isDict, isOsOrValueError, norm, nodes, outside_fences, parse_attrs, pyErrText, squash, str, truthy, walk,
  type Attrs,
} from "./pyops";
import { Compiler, Fs, type FsApi } from "./services";

const re = py.re;
const { join, basename, abspath, expanduser, relpath } = py.path;

export interface LintOptions {
  readonly packet: string;
  readonly build?: string | null;
  readonly compile?: boolean;
  readonly skip?: readonly string[];
}

export type Loc = readonly [string | null, number | null];
export type Slot = readonly [string, PyDict | null];

/** The six house slots: [reader's name, ids that match, title pattern]. */
export const SLOTS: readonly (readonly [string, ReadonlySet<string>, string])[] = [
  ["the redesign (overview)", new Set(["redesign", "overview"]), String.raw`\b(redesign|overview)\b`],
  ["front end", new Set(["frontend", "front-end"]), String.raw`\bfront[\s-]?end\b`],
  ["rule coverage or requirements", new Set(["coverage", "requirements", "rules"]), String.raw`\b(coverage|requirements)\b`],
  ["end-to-end flow", new Set(["flow", "end-to-end", "end-to-end-flow", "e2e"]), String.raw`\bend[\s-]to[\s-]end\b`],
  ["decisions and open questions", new Set(["questions", "decisions", "open-questions"]), String.raw`\bdecisions?\b|\bopen questions\b`],
  ["for agents", new Set(["more", "agents", "for-agents"]), String.raw`\bfor agents\b`],
];

export class LintPacket {
  root: string;
  slug: string;
  skip: Set<string>;
  findings: Finding[] = [];
  notes: string[] = [];
  citations: PyDict[] = [];
  build: string;
  compiled = false;
  tmp: string | null = null;
  meta: PyValue | null = null;
  src: string | null = null;
  prs: string[] = [];
  content: PyValue = new Map();
  manifest: PyValue | null = null;
  page_ids: Set<unknown> = new Set();
  slots: Slot[] = [];
  readonly fs: FsApi;
  private readonly _lines = new Map<string, string[]>();
  private readonly _norm = new Map<string, string[]>();

  constructor(fs: FsApi, opts: LintOptions) {
    this.fs = fs;
    this.root = abspath(expanduser(opts.packet));
    this.slug = basename(py.str.rstrip(this.root, "/"));
    this.skip = new Set((opts.skip ?? []).filter((s) => py.str.strip(s)).map((s) => py.str.strip(s)));
    this.build = opts.build ? abspath(expanduser(opts.build)) : join(this.root, "build");
  }

  // -- plumbing
  add(rule: string, check: string, sev: Severity, msg: unknown, file: string | null = null, line: number | null = null): void {
    const cid = `${rule}/${check}`;
    if (this.skip.has(rule) || this.skip.has(cid)) return;
    this.findings.push(new Finding({ rule, check: cid, severity: sev, file, line, message: squash(str(msg)) }));
  }

  /** RULE/check-failed for a rule that raised a defect of this linter. --skip RULE/check-failed does not filter it out:
   * lint_packet.py has no rule isolation, so a rule that raises always fails its run (exit 1); --skip RULE leaves the
   * rule out instead. */
  defect(rule: string, msg: string): void {
    this.findings.push(new Finding({ rule, check: `${rule}/check-failed`, severity: "error", file: null, line: null, message: squash(msg) }));
  }

  note(msg: string): void {
    this.notes.push(squash(msg));
  }

  /** A path relative to the packet, else ~-relative, else absolute; a falsy path as it is. */
  rel(path: string | null): string | null;
  rel(path: unknown): unknown;
  rel(path: unknown): unknown {
    if (!truthy(path)) return path;
    const p = abspath(path as string);
    if (p.startsWith(this.root + "/")) return relpath(p, this.root);
    const home = expanduser("~");
    return p.startsWith(home + "/") ? "~" + p.slice(home.length) : p;
  }

  /** json.load(open(path, encoding="utf-8")), or None with a RULE/missing-file or RULE/bad-json finding. */
  load_json(path: string, rule: string, quiet_missing = false): PyValue | null {
    if (!this.fs.isfile(path)) {
      if (!quiet_missing) this.add(rule, "missing-file", "error", `${this.rel(path)} does not exist`, this.rel(path));
      return null;
    }
    try {
      return py.json.loads(this.fs.readText(path));
    } catch (e) {
      if (!isOsOrValueError(e)) throw e;
      this.add(rule, "bad-json", "error", `${this.rel(path)} is not valid JSON: ${pyErrText(e)}`, this.rel(path));
      return null;
    }
  }

  find_source(): string | null {
    const cand = join(this.root, this.slug + ".md");
    if (this.fs.isfile(cand)) return cand;
    let c: PyValue = null;
    const cpath = join(this.build, "content.json");
    if (this.fs.isfile(cpath)) {
      try {
        c = dget(py.json.loads(this.fs.readText(cpath)), "source");
      } catch (e) {
        if (!isOsOrValueError(e)) throw e;
        c = null;
      }
    }
    if (truthy(c) && typeof c === "string" && this.fs.isfile(join(this.root, c))) return join(this.root, c);
    const mds = py.sorted(this.fs.listdir(this.root)).filter((f) => f.endsWith(".md") && f !== "status.md" && !f.startsWith("review") && !f.startsWith("README"));
    return mds.length === 1 ? join(this.root, mds[0]!) : null;
  }

  lines(path: string): string[] {
    let ls = this._lines.get(path);
    if (ls === undefined) {
      try {
        ls = this.fs.readText(path).split("\n");
      } catch {
        ls = [];
      }
      this._lines.set(path, ls);
      this._norm.set(path, ls.map((l) => norm(l)));
    }
    return ls;
  }

  text_files(): string[] {
    return [...(this.src ? [this.src] : []), ...this.prs];
  }

  /** (relative file, line) of the first line matching a pattern, or containing a literal string. */
  locate(pattern: string | py.re.Pattern, files: string[] | null = null): Loc {
    const test = typeof pattern === "string" ? (l: string) => l.includes(pattern) : (l: string) => pattern.search(l) !== null;
    for (const f of files && files.length ? files : this.text_files()) {
      const ls = this.lines(f);
      for (let i = 0; i < ls.length; i++) if (test(ls[i]!)) return [this.rel(f), i + 1];
    }
    return [null, null];
  }

  locate_all(rx: py.re.Pattern, files: string[] | null = null): Loc[] {
    const out: Loc[] = [];
    for (const f of files && files.length ? files : this.text_files()) {
      const ls = this.lines(f);
      for (let i = 0; i < ls.length; i++) if (rx.search(ls[i]!)) out.push([this.rel(f), i + 1]);
    }
    return out;
  }

  /** Line of a box label in a hotspots json, whichever way the json escaped it. */
  locate_label(label: PyValue, path: string): Loc {
    for (const enc of [py.json.dumps(label, { ensureAscii: false }), py.json.dumps(label)]) {
      const [f, l] = this.locate('"label": ' + enc, [path]);
      if (f) return [f, l];
    }
    return [this.rel(path), null];
  }

  /** Locate rendered text in the markdown: literally, then by its letters and digits only (markup differs). */
  locate_text(text: string, files: string[] | null = null): Loc {
    const lit = py.cpSlice(py.str.strip(text || ""), 0, 60);
    if (py.cpLen(lit) >= 3) {
      const [f, l] = this.locate(lit, files);
      if (f) return [f, l];
    }
    const n = norm(text);
    if (!n) return [null, null];
    for (const size of [60, 32, 16, 8]) {
      const probe = n.slice(0, size);
      if (probe.length < 4 || (size < 16 && n.length > size)) break;
      for (const f of files && files.length ? files : this.text_files()) {
        this.lines(f);
        const nl = this._norm.get(f)!;
        for (let i = 0; i < nl.length; i++) if (nl[i]!.includes(probe)) return [this.rel(f), i + 1];
      }
    }
    return [null, null];
  }

  locate_token(tok: string): Loc {
    return this.locate(re.compile(String.raw`(?<![\w#/.\-])` + re.escape(tok) + String.raw`(?![\w\-]|\.\d)`));
  }

  // -- page facts
  collect_ids(): Set<unknown> {
    // lint_packet.py calls .get on content.json's root and records unguarded: a root that is not a dict ends the run
    const c = this.content;
    const ids = new Set<unknown>(iter(dget(c, "defs", new Map())));
    for (const a of iter(dget(c, "anchors", []))) if (truthy(dget(a, "id"))) ids.add(dget(a, "id"));
    const tree: PyValue = [dget(c, "sections", []), dget(c, "lede", [])];
    for (const n of nodes(tree)) {
      const id = get(n, "id");
      if (typeof id === "string" && id) ids.add(id);
    }
    for (const [, , , v] of walk(tree)) for (const m of re.finditer(String.raw`\bid="([^"]+)"`, v)) ids.add(m.group(1));
    for (const t of iter(dget(c, "schema", []))) if (truthy(dget(t, "id"))) ids.add(dget(t, "id"));
    return ids;
  }

  match_slots(): Slot[] {
    const secs = iter(get(this.content, "sections", [])) as PyDict[];
    const used = new Set<unknown>();
    const out: Slot[] = [];
    for (const [name, ids, rx] of SLOTS) {
      const hit =
        secs.find((s) => !used.has(sid(s)) && ids.has(sid(s) as string)) ??
        secs.find((s) => !used.has(sid(s)) && re.search(rx, titleText(s), re.I) !== null) ??
        null;
      if (hit) used.add(sid(hit));
      out.push([name, hit]);
    }
    return out;
  }

  /** [(line, title, attrs)] for every ## in the source. */
  source_sections(): [number, string, Attrs][] {
    if (!this.src) return [];
    const out: [number, string, Attrs][] = [];
    for (const [i, line] of outside_fences(this.lines(this.src))) {
      const m = re.match(String.raw`^##\s+(.*?)\s*$`, line);
      if (m) {
        const [t, attrs] = parse_attrs(m.group(1)!);
        out.push([i, t, attrs]);
      }
    }
    return out;
  }

  /** The [diagram: key] marker lines of the source (DIAG). */
  diagramMarks(): [number, string][] {
    const out: [number, string][] = [];
    if (!this.src) return out;
    for (const [ln, line] of outside_fences(this.lines(this.src))) {
      const m = MARK.match(line);
      if (m && py.lower(m.group(1)!) === "diagram") out.push([ln, m.group(2)!]);
    }
    return out;
  }
}

/** s["id"] (Python raises KeyError on a section without one). */
export function sid(s: PyDict): PyValue {
  if (!isDict(s) || !s.has("id")) throw new py.PyError("KeyError", "'id'");
  return s.get("id")!;
}
function titleText(s: PyDict): string {
  const t = get(s, "titleText", "");
  if (typeof t !== "string") throw new py.PyError("TypeError", "expected string or bytes-like object");
  return t;
}

export class Packet extends Context.Service<Packet, LintPacket>()("tdd/lint/Packet") {}

/** lint_packet.Lint.__init__: read meta, find the source and PR files, load the build or compile the source into a
 * scoped temporary folder, then collect the page's ids and match the house slots. */
export const makePacket = (opts: LintOptions): Effect.Effect<LintPacket, never, Fs | Compiler | Scope.Scope> =>
  Effect.gen(function* () {
    const fs = yield* Fs;
    const compiler = yield* Compiler;
    const p = new LintPacket(fs, opts);
    p.meta = p.load_json(join(p.root, "meta.json"), "META", true);
    p.src = p.find_source();
    const prsDir = join(p.root, "prs");
    p.prs = fs.isdir(prsDir) ? py.sorted(fs.listdir(prsDir).filter((f) => f.endsWith(".md")).map((f) => join(prsDir, f))) : [];
    let content: PyValue | null = null;
    const cpath = join(p.build, "content.json");
    if (opts.compile || !fs.isfile(cpath)) {
      const reason = opts.compile ? "--compile" : `no ${p.rel(cpath)}`;
      if (!p.src) {
        p.add("STRUCT", "no-source", "error", `no source markdown in the packet (expected ${p.slug}.md)`, null);
      } else if (!compiler.available()) {
        p.add("STRUCT", "no-content", "error", `${reason}, and build_tdd.py is not beside this script to compile one`, null);
      } else {
        const r = yield* compiler.compile(p.src, p.root, "lint-tdd-");
        p.tmp = r.dir;
        if (r.error !== null) {
          p.add("STRUCT", "no-content", "error", `${reason}; compiling ${p.rel(p.src)} failed: ${r.error}`, p.rel(p.src));
        } else {
          p.compiled = true;
          p.note(`${reason}: compiled ${p.rel(p.src)} with build_tdd.py --no-copy into a temporary folder and linted that ` +
            `(page, stale-build and agents checks need a real build)`);
          content = py.json.loads(fs.readText(join(r.dir, "content.json")));
          p.manifest = py.json.loads(fs.readText(join(r.dir, "content.files.json")));
        }
      }
    } else {
      content = p.load_json(cpath, "STRUCT");
      p.manifest = p.load_json(join(p.build, "content.files.json"), "FILES", true);
      if (p.manifest === null) {
        p.add("FILES", "no-manifest", "error", `no content.files.json beside ${p.rel(cpath)}: rebuild with build_tdd.py`, p.rel(p.build));
      }
    }
    p.content = content === null ? new Map() : content;
    p.page_ids = p.collect_ids();
    p.slots = p.match_slots();
    return p;
  });

export { has };
