// prs/pr-NN.md and sub-PRs prs/pr-NNa.md (build_tdd.py:207-233): front matter as build_prs.py (milestone,
// milestone_note, title, change, rollback) plus depends_on, sorted NN, NNa, NNb, NN+1.
import * as py from "../py";
import * as sd from "../sd";

const re = py.re;

export const PR_FILE = re.compile(String.raw`pr-(\d+)([a-z]?)\.md`);

export interface Pr {
  n: number | bigint;
  sub: string;
  label: string;
  title: string;
  ms: string;
  ms_note: string;
  body: string;
  change: string;
  rollback: string;
  depends_on: string[];
  file: string;
}

const cmpN = (a: number | bigint, b: number | bigint) => (a < b ? -1 : a > b ? 1 : 0);

/** build_tdd.load_prs(prs_dir) */
export function load_prs(prs_dir: string): Pr[] {
  const prs: Pr[] = [];
  for (const f of py.io.listdirSorted(prs_dir)) {
    const m = PR_FILE.fullmatch(f);
    if (!m) continue;
    const raw = py.io.readText(py.path.join(prs_dir, f));
    const [meta, body] = sd.prs.front(raw);
    const n = py.int(m.group(1)!);
    const sub = m.group(2)!;
    const label = `${py.repr(n)}${sub}`;
    const h1 = re.search("^# (.+)$", body, re.M);
    const title = meta.get("title") || (h1 ? re.sub(String.raw`^PR\s*\d+[a-z]?\s*[:.\-·]?\s*`, "", h1.group(1)!) : `PR ${label}`);
    const sm = re.search(String.raw`^## Summary\s*\n(.*?)(?=^## |\Z)`, body, re.S | re.M);
    const summ = sm ? py.str.split(sm.group(1)!).join(" ") : "";
    const rb = re.search(String.raw`Rollback:\s*(.+?)(?:\.\s|\.?$)`, summ);
    let ms = meta.get("milestone") || "";
    if (!ms) {
      const t = re.search(String.raw`milestone-(\w+)`, raw);
      ms = t ? `Milestone ${t.group(1)}` : "PRs";
    }
    const depsRaw = meta.get("depends_on") || "";
    let deps = re.split(String.raw`[,;]\s*`, depsRaw).map((d) => py.str.strip(d!)).filter((d) => d);
    deps = deps.map((d) => (py.upper(d).startsWith("PR") ? d : `PR ${d}`));
    prs.push({
      n, sub, label, title, ms, ms_note: meta.get("milestone_note") ?? "", body: re.sub(String.raw`\A# .*\n`, "", py.str.lstrip(body)),
      change: meta.get("change") || sd.prs.firstSentence(summ), rollback: meta.get("rollback") || (rb ? rb.group(1)! : "not stated"),
      depends_on: deps, file: `prs/${f}`,
    });
  }
  // sorted(prs, key=lambda q: (q["n"], q["sub"])): stable, numbers then code point order
  return prs.sort((a, b) => cmpN(a.n, b.n) || py.cmp(a.sub, b.sub));
}
