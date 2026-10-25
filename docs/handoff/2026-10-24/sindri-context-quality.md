# Sindri context-quality — remaining work (2026-10-24)

The original findings are below. Part of them is already on #938. This file is the rest.

## Status (2026-10-24)

| Item | Where it is handled |
|---|---|
| §1-2 roles and ignore config | #938, being reworked by the context lane |
| Budget removal and MCP paging rules | #938 |
| §6 Onyx | #938, with an owner-steps list |
| §3, §4, §7, §8 | A follow-up PR from branch `fix/context-quality-followups` |
| §5 token budget | Moot once the budget is removed |

## Already on #938 — do not redo

| Already done | Where |
|---|---|
| Tests, specs, fixtures, demos, icons, deprecated trees, shells, routers, and crons are not feature implementers and do not claim surfaces. The globs are case-insensitive. `**/` is any directories and `*` stays in one segment. | `ClusterFeatures._sharedRoles` |
| A query budget is split across the sections that have items. Unused share rolls forward. Each overflow cite is listed once. Fill order stays graph, affects, decisions, code, records. | `Packer` |
| After exact key, alias, and one exact label, a feature matches when every significant word (length at least 3, two or more words) sits in exactly one feature label. Suggestions sort test, spec, and fixture keys and labels after other nodes that share those words. | `AnchorMatch` |
| Every MCP tool takes `limit` and `offset`. Omit both for the whole answer. Pass them and each list keeps that window, `page.more` says another page follows, and counts stay the full totals. `sections` and `groups` stay whole. `context_lenses` pages in the service so HTTP, CLI, and MCP share one window. The 48,000-character cap was removed on purpose. Do not put it back. | `McpPages`, `LensPages`, `QueryRules.CheckPage` |

Those role globs stay. Do not replace them with paths from one work repo.

A map rebuild is required before the role globs show up in the live graph. `sindri context map build` is the owner's command (`SND-AUTH-002` refuses a session).

## Still to fix

### 1. Catch-all files the globs miss

#938 skips the roles above. Two kinds of file from a live map still fall outside the globs, so they can still own a feature:

- a shared layout component under a routes folder
- a small shared display component

Shared admin shells whose path contains `shell` are already skipped. Confirm that against a rebuilt map rather than adding repo-specific paths.

### 2. No configurable ignore list

There is no way to exclude files by prefix, suffix, or filename regex. The role globs are fixed in code and run only when a cluster is proposed. Queries do not apply an ignore list.

The ignore config has to support prefix, suffix, and regex, and it has to apply both when the map is built and when a query is answered. Keep it general. The examples that motivated it are an icons package, a deprecated UI package, `**/__tests__/**`, `*.unit.test.ts(x)`, `*.test.ts`, `**/Demo/**`, `*fixture*`, routers, and cron registries. Those examples are not the config.

### 3. Auto-clustered names and confirmed labels

Many features are auto-clustered under keys such as `root.package-NN` (a run of consecutive numbers). Their labels do not match their keys, and they are stored as `provenance: human, confirmed: true`. Examples of the mismatch: a key naming a chat assistant panel labelled as a metrics view, a key naming a bonus dialog labelled as a scheduling step, a fixtures key labelled as an admin screen, and an auto-clustered key labelled as an editor.

Find what marks an auto-clustered label confirmed. Do not hand-edit the map.

### 4. Ranking, and coverage

#938 shares the budget and dedupes `more`. It does not rank product surfaces ahead of generic ones before the budget is spent. Within a section, items stay in the order they were given.

`coverage.gaps == coverage.scope` (for example 292/292 on one feature) was left as intent uncovered. Confirm that before changing coverage.

`context_lenses` without `limit` still returns the whole answer and can exceed the MCP transport. That is the paging contract. Callers pass `limit` and `offset`. Do not add a silent character cap.

### 5. What the token budget consumes

Answer these in the code or the docs the query command points at:

- What the `tokens` estimate on each item counts.
- Whether any model call is involved. `provenance: deterministic` suggests none. Confirm it.
- The default when `budget` is omitted.
- The crowding of records by graph items is already the even share on #938. Do not re-explain it by changing `FillOrder`.

### 6. Onyx — owner, not a session

Live failure, unchanged by #938:

- `SND-CONTEXT-050`: Onyx v4.9.0 on `127.0.0.1:3189` refused the key on `GET /api/me` (HTTP 403). The key is missing, wrong, or lacks the admin role `POST /api/admin/search` needs.
- The same message says sessions do not search it until `context.repoTeams` maps its teams to a repo.
- `lastSync: null`, `records: 0`.
- Onyx was on a port shared with another local dev server, and another tool has also been seen there. Check what actually answers before treating the 403 as only a bad key.

The owner sets `context.sources.onyx.token` to a dedicated admin service-account key, re-approves the profile (spec §8.8), sets `context.repoTeams`, and uses a port that does not clash. A session cannot approve the profile. Print the commands and stop.

### 7. Concept nodes

#938 adds the single-feature word match. It does not seed concept nodes. `context_node` for a product-area name and for a lowercase two-word phrase failed with `SND-CONTEXT-003` because the graph had no concept node for either. Suggestions were code paths.

Seed domain concepts from the repo's `CONTEXT.md` glossary, as a general mechanism. Do not hardcode any names.

### 8. Suggestions

#938 sorts test-like keys and labels last. They still appear when nothing else shares the words. Prefer source files and product surfaces, and keep tests out of the suggested anchors when a non-test node shares the words.

## Reproduce

Against a work repo's Sindri instance (map and repo-docs versions as recorded when found):

- `context_sources`
- `context_query` with a long natural-language question about one product area and `budget=5000`
- `context_lenses` with `anchor=feature:<key>` for a feature whose lens answer was ~1 MB (pass `limit` and `offset`)
- `context_node` with a product-area name and with a lowercase phrase, neither an exact key

## Original findings (2026-10-24)

Found while using Sindri MCP `context_*` to list one product area's surfaces.

### Catch-all files tagged to many features

- One shared combobox component in a deprecated UI package (all 9 `Combobox*` surfaces) is `part-of` 4 auto-clustered features.
- App-shell components (chat widget, nav button, dropdowns, banners) and a shared layout component under a routes folder are each in 5-6 features.
- Icon files in an icons package are in several features, including auto-clustered ones.
- Shared admin shells (nested nav, tab select, tab shell, vertical nav) and a small display component are owned by a single product feature.
- The first code files under an auto-clustered feature (named for a self-scheduling surface) are a server API router, then its treaty, health-check router, crons router, and a cron job.
- Test files count as implementers: a feature is implemented-by a unit test file under a `Demo/.../__tests__/` folder.

### Query noise

`context_query` with budget 5000 used 4997 and returned `truncated: true`. The budget went on about 60 generic surfaces and left no room for the specific records. The `more` arrays repeated `"C1"` hundreds of times.

`context_lenses` for one feature returned 996,259 characters (scope 292, gaps 292, own 292, edges 1,387, neighbors 108) and spilled past the MCP output cap.

## Done when

- Re-running the four repro calls, after the owner rebuilds the map, returns surfaces specific to the queried area first, without primitives, icons, routers, tests, or demo fixtures.
- `context_lenses` with a `limit` stays in one MCP result and sets `page.more` when another page exists. A call that omits `limit` is still the whole answer.
- Onyx shows `state: ok` with `records` greater than 0. That step is the owner's.
- The ignore list is configuration, applied at map build and at query time.
- Auto-clustered `root.package-NN` labels are not stored as human-confirmed until someone confirms them.
- Glossary concept nodes match a product-area name and a lowercase phrase without an exact key.

## Next steps

1. Read #938 and this file. Branch from the #938 branch if that pull request is still open, otherwise from `main`.
2. Fix sections 3, 4, 7, and 8 in code, plus the leftovers in section 1. Sections 1-2 and 6 are handled on #938; section 5 is moot once the budget is removed.
3. Prove each fix with a failing test first. Keep the role globs and the paging contract.
4. Open a new pull request. Do not add the fixes to #938 and do not merge either pull request.
