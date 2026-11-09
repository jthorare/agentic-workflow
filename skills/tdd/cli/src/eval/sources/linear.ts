// sources/linear.py: the Linear issues that name a TDD by the tagging convention (references/eval-loop.md): a label
// `tdd:<slug>`, or the slug in the title or description (a line `TDD: <slug> D4` or `TDD: <slug> PR 3` names the
// decision or PR). A foundation, as the Python is: written against Linear's GraphQL docs, not exercised with a key.
// Loaded lazily by bugs.ts; the key is LINEAR_API_KEY from the environment (Redacted), sent only in the request header.
import { Effect, Option, Redacted } from "effect";
import * as py from "../../py";
import { type Py, get } from "../common";

const re = py.re;

export const ENDPOINT = "https://api.linear.app/graphql";
export const QUERY = `query TddIssues($filter: IssueFilter, $after: String) {
  issues(filter: $filter, first: 100, after: $after) {
    nodes { identifier title url createdAt description state { name type } labels { nodes { name } } }
    pageInfo { hasNextPage endCursor }
  }
}`;
const MAX_PAGES = 5;

export const issueFilter = (slug: string) => ({ or: [{ labels: { name: { eq: `tdd:${slug}` } } },
  { title: { containsIgnoreCase: slug } }, { description: { containsIgnoreCase: slug } }] });

/** D-numbers and PR numbers on the lines of the description that name the slug. */
export function mentions(slug: string, text: Py): [string[], number[]] {
  const ds = new Set<string>();
  const prs = new Set<number>();
  for (const line of py.str.splitlines(py.truthy(text) ? text : "")) {
    if (!py.lower(line).includes(py.lower(slug))) continue;
    for (const m of re.findall(String.raw`\b([DR]\d+)\b`, line, re.I) as string[]) ds.add(py.upper(m));
    for (const m of re.findall(String.raw`\bPR[\s\-#]*(\d+)\b`, line, re.I) as string[]) prs.add(Number(py.int(m)));
  }
  return [py.sorted(ds), [...prs].sort((a, b) => a - b)];
}

export type SourceResult = Record<string, Py>;

export const query = (key: Option.Option<Redacted.Redacted<string>>, slug: string, plannedPrs: Py[] = [], decisionIds: Py[] = [], timeout = 30) =>
  Effect.gen(function* () {
    if (Option.isNone(key)) return { status: "not run", reason: "no LINEAR_API_KEY" } as SourceResult;
    const issues: Py[] = [];
    let after: Py = null;
    for (let page = 0; page < MAX_PAGES; page++) {
      const body = JSON.stringify({ query: QUERY, variables: { filter: issueFilter(slug), after } });
      const res = yield* Effect.promise(() => fetch(ENDPOINT, {
        method: "POST", body, signal: AbortSignal.timeout(timeout * 1000),
        headers: { "Content-Type": "application/json", Authorization: Redacted.value(key.value) },
      }).then(async (r) => (r.ok ? { data: py.json.loads(await r.text()) } : { http: r.status }), (e) => ({ failed: (e as Error).name || "Error" }))
        .catch(() => ({ failed: "ValueError" })));
      if ("http" in res) return { status: "error", reason: `Linear HTTP ${res.http}` } as SourceResult;
      if ("failed" in res) return { status: "error", reason: `Linear request failed: ${res.failed}` } as SourceResult;
      const data = res.data;
      const errors = get(data, "errors");
      if (py.truthy(errors))
        return { status: "error", reason: "Linear GraphQL error: " + (errors as Py[]).slice(0, 3).map((x) => py.cpSlice(String(get(x, "message")), 0, 120)).join("; ") } as SourceResult;
      const pg = get(get(data, "data") || new Map(), "issues") || new Map();
      issues.push(...(get(pg, "nodes") || []));
      const info = get(pg, "pageInfo") || new Map();
      if (!py.truthy(get(info, "hasNextPage"))) break;
      after = get(info, "endCursor");
    }
    const rows: Record<string, Py>[] = [];
    const byState = new Map<Py, number>();
    for (const it of issues) {
      const st = get(it, "state") || new Map();
      const [ds, prs] = mentions(slug, get(it, "description"));
      const labels = (get(get(it, "labels") || new Map(), "nodes") || []).map((l: Py) => get(l, "name"));
      rows.push({ identifier: get(it, "identifier"), title: get(it, "title"), state: get(st, "name"), state_type: get(st, "type"),
        created: get(it, "createdAt"), url: get(it, "url"), labels,
        decisions: ds.filter((d) => !decisionIds.length || decisionIds.includes(d)), prs: prs.filter((p) => !plannedPrs.length || plannedPrs.includes(p)) });
      const k = get(st, "type") || get(st, "name") || "unknown";
      byState.set(k, (byState.get(k) ?? 0) + 1);
    }
    const open = [...byState.entries()].filter(([k]) => k !== "completed" && k !== "canceled").reduce((a, [, v]) => a + v, 0);
    return { status: "ok", count: rows.length, open, by_state: byState, issues: rows,
      method: "issues(filter: label tdd:<slug> OR title/description containsIgnoreCase <slug>); open = state.type not completed/canceled" } as SourceResult;
  });
