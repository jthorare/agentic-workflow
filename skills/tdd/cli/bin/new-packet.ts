#!/usr/bin/env bun
// tdd new: new_packet.py in TypeScript. Scaffolds <root>/<repo-slug>/system-design/<slug>/ from skills/tdd/templates
// with the same files, bytes, stdout and exit codes as the Python; TDD_TODAY (YYYY-MM-DD) pins the date in both.
// The argparse surface is reproduced for this parser's options (exact, --opt=value, unique prefixes, --, -h); as in
// CPython 3.13, an option never takes its value across "--".
import { lstatSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { PyError, abspath, basename, cmpCodepoint, dirname, exists, expanduser, isDir, isFile, join, jsonDumps, jsonLoads, listdir,
  normpath, readText, repr, splitWs, strip, type PyDict, type PyValue } from "../src/hotspots/_py";
import { traceback } from "../src/hotspots/shapes";
import * as re from "../src/py/re";

export const DOC = "Create a new TDD packet from the house template.\n\nUsage:\n  new_packet.py <slug> [--title T] [--ticket P-XXX-NNN] [--repo <path>] [--root <dir>]\n\nCreates <root>/<repo-slug>/system-design/<slug>/ with:\n  <slug>.md       templates/tdd-template.md, with the title, ticket, sha and date filled in\n  meta.json       templates/meta.template.json without its _doc block, filled: title (the tab title, from --title the\n                  way build_doc.page_title derives it, else from the slug), project (the ticket), Author (git user.name),\n                  updated (today), rev 1, repo_url (GitHub or GitLab origin only), sha, sha_date, repo_path\n  status.md       the one living doc: artifact_url placeholder, Reference, Surfaces, Counts, Decisions, Open\n                  questions, Lead defaults, Revisions, Tracker\n  diagrams/*.svg  templates/packet/ copied as it is: a placeholder image, marked PLACEHOLDER and REPLACE ME, for\n  mockups/*.svg   every diagram and shot the template references, so a fresh packet builds and renders; it lints\n                  with FILES/placeholder and FILES/placeholder-text errors until the author replaces each one (the\n                  \"todo:\" line lists the images)\n  prs/ diagrams/ mockups/ schema/ queries/ shots/   working folders (prs/ schema/ queries/ shots/ start empty)\n\n<root> is $SYSTEM_DESIGN_OUT, else ~/.system-design. <repo-slug> is derived the way system-design's scripts/env.sh does\nit: owner-repo from the origin remote of --repo (default: the current folder), else the repo root's folder name, else\nthe folder's own name. The sha is origin/HEAD when that ref exists, else HEAD; sha_date is its commit date. Nothing is\nfetched: run `git fetch` first if the remote default branch should be current.\n\nRefuses to write into a packet folder that already has files in it. Standard library only; run with python3 -I.\nExit 0 on success; 1 on bad arguments, an existing packet, or a missing template.";

// The Python's paths: HERE is skills/tdd/scripts (named in the "next:" line), SKILL is skills/tdd.
const SKILL = normpath(join(import.meta.dir, "..", ".."));
const HERE = join(SKILL, "scripts");
const TEMPLATE_MD = join(SKILL, "templates", "tdd-template.md");
const TEMPLATE_META = join(SKILL, "templates", "meta.template.json");
const TEMPLATE_PACKET = join(SKILL, "templates", "packet");
const DIRS = ["prs", "diagrams", "mockups", "schema", "queries", "shots"];

// argparse's rendering at its default width (no COLUMNS, not a terminal: 80 columns).
const USAGE = "usage: new_packet.py [-h] [--title TITLE] [--ticket TICKET] [--repo REPO]\n                     [--root ROOT]\n                     slug\n";
const HELP = USAGE + "\nCreate a new TDD packet from the house template.\n\npositional arguments:\n  slug             packet folder name: lower-case letters, digits and hyphens\n\noptions:\n  -h, --help       show this help message and exit\n  --title TITLE    the page title (the H1): the finding, not the topic\n  --ticket TICKET  ticket or project id, e.g. P-XXX-000\n  --repo REPO      the code repo the design is about (default: current folder)\n  --root ROOT      deliverables root (default: $SYSTEM_DESIGN_OUT, else\n                   ~/.system-design)\n";

type Result = { code: number; out: string; err: string };
class Exit extends Error { constructor(readonly result: Result) { super("exit"); } }
const argError = (msg: string): Exit => new Exit({ code: 2, out: "", err: `${USAGE}new_packet.py: error: ${msg}\n` });

interface Args { slug: string | null; title: string | null; ticket: string | null; repo: string; root: string | null }
const OPTION_STRINGS = ["-h", "--help", "--title", "--ticket", "--repo", "--root"];
const DEST: Record<string, "help" | "title" | "ticket" | "repo" | "root"> = {
  "-h": "help", "--help": "help", "--title": "title", "--ticket": "ticket", "--repo": "repo", "--root": "root",
};
const ACTION_NAME = { help: "-h/--help", title: "--title", ticket: "--ticket", repo: "--repo", root: "--root" };
type Tuple = { opt: string | null; explicit: string | null; sep: string | null };

function partition(s: string, sep: string): [string, string, string] {
  const i = s.indexOf(sep);
  return i < 0 ? [s, "", ""] : [s.slice(0, i), sep, s.slice(i + sep.length)];
}
/** argparse._parse_optional: null for a positional, else the matching option tuples (opt null: unknown option). */
function parseOptional(arg: string): Tuple[] | null {
  if (!arg || arg[0] !== "-") return null;
  if (DEST[arg]) return [{ opt: arg, explicit: null, sep: null }];
  if (arg.length === 1) return null;
  const [os, sep, ex] = partition(arg, "=");
  if (sep && DEST[os]) return [{ opt: os, explicit: ex, sep }];
  const tuples: Tuple[] = [];
  const [prefix, sep2, ex2] = partition(arg, "=");
  if (arg[1] === "-") {
    for (const o of OPTION_STRINGS) if (o.startsWith(prefix)) tuples.push({ opt: o, explicit: sep2 ? ex2 : null, sep: sep2 || null });
  } else {
    const short = arg.slice(0, 2), shortEx = arg.slice(2);
    for (const o of OPTION_STRINGS) {
      if (o === short) tuples.push({ opt: o, explicit: shortEx, sep: "" });
      else if (o.startsWith(prefix)) tuples.push({ opt: o, explicit: sep2 ? ex2 : null, sep: sep2 || null });
    }
  }
  if (tuples.length) return tuples;
  if (/^-\d+$|^-\d*\.\d+$/.test(arg)) return null;
  if (arg.includes(" ")) return null;
  return [{ opt: null, explicit: null, sep: null }];
}

function parseArgs(argv: string[]): Args {
  const a: Args = { slug: null, title: null, ticket: null, repo: process.cwd(), root: null };
  const extras: string[] = [];
  const kinds: ("A" | "O" | "-")[] = [];
  const tuples: (Tuple[] | null)[] = [];
  let dd = false;
  for (const arg of argv) {
    if (dd) { kinds.push("A"); tuples.push(null); continue; }
    if (arg === "--") { dd = true; kinds.push("-"); tuples.push(null); continue; }
    const t = parseOptional(arg);
    kinds.push(t ? "O" : "A");
    tuples.push(t);
  }
  let removedDD = false;
  const positional = (s: string) => {
    if (a.slug === null) a.slug = s;
    else extras.push(s);
  };
  for (let i = 0; i < argv.length; i++) {
    const kind = kinds[i]!;
    if (kind === "-") {
      if (removedDD) positional(argv[i]!);
      removedDD = true;
      continue;
    }
    if (kind === "A") { positional(argv[i]!); continue; }
    const ts = tuples[i]!;
    if (ts.length > 1) throw argError(`ambiguous option: ${argv[i]} could match ${ts.map((t) => t.opt).join(", ")}`);
    const t = ts[0]!;
    if (t.opt === null) { extras.push(argv[i]!); continue; }
    const dest = DEST[t.opt]!;
    if (dest === "help") {
      if (t.explicit !== null && t.explicit !== "" && t.opt[1] !== "-" && !t.sep && t.explicit[0] !== "-") {
        throw new Exit({ code: 0, out: HELP, err: "" });
      }
      if (t.explicit !== null) throw argError(`argument ${ACTION_NAME.help}: ignored explicit argument ${repr(t.explicit)}`);
      throw new Exit({ code: 0, out: HELP, err: "" });
    }
    let value: string;
    if (t.explicit !== null) value = t.explicit;
    else if (kinds[i + 1] === "A") value = argv[++i]!;
    else throw argError(`argument ${ACTION_NAME[dest]}: expected one argument`);
    a[dest] = value;
  }
  if (a.slug === null) throw argError("the following arguments are required: slug");
  if (extras.length) throw argError(`unrecognized arguments: ${extras.join(" ")}`);
  return a;
}

const UTF8 = new TextDecoder("utf-8", { fatal: true });
/** Output of a git command in repo, or null when git fails (not a repo, no such ref, no remote). */
function git(repo: string, ...args: string[]): string | null {
  let r: ReturnType<typeof Bun.spawnSync>;
  try {
    r = Bun.spawnSync({ cmd: ["git", "-C", repo, ...args], stdin: "inherit", stdout: "pipe", stderr: "pipe", timeout: 20_000 });
  } catch {
    return null;
  }
  if (r.exitCode !== 0 || !r.stdout) return null;
  const out = strip(UTF8.decode(r.stdout).replace(/\r\n?/g, "\n"));
  return out || null;
}

/** system-design/scripts/env.sh: the two sed substitutions on the origin URL, then '/' -> '-'. */
function repoSlug(repo: string, origin: string | null): string {
  if (origin) {
    let s = origin.replace(/[^\n]*[:/]([^/]*\/[^/]*)\.git(?=\n?$)/u, "$1");
    s = s.replace(/[^\n]*[:/]([^/]*\/[^/]*)(?=\n?$)/u, "$1");
    return s.replaceAll("/", "-");
  }
  const top = git(repo, "rev-parse", "--show-toplevel");
  return basename(top || abspath(repo));
}

/** https://<host>/<owner>/<repo> for a github.com or gitlab.com origin; null for anything else. */
function webUrl(origin: string | null): string | null {
  if (!origin) return null;
  const m = /^(?:https?:\/\/(?:[^@/]+@)?|ssh:\/\/(?:[^@/]+@)?|[^@/]+@)(github\.com|gitlab\.com)[:/]([^\n]+?)(?:\.git)?\/?(?=\n?$)/u.exec(origin);
  return m ? `https://${m[1]}/${m[2]}` : null;
}

// Python patterns run through the kernel's re (sre's IGNORECASE: "i" also matches İ and ı, "s" also ſ)
const GENERIC_TAILS: [string, string][] = [
  [String.raw`\s+technical\s+spec(ification)?$`, " Spec"], [String.raw`\s+technical\s+design(\s+doc(ument)?)?$`, " TDD"],
  [String.raw`\s+design\s+doc(ument)?$`, " Design Doc"], [String.raw`\s+system\s+design$`, " Design"],
];
const TITLE_SPLIT = String.raw`\s+[—–|:]\s+|:\s+`;

/** The browser-tab title: system-design's build_doc.page_title applied to the H1, or the slug's first three words
 * plus TDD when no title was given. */
function tabTitle(h1: string, slug: string): string {
  if (!h1) return slug.split("-").slice(0, 3).map((w) => (w ? w[0]!.toUpperCase() + w.slice(1).toLowerCase() : w)).join(" ") + " TDD";
  let t = strip(re.split(TITLE_SPLIT, h1, 1)[0]!);
  if (splitWs(t).length > 4) {
    for (const [pat, rep] of GENERIC_TAILS) {
      const t2 = re.sub(pat, rep, t, 0, re.I);
      if (t2 !== t) { t = t2; break; }
    }
  }
  const words = splitWs(t);
  return words.length > 4 ? words.slice(0, 4).join(" ") : t;
}

/** Every file under templates/packet/ as a '/'-separated path relative to it, sorted by code point; names starting
 * with '.' are skipped. os.walk's reading: an entry is a folder when it stats as one (links followed), and a linked
 * folder is listed but not entered. */
function packetFiles(): string[] {
  const out: string[] = [];
  const walk = (d: string, rel: string) => {
    for (const name of readdirSync(d)) {
      if (name.startsWith(".")) continue;
      const p = join(d, name), r = rel ? rel + "/" + name : name;
      let dir = false;
      try { dir = statSync(p).isDirectory(); } catch { dir = false; }
      if (!dir) out.push(r);
      else if (!lstatSync(p).isSymbolicLink()) walk(p, r);
    }
  };
  walk(TEMPLATE_PACKET, "");
  return out.sort(cmpCodepoint);
}

function tilde(path: string): string {
  const home = expanduser("~");
  return path === home || path.startsWith(home + "/") ? "~" + path.slice(home.length) : path;
}

function localToday(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

/** new_packet.main(argv). */
export function run(argv: string[]): Result {
  const out: string[] = [];
  const die = (msg: string): Exit => new Exit({ code: 1, out: out.join(""), err: msg + "\n" });
  try {
    const a = parseArgs(argv);
    const slug = a.slug!;
    if (!/^[a-z0-9][a-z0-9-]{0,62}$/.test(slug)) {
      throw die(`slug ${repr(slug)}: use lower-case letters, digits and hyphens (it names the folder and <slug>.md)`);
    }
    if (a.ticket !== null && !strip(a.ticket)) throw die("--ticket is empty");
    for (const t of [TEMPLATE_MD, TEMPLATE_META]) {
      if (!isFile(t)) throw die(`${t}: template missing; is the tdd skill complete?`);
    }
    if (!isDir(TEMPLATE_PACKET)) throw die(`${TEMPLATE_PACKET}: template missing; is the tdd skill complete?`);
    const repo = abspath(expanduser(a.repo));
    if (!isDir(repo)) throw die(`--repo ${repo}: no such folder`);

    const origin = git(repo, "remote", "get-url", "origin");
    const rslug = repoSlug(repo, origin);
    const root = abspath(expanduser(a.root || process.env.SYSTEM_DESIGN_OUT || "~/.system-design"));
    const packet = join(root, rslug, "system-design", slug);
    if (isDir(packet) && listdir(packet).length) {
      throw die(`${packet}: a packet already exists there; refusing to overwrite it (pick another slug, or edit that packet)`);
    }
    if (exists(packet) && !isDir(packet)) throw die(`${packet}: exists and is not a folder`);

    const ref = git(repo, "rev-parse", "--verify", "-q", "origin/HEAD") ? "origin/HEAD" : "HEAD";
    const full = git(repo, "rev-parse", "--verify", "-q", ref + "^{commit}");
    const sha = full ? full.slice(0, 10) : null;
    const shaDate = full ? git(repo, "show", "-s", "--format=%cs", full) : null;
    const author = git(repo, "config", "user.name");
    const today = process.env.TDD_TODAY || localToday();
    const title = strip(a.title ?? "") || "<Title: the finding, not the topic>";
    const ticket = strip(a.ticket ?? "");

    const meta = jsonLoads(readText(TEMPLATE_META));
    if (!(meta instanceof Map)) throw new PyError("AttributeError", "'list' object has no attribute 'pop'");
    meta.delete("_doc");
    const update: [string, PyValue][] = [
      ["title", tabTitle(strip(a.title ?? ""), slug)], ["h1", null],
      ["project", ticket ? new Map<string, PyValue>([["label", ticket], ["href", null]]) : null],
      ["people", [new Map<string, PyValue>([["role", "Author"], ["name", author || "<author>"]])]],
      ["updated", today], ["rev", 1n], ["rev_note", "first draft"], ["status", "Draft for review"], ["status_tone", null],
      ["footer", null], ["repo_url", webUrl(origin)], ["sha", sha], ["sha_date", shaDate], ["repo_path", tilde(repo)],
      ["path_aliases", new Map()], ["refs", new Map()], ["artifact_url", null], ["notion_url", null],
    ];
    for (const [k, v] of update) (meta as PyDict).set(k, v);

    const fill: [string, string][] = [
      ["{{title}}", title], ["{{ticket}}", ticket || "(none named)"], ["{{sha}}", sha || "(no commit recorded)"],
      ["{{sha_date}}", shaDate || "no date"], ["{{slug}}", slug], ["{{date}}", today],
    ];
    let md = readText(TEMPLATE_MD);
    for (const [k, v] of fill) md = md.split(k).join(v);

    const evidence = sha
      ? `evidence read at \`${sha}\` (${ref} of \`${tilde(repo)}\`, committed ${shaDate ?? "None"})`
      : `no commit recorded (\`${tilde(repo)}\` is not a git repo with a commit); record the sha in meta.json before citing code`;
    const status = `# ${title}: status (the one living doc)

artifact_url: (not published yet)
notion_url: (not mirrored yet)

Slug \`${slug}\`. Ticket ${ticket || "(none named)"}. Started ${today}${author ? " by " + author : ""}. Packet \`${tilde(packet)}\` (tdd skill). Repo \`${tilde(repo)}\` (repo slug \`${rslug}\`); ${evidence}. meta.json carries the same sha and date.

## Reference
- (the ticket, the PRD, threads and earlier designs this TDD answers to: each with its link and the date read)

## Surfaces
- (readers, writers and screens: what the tools found and what the hand map found, reconciled, as path:line at the sha above)

## Counts
- (each count: the number, what it counts, \`queries/<name>.sql\`, where it ran, the date it ran; or "not measured")

## Decisions (the owner's words)
- (none yet. Each answer gets the next D-number: D<n> (YYYY-MM-DD, who): "<the owner's words>", then one line on what it means for the design. Never re-ask a recorded one.)

## Open questions
- (Q<n>: one line each, asked one at a time; renamed to the next D-number when answered)

## Lead defaults (not rulings)
- (defaults and tool picks made without the owner, labelled as such; the owner can overrule any)

## Revisions
- rev 1 (${today}): first draft, not published.

## Tracker
- none yet
`;

    mkdirSync(packet, { recursive: true });
    for (const d of DIRS) mkdirSync(join(packet, d), { recursive: true });
    const placeholders = packetFiles();
    for (const rel of placeholders) {
      const dest = join(packet, ...rel.split("/"));
      mkdirSync(dirname(dest), { recursive: true });
      writeFileSync(dest, readFileSync(join(TEMPLATE_PACKET, ...rel.split("/"))));
    }
    writeFileSync(join(packet, `${slug}.md`), md, "utf8");
    writeFileSync(join(packet, "meta.json"), jsonDumps(meta, { indent: 2, ensureAscii: false }) + "\n", "utf8");
    writeFileSync(join(packet, "status.md"), status, "utf8");

    const repoUrl = (meta as PyDict).get("repo_url");
    out.push(`packet: ${packet}\n`);
    out.push(`repo:   ${tilde(repo)} (repo slug ${rslug}; origin ${origin || "none"}; repo_url ${repoUrl || "none"})\n`);
    out.push(`sha:    ${sha ? sha + " (" + ref + ", committed " + (shaDate ?? "None") + ")" : "none recorded"}\n`);
    out.push(`wrote:  ${slug}.md, meta.json, status.md; folders ${DIRS.map((d) => d + "/").join(", ")}\n`);
    if (placeholders.length) out.push(`todo:   replace the placeholder images before publishing: ${placeholders.join(", ")}\n`);
    out.push(`next:   python3 -I ${tilde(join(HERE, "build_tdd.py"))} ${tilde(join(packet, slug + ".md"))} ${tilde(join(packet, "build"))}\n`);
    return { code: 0, out: out.join(""), err: "" };
  } catch (e) {
    if (e instanceof Exit) return e.result;
    return { code: 1, out: out.join(""), err: traceback(e) };
  }
}

export async function main(argv: string[]): Promise<number> {
  const r = run(argv);
  if (r.out) await Bun.write(Bun.stdout, r.out);
  if (r.err) await Bun.write(Bun.stderr, r.err);
  return r.code;
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
