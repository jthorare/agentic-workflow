"""Linear source for output.bugs.json (a foundation: written against Linear's API docs, not yet exercised with a key).

Finds the issues that name a TDD by the tagging convention (references/eval-loop.md): a label `tdd:<slug>`, or the
slug in the title or description (a line `TDD: <slug> D4` or `TDD: <slug> PR 3` names the decision or PR). D-numbers
and PR numbers are read from the description of an issue that already names the slug; a bare "D4" never matches on
its own (it would hit unrelated issues across the workspace).

Verified 2026-11-08 at linear.app/developers: endpoint https://api.linear.app/graphql; a personal API key goes in the
Authorization header with no "Bearer" prefix; issue filters take comparators eq/in/contains/containsIgnoreCase, label
filters take labels: {name: {...}}, and every filter accepts `or`. From memory (unverified): the containsIgnoreCase
comparator on title and description, and Relay pagination (first/after, pageInfo{hasNextPage endCursor}).
Key: LINEAR_API_KEY in the environment only; never written to a file. Stdlib only (urllib)."""
import json, os, re, urllib.error, urllib.request

ENDPOINT = "https://api.linear.app/graphql"
QUERY = """query TddIssues($filter: IssueFilter, $after: String) {
  issues(filter: $filter, first: 100, after: $after) {
    nodes { identifier title url createdAt description state { name type } labels { nodes { name } } }
    pageInfo { hasNextPage endCursor }
  }
}"""
MAX_PAGES = 5


def issue_filter(slug):
    return {"or": [{"labels": {"name": {"eq": f"tdd:{slug}"}}},
                   {"title": {"containsIgnoreCase": slug}},
                   {"description": {"containsIgnoreCase": slug}}]}


def mentions(slug, text):
    """D-numbers and PR numbers on the lines of the description that name the slug."""
    ds, prs = set(), set()
    for line in (text or "").splitlines():
        if slug.lower() not in line.lower():
            continue
        ds |= {m.upper() for m in re.findall(r"\b([DR]\d+)\b", line, re.I)}
        prs |= {int(m) for m in re.findall(r"\bPR[\s\-#]*(\d+)\b", line, re.I)}
    return sorted(ds), sorted(prs)


def query(slug, planned_prs=(), decision_ids=(), timeout=30):
    key = os.environ.get("LINEAR_API_KEY")
    if not key:
        return {"status": "not run", "reason": "no LINEAR_API_KEY"}
    issues, after = [], None
    for _ in range(MAX_PAGES):
        body = json.dumps({"query": QUERY, "variables": {"filter": issue_filter(slug), "after": after}}).encode()
        req = urllib.request.Request(ENDPOINT, data=body, headers={"Content-Type": "application/json", "Authorization": key})
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                data = json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            return {"status": "error", "reason": f"Linear HTTP {e.code}"}
        except (urllib.error.URLError, TimeoutError, ValueError) as e:
            return {"status": "error", "reason": f"Linear request failed: {type(e).__name__}"}
        if data.get("errors"):
            return {"status": "error", "reason": "Linear GraphQL error: " + "; ".join(str(x.get("message"))[:120] for x in data["errors"][:3])}
        page = ((data.get("data") or {}).get("issues") or {})
        issues += page.get("nodes") or []
        info = page.get("pageInfo") or {}
        if not info.get("hasNextPage"):
            break
        after = info.get("endCursor")
    rows, by_state = [], {}
    for it in issues:
        st = it.get("state") or {}
        ds, prs = mentions(slug, it.get("description"))
        labels = [l.get("name") for l in ((it.get("labels") or {}).get("nodes") or [])]
        rows.append({"identifier": it.get("identifier"), "title": it.get("title"), "state": st.get("name"), "state_type": st.get("type"),
                     "created": it.get("createdAt"), "url": it.get("url"), "labels": labels,
                     "decisions": [d for d in ds if not decision_ids or d in decision_ids], "prs": [p for p in prs if not planned_prs or p in planned_prs]})
        k = st.get("type") or st.get("name") or "unknown"
        by_state[k] = by_state.get(k, 0) + 1
    open_n = sum(v for k, v in by_state.items() if k not in ("completed", "canceled"))
    return {"status": "ok", "count": len(rows), "open": open_n, "by_state": by_state, "issues": rows,
            "method": "issues(filter: label tdd:<slug> OR title/description containsIgnoreCase <slug>); open = state.type not completed/canceled"}
