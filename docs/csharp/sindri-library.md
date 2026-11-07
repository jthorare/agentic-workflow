# The design-doc library: routes, verbs and codes

The library is the design docs Sindri reads from the `aw` state folder, the vaults and the repos (PR review page spec `docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md` §4, §5). This page holds what a client calls.

## Routes

| Route | Answer |
|-------|--------|
| `GET /sindri/library/{doc}` | One doc by its front-matter `id`: `{asOf, doc: {id, root, scope, kind, status, project, repos, issues, title, path, errors: [{rule, text}]}}`. `path` has the home folder written `~`. `errors[].rule` is the `SND-LIB-nnn` code, `text` is the finding's message with the file and line written `<path> line <n>:`, then the fix. Two scopes can share an id (the `aw` root keeps one scope per repo-slug): the doc at the lowest path answers, which is the one that keeps the id (spec §4.2), and the other shows `SND-LIB-012` in its list entry |

A session caller gets the same body with `doc.title`, `doc.path` and each `doc.errors[].text` inside the untrusted fence (source `sindri:library`); the rest stays plain. A doc the caller's audience may not read is as good as missing.

## Verbs

| Verb | What it does |
|------|--------------|
| `sindri library show <doc> [--json]` | `GET /sindri/library/{doc}`. Never starts the control plane |
| `sindri library validate <path\|dir> [--root <name>] [--json]` | Runs the design-doc rules over a file or folder in this process: `DesignDocParser` per `*.md` (symlinks and dot folders skipped), then `DesignDocScope.Check` (012, 015, 016) over the docs found. All docs share the scope `--root` names (default `local`). Exit 0 no findings, 1 findings, 2 usage or an unreadable path. `--json` prints `{findings: [{rule, file, line, message, fix}], passed}`. `--pr-body`, `--repo` and `--docs` need the stored library and are refused with exit 2 "not yet" |

## Codes

| Code | Meaning |
|------|---------|
| `SND-LIB-404` | No design doc the caller may read has that id: "No design doc '<id>'; `sindri library list` names them" (HTTP 404) |

The per-doc codes `SND-LIB-010`..`019` are the convention's (`docs/conventions/design-docs.md`).
