---
milestone: "M4 · Agent-first building"
milestone_note: "public data blocks and Jev; can run beside PR 11"
title: News, Vulnerabilities and Jev blocks
change: Three blocks that need no sign-in or only the judge's key: News (Hacker News, RSS), Vulnerabilities (OSV.dev) and Jev (the judge's HTTP client), each behind a provider port with a host allowlist
rollback: Revert the PR; blocks are registry entries, a spec that uses one fails validation with SND-GRAPH unknown block. The judge package move reverts with it
---
# PR 9b: News, Vulnerabilities and Jev

## Summary
Three blocks from the owner's list (2026-11-08-sindri-block-list.md). **News** (`news`) reads Hacker News or any RSS feed
behind `INewsProvider`. **Vulnerabilities** (`vulnerabilities`) asks OSV.dev's batch API about packages, or about a repo's
lockfiles, behind `IVulnerabilityProvider`. **Jev** (`jev`) calls the same HTTP API the judge calls
(`POST https://api.typesafe.ai/v1/systemone`) with questions of type `choice`, `score` or `noul`. Adding a news site or
a second vulnerability source is one class plus its host in the allowlist. Public reads need no connection. Jev uses the
`jev` connection preset: a `rest` connection with that URL and the key from `TYPESAFE_API_KEY` (environment), else the
judge's keychain entry, so one key serves both. The block redacts what it sends (`Aw.Redaction`) and caps it at 160,000
characters, as `JevProvider` does, because Jev is a third party. Errors use the judge's reason codes: `no-api-key`,
`network-error`, `http-NNN`, `timeout`. This PR can run beside PR 11.
Rollback: revert.

## Code changes
| path:line | change |
|---|---|
| src/Sindri/Aw.Sindri.Core/Graph/Blocks/NewsBlock.cs, NewsConfig.cs, INewsProvider.cs (new) | config: `provider` (`hackernews` default, `rss`), `feed` (Hacker News: `top`, `new`, `best`, `ask`, `show`; RSS: a URL), `query?`, `limit` (default 30, max 200), `since?`; outputs `items[]` (`title`, `url`, `source`, `score?`, `comments?`, `publishedAt`, `id`), `count` |
| src/Sindri/Aw.Sindri.Infrastructure/Workflow/News/HackerNewsProvider.cs, RssNewsProvider.cs (new) | plain `HttpClient`; hosts from an allowlist, no redirects to other hosts, loopback refused, 1 MB cap; item text marked untrusted and fenced wherever it reaches a prompt |
| src/Sindri/Aw.Sindri.Core/Graph/Blocks/VulnerabilitiesBlock.cs, VulnerabilitiesConfig.cs, IVulnerabilityProvider.cs (new) | config: `provider` (`osv` default) and either `packages[]` (`ecosystem`, `name`, `version`) or `repo`; outputs `vulns[]` (`id`, `aliases`, `summary`, `severity`, `package`, `version`, `fixedIn`, `url`), `count` |
| src/Sindri/Aw.Sindri.Infrastructure/Workflow/Vulnerabilities/OsvProvider.cs, LockfileReader.cs (new) | OSV batch query (no key); `LockfileReader` reads `packages.lock.json`, `package-lock.json`, `go.sum`, `Cargo.lock`, `poetry.lock` from the profile repo |
| src/Sindri/Aw.Sindri.Core/Graph/Blocks/JevBlock.cs, JevConfig.cs (new) | config: `connection` (the `jev` preset), `model` (default: the judge's pinned model, today `jev-1.13.0`), `text`, `questions[]` (`name`, `type`, `instructions`, `criteria`); outputs one field per question (`choice`, `confidence`, `probabilities`), plus `model` and `usage` (`input_tokens`, `output_tokens`) |
| src/Sindri/Aw.Sindri.Infrastructure/Workflow/Jev/JevClient.cs (new), ConnectionPresets (PR 10) `jev` preset | calls the shared wire code with the preset's key; redact, then cap at 160,000 characters; judge reason codes on failure |
| **New deps.yaml edge:** the judge's Jev wire code (JevRequest, JevResponse, the question records, the input cap; internal in src/Judge/Aw.Judge.Infrastructure/Providers today) moves to a new shared package that Aw.Judge and Aw.Sindri both reference (the Aw.Redaction precedent) | declare the package and both edges in `deps.yaml`, run `node scripts/check-deps.mjs`; the judge's tests and PublicAPI files move with the code; `scripts/scaffold` (/new-package) creates the package; update `src/Judge/aot-smoke` only if its output changes |
| src/Sindri/Aw.Sindri.Core/Profile/WorkflowSettings.cs:31 | host allowlist keys for the News and Vulnerabilities providers; profile rules, ProfileDiff keys and docs/sindri/profile.md follow (`check-profile-doc` bash test) |
| docs/workflows/blocks/news.md, vulnerabilities.md, jev.md, docs/csharp/judge.md "Jev" | one example spec per block; the judge doc points to the shared package |

## Graph context
- `JevProvider` in the judge is the reference for redaction, the 160,000-character cap and the reason codes; this PR moves its wire types, not its behavior.
- The judge's key lookup (`TYPESAFE_API_KEY`, else the keychain entry) is reused by the `jev` preset; no second key is stored.
- Provider ports follow the block pattern of PR 6: a typed C# module, config record, declared outputs and an example.
- Readers of the block list: `workflow_blocks` (PR 8) and the canvas picker (PR 17), through `title` and `group` (Data for News and Vulnerabilities, AI for Jev).

## Tests
- NewsBlockTests.Hacker_News_top_returns_items_in_feed_order_and_honors_limit_and_since: fake handler, no network.
- NewsBlockTests.A_host_off_the_allowlist_and_a_cross_host_redirect_are_refused: SND-GRAPH reason.
- VulnerabilitiesBlockTests.A_package_list_maps_OSV_results_to_vulns_with_fixedIn: fixture JSON.
- LockfileReaderTests.Each_supported_lockfile_yields_ecosystem_name_and_version: five fixtures.
- JevBlockTests.Questions_go_out_redacted_and_capped_at_160000_characters: secret absent from the request body.
- JevBlockTests.A_missing_key_fails_with_no_api_key_and_a_503_with_http_503: judge reason codes.
- JevBlockTests.Each_question_becomes_one_output_field_with_choice_confidence_and_probabilities: fake Jev server.
- DepsGraphTests.The_shared_Jev_package_is_declared_and_both_edges_pass_check_deps: `node scripts/check-deps.mjs`.
