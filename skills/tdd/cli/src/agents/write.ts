// write_agents.py's write() and check(): run a lane into --out, and verify a lane is current.
import { copyFileSync, mkdirSync, readFileSync, readdirSync, rmdirSync, unlinkSync, writeFileSync, existsSync, lstatSync } from "node:fs";
import { Lane, BUDGET, GENERATOR, readJson, rel } from "./lane";
import { PageLane } from "./pagelane";
import { type Py, PY_WS, abspath, dirname, isdir, isfile, pjoin, readText, sha256, get, or, str as S, strip as pystrip } from "./_py";

export class SystemExit extends Error {
  constructor(readonly code: number, msg = "") {
    super(msg);
  }
}

function laneFiles(out: string): Map<string, Py> | null {
  const tj = pjoin(out, "tokens.json");
  if (!isfile(tj)) return null;
  try {
    const d = readJson(tj);
    const f = or(get(d, "files"), new Map());
    return f instanceof Map ? f : new Map();
  } catch {
    return null;
  }
}

function firstLine(p: string): string {
  const s = readText(p, true);
  const i = s.indexOf("\n");
  return i < 0 ? s : s.slice(0, i + 1);
}

function looksLikeLane(out: string): boolean {
  if (!isdir(out) || !readdirSync(out).length) return true;
  const readme = pjoin(out, "README.md");
  if (isfile(readme) && firstLine(readme).includes("agents lane")) return true;
  return laneFiles(out) !== null;
}

/** os.walk(top): (dirpath, dirnames, filenames) top-down in directory order, symlinked directories listed but not entered. */
export function* walk(top: string): Generator<[string, string[], string[]]> {
  const stack = [top];
  while (stack.length) {
    const d = stack.pop()!;
    let names: string[];
    try {
      names = readdirSync(d);
    } catch {
      continue;
    }
    const dirs: string[] = [], files: string[] = [];
    for (const n of names) (isdir(pjoin(d, n)) ? dirs : files).push(n);
    yield [d, dirs, files];
    for (let i = dirs.length - 1; i >= 0; i--) {
      const p = pjoin(d, dirs[i]!);
      if (!lstatSync(p).isSymbolicLink()) stack.push(p);
    }
  }
}

export interface WriteResult { lane: Lane | PageLane; mode: string; out: string }

export function write(buildDir: string, packet: string | null = null, out: string | null = null, budget = BUDGET, copyImages = true, force = false): WriteResult {
  const build = abspath(buildDir);
  let lane: Lane | PageLane, mode: string;
  if (isfile(pjoin(build, "content.json"))) {
    lane = new Lane(build, packet, out, budget, copyImages).run();
    mode = "content.json";
  } else if (isfile(pjoin(build, "tdd.html"))) {
    lane = new PageLane(build, out, budget).run();
    mode = "tdd.html fallback";
  } else throw new SystemExit(1, `${build}: no content.json and no tdd.html`);
  const o = lane.out;
  if (existsSync(o) && !looksLikeLane(o) && !force) {
    throw new SystemExit(1, `${o}: exists and is not an agents lane (no README header, no tokens.json); pass --force to overwrite`);
  }
  const old = laneFiles(o) ?? new Map();
  for (const f of old.keys()) {   // remove what the previous run generated; never touch unlisted files
    const p = pjoin(o, f);
    if (!lane.files.has(f) && isfile(p)) unlinkSync(p);
  }
  for (const [f, v] of lane.files) {
    const p = pjoin(o, f);
    mkdirSync(dirname(p), { recursive: true });
    if (typeof v !== "string") copyFileSync(v.copy, p);
    else writeFileSync(p, v);
  }
  const dirs = [...walk(o)].sort((a, b) => b[0].length - a[0].length);
  for (const [dp] of dirs) if (dp !== o && !readdirSync(dp).length) rmdirSync(dp);
  return { lane, mode, out: o };
}

/** Problems that make the agents/ lane stale or untrustworthy; empty when it is current. */
export function check(buildDir: string, out: string | null = null): string[] {
  const build = abspath(buildDir);
  const o = abspath(out || pjoin(build, "agents"));
  const cpath = pjoin(build, "content.json"), page = pjoin(build, "tdd.html");
  const pageSha = isfile(page) ? sha256(readFileSync(page)).slice(0, 12) : null;
  let want: string[];
  if (isfile(cpath)) {
    let gen: Py;
    try {
      gen = get(readJson(cpath), "generated");
    } catch (e) {
      return [`${cpath}: unreadable (${(e as Error).message})`];
    }
    want = [`content ${S(gen)} `, `page ${pageSha || "none"} `];
  } else if (pageSha) want = [`content page:${pageSha} `];
  else return [`${build}: no content.json or tdd.html; build the page first`];
  const readme = pjoin(o, "README.md");
  if (!isfile(readme)) return [`${readme}: missing; run write_agents.py ${build}`];
  const first = pystrip(firstLine(readme));
  const problems: string[] = [];
  for (const w of want) {
    if (!first.includes(w)) {
      const key = w.split(" ")[0]!;
      const found = new RegExp(reEscape(key) + ` ([^${PY_WS}]+)`).exec(first);
      problems.push(`agents/ is stale: README header has '${key} ${found ? found[1] : "?"}', the build has '${pystrip(w)}'; rerun write_agents.py ${build}`);
    }
  }
  if (!first.includes(GENERATOR)) problems.push(`agents/ was written by another generator version (want ${GENERATOR}); rerun write_agents.py`);
  const listed = laneFiles(o);
  if (listed === null) {
    problems.push(`${o}/tokens.json: missing or unreadable`);
    return problems;
  }
  for (const [f, rec] of listed) {
    const p = pjoin(o, f);
    if (!isfile(p)) problems.push(`agents/${f}: listed in tokens.json but missing`);
    else if (sha256(readFileSync(p)) !== get(rec, "sha256")) problems.push(`agents/${f}: changed since it was generated (hand edit?)`);
  }
  for (const [dp, , fns] of walk(o)) {
    for (const fn of fns) {
      const r = rel(pjoin(dp, fn), o);
      if (r !== "tokens.json" && !listed.has(r)) problems.push(`agents/${r}: not generated by write_agents.py`);
    }
  }
  return problems;
}

// KERNEL: replace with src/py/re (escape) at merge
function reEscape(s: string): string {
  return s.replace(/[^\p{L}\p{N}_]/gu, (c) => "\\" + c);
}
