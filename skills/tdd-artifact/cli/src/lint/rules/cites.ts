// CITES: every path:line citation resolves in meta.repo_path at meta.sha with its line range inside the file; sha,
// sha_date and repo_url agree with the clone (lint_packet.Lint.r_cites). Git is the read-only Git service; the per-file
// `git show` line counts run with bounded concurrency and are read back in Python's order.
import { Effect } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { CODE_EXT } from "../patterns";
import { Git } from "../services";
import { type PyDict, type PyValue, decodeSurrogateEscape, dget, get, iter, norm, str, truthy } from "../pyops";

const re = py.re;
const { join, abspath, expanduser, isabs } = py.path;
const dec = (b: Uint8Array): string => py.io.decodeUtf8(b, "strict");
const decIgnore = (b: Uint8Array): string => py.io.decodeUtf8(b, "ignore");
/** bytes.strip(): ASCII whitespace only. */
const bstrip = (s: string): string => s.replace(/^[ \t\n\r\v\f]+|[ \t\n\r\v\f]+$/g, "");
const NL = 10;

function lineCount(data: Uint8Array): number {
  let n = 0;
  for (const b of data) if (b === NL) n++;
  return n;
}

export const cites = Effect.gen(function* () {
  const p = yield* Packet;
  const git = yield* Git;
  const c = p.content;
  const citesList = truthy(c) ? iter(dget(c, "cites", [])) : [];
  const meta = truthy(p.meta) ? p.meta : new Map();
  const uniq = new Map<unknown, PyDict>();
  for (const x of citesList) {
    const t = dget(x, "text");
    if (!uniq.has(t)) uniq.set(t, x as PyDict);
  }
  const repoV = dget(meta, "repo_path");
  if (!truthy(repoV)) {
    if (uniq.size) {
      p.add("CITES", "no-repo", "warn", `NEEDS_CONTEXT: ${uniq.size} citations not checked: meta.json has no repo_path (add repo_path, ` +
        `repo_url and sha so path:line citations can be verified and linked)`, p.meta !== null ? "meta.json" : null);
    }
    return;
  }
  const repo = abspath(expanduser(str(repoV)));
  const mpath = join(p.root, "meta.json");
  {
    const [rc, out] = yield* git.run(repo, ["rev-parse", "--is-inside-work-tree"]);
    if (rc !== 0 || bstrip(decIgnore(out)) !== "true") {
      p.add("CITES", "no-repo", "warn", `NEEDS_CONTEXT: repo_path ${p.rel(repo)} is not a git checkout here; ${uniq.size} citations not checked`,
        ...p.locate('"repo_path"', [mpath]));
      return;
    }
  }
  const url = get(meta, "repo_url");
  if (truthy(url)) {
    const [rc, origin] = yield* git.run(repo, ["remote", "get-url", "origin"]);
    if (rc === 0) {
      let o = re.sub(String.raw`(\.git)?/?$`, "", py.str.strip(dec(origin)));
      o = re.sub("^git@([^:]+):", String.raw`https://\1/`, o);
      if (norm(o) !== norm(re.sub("/?$", "", str(url)))) {
        p.add("CITES", "repo-url", "warn", `repo_url ${str(url)} is not repo_path's origin (${o}): citations link to one repo and are checked in another`,
          ...p.locate('"repo_url"', [mpath]));
      }
    }
  }
  const sha = get(meta, "sha");
  let ref: string | null = null;
  let verified = false;
  if (truthy(sha)) {
    const shaS = str(sha);
    const [rc, kind] = yield* git.run(repo, ["cat-file", "-t", shaS]);
    if (rc === 0 && bstrip(decIgnore(kind)) === "commit") {
      ref = shaS;
      verified = true;
      const [rc2, d] = yield* git.run(repo, ["show", "-s", "--format=%cs", shaS]);
      const real = rc2 === 0 ? py.str.strip(dec(d)) : "";
      const sd = get(meta, "sha_date");
      if (truthy(sd) && real && !(typeof sd === "string" && sd === real)) {
        p.add("CITES", "sha-date", "warn", `sha_date ${str(sd)} but commit ${shaS} is dated ${real}`, ...p.locate('"sha_date"', [mpath]));
      } else if (!truthy(sd)) {
        p.add("CITES", "sha-date", "warn", `meta.json has no sha_date (commit ${shaS} is dated ${real})`, ...p.locate('"sha"', [mpath]));
      }
    } else {
      p.add("CITES", "sha-unknown", "warn", `NEEDS_CONTEXT: meta.sha ${shaS} is not a commit in ${p.rel(repo)} (a clone older than the sha, or a ` +
        `wrong sha; \`git -C ${p.rel(repo)} fetch origin\` then rerun): citations are checked at HEAD instead, as warnings`,
        ...p.locate('"sha"', [mpath]));
    }
  } else {
    p.add("CITES", "sha-unknown", "warn", "meta.json has no sha: citations are not linked and are checked at HEAD, as warnings", "meta.json");
  }
  if (!ref) {
    const [rc, h] = yield* git.run(repo, ["rev-parse", "--short=10", "HEAD"]);
    if (rc !== 0) return;
    ref = py.str.strip(dec(h));
  }
  const [rcT, tree] = yield* git.run(repo, ["ls-tree", "-r", "--name-only", ref]);
  if (rcT !== 0) {
    p.add("CITES", "no-repo", "warn", `git ls-tree ${ref} failed in ${p.rel(repo)}: ${py.cpSlice(py.str.strip(decIgnore(tree)), 0, 120)}`, "meta.json");
    return;
  }
  const files = new Set(py.str.splitlines(decodeSurrogateEscape(tree)));
  const by_base = new Map<string, string[]>();
  for (const f of files) {
    const b = f.slice(f.lastIndexOf("/") + 1);
    if (!by_base.has(b)) by_base.set(b, []);
    by_base.get(b)!.push(f);
  }
  const sev = verified ? "error" : "warn";
  const at = verified ? "" : " [HEAD fallback]";
  const theRef = ref;

  type Plan = { text: unknown; x: PyDict; path: string; a: PyValue; b: PyValue; local: string | null };
  const plans: Plan[] = [];
  for (const [text, x] of uniq) {
    const path = str(truthy(get(x, "path")) ? get(x, "path") : "");
    let local: string | null = null;
    for (const q of [join(p.root, path), join(p.build, path)]) {
      if (p.fs.isfile(q)) {
        local = q;
        break;
      }
    }
    const ex = expanduser(path);
    if (isabs(ex) && p.fs.isfile(ex)) local = ex;
    plans.push({ text, x, path, a: get(x, "from"), b: get(x, "to"), local });
  }
  // git show <ref>:<path> for every file the loop counts, eight at a time, kept by path (count() in the Python)
  const toCount = [...new Set(plans.filter((q) => !q.local && files.has(q.path)).map((q) => q.path))];
  const counted = yield* Effect.forEach(toCount, (path) =>
    git.run(repo, ["show", `${theRef}:${path}`]).pipe(Effect.map(([rc, data]) => {
      const n = rc === 0 ? lineCount(data) + (data.length && data[data.length - 1] !== NL ? 1 : 0) : null;
      return [path, n] as const;
    })), { concurrency: 8 });
  const nlines = new Map<string, number | null>(counted);

  let checked = 0;
  const wt_hits: string[] = [];
  const rec = (x: PyDict, path: string, how: string) => {
    p.citations.push(new Map<string, PyValue>([
      ["path", path], ["from", get(x, "from")], ["to", get(x, "to")], ["section", get(x, "section")], ["resolved", how],
      ["checked_sha", how === "sha" || how === "head" ? theRef : null],
    ]));
  };
  for (const { text, x, path, a, b, local } of plans) {
    const textS = str(text);
    const [f, l] = p.locate("`" + textS + "`");
    const ext = path.includes(".") ? py.lower(path.slice(path.lastIndexOf(".") + 1)) : "";
    let n: number | null = null;
    if (local) {
      n = lineCount(p.fs.readBytes(local)) + 1;
      rec(x, path, "packet");
    } else if (files.has(path)) {
      n = nlines.get(path) ?? null;
      checked += 1;
      rec(x, path, verified ? "sha" : "head");
    } else if (!verified && p.fs.isfile(join(repo, path))) {
      // untracked or newer than HEAD: check the working tree and say so once
      n = lineCount(p.fs.readBytes(join(repo, path))) + 1;
      checked += 1;
      wt_hits.push(path);
      rec(x, path, "working_tree");
    } else {
      rec(x, path, "missing");
      const base = path.slice(path.lastIndexOf("/") + 1);
      const suffix = py.sorted((by_base.get(base) ?? []).filter((q) => q.endsWith("/" + path)));
      if (suffix.length) {
        // a short form of a real file: the link the compiler builds (repo_url/blob/sha/<short>) is dead at any commit
        const hint = suffix.length === 1 ? `did you mean ${suffix[0]}` : `${suffix.length} files end with it (${suffix.slice(0, 2).join(", ")}, ...)`;
        if (truthy(a)) {
          p.add("CITES", "short-path", "error", `\`${textS}\` is a short path, so its line link is dead: ${hint}; write the full path or an ` +
            `alias from meta.path_aliases`, f, l);
        } else {
          p.add("CITES", "short-path", "warn", `\`${textS}\` is a short path a reader cannot open: ${hint}; write the full path or an alias`, f, l);
        }
      } else if (truthy(a)) {
        p.add("CITES", "path-unknown", sev, `\`${textS}\`: ${path} is not a file in ${p.rel(repo)} at ${theRef}, so the line link is dead${at}`, f, l);
      } else {
        p.add("CITES", "path-unknown", sev, `\`${textS}\`: ${path} is not a file in ${p.rel(repo)} at ${theRef} (if the design adds this file, name ` +
          `it in prose rather than as a citation)${at}`, f, l);
      }
      continue;
    }
    if (truthy(a)) {
      const av = num(a), hi = num(truthy(b) ? b : a);
      if (n !== null && (av < 1 || hi < av || hi > n)) {
        p.add("CITES", "line-range", sev, `\`${textS}\`: lines ${str(a)}${truthy(b) ? "-" + str(b) : ""} are outside ${path}, which has ${n} lines at ` +
          `${!local ? theRef : "the packet copy"}${at}`, f, l);
      }
    } else if (CODE_EXT.has(ext)) {
      p.add("CITES", "no-line", "warn", `\`${textS}\` cites a code file without a line or range (write path:line or path:from-to)`, f, l);
    }
  }
  if (wt_hits.length) {
    p.add("CITES", "working-tree", "warn", `${new Set(wt_hits).size} cited file(s) are not in ${theRef} but exist in ${p.rel(repo)}'s working tree (uncommitted); ` +
      `checked there: commit and set meta.sha so the links resolve`, "meta.json");
  }
  if (uniq.size) {
    p.note(`CITES: ${uniq.size} distinct citations, ${checked} resolved in ${p.rel(repo)} at ${theRef}` +
      `${verified ? "" : " (HEAD fallback)"}${wt_hits.length ? `, ${new Set(wt_hits).size} in the working tree` : ""}`);
  }
});

function num(v: PyValue): number {
  if (typeof v === "number") return v;
  if (typeof v === "bigint") return Number(v);
  if (typeof v === "boolean") return v ? 1 : 0;
  if (v instanceof py.PyFloat) return v.value;
  throw new py.PyError("TypeError", "'<' not supported between instances");
}
