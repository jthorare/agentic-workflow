# The design-doc library: routes, verbs and codes

`GET /sindri/library` is the read behind the Reviews page's library sheet (PR review page spec `docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md` §6.1, §6.3). It lists what the design-doc library holds and what is wrong with it.
The Reviews header's one-line library state (`library` in `GET /sindri/reviews`) reads the same sources; this route is the long form.

## The list: `GET /sindri/library`

A query (`GetLibrary` in `Aw.Sindri.Features/GetLibrary/`). It reads stored data only: nothing re-reads a folder.

```json
{
  "asOf": { ... },
  "docs": 41,
  "withErrors": 3,
  "broken": [ { "doc": "export-plan", "path": "~/notes/design/export-plan.md", "errors": [ { "rule": "SND-LIB-015", "text": "..." } ] } ],
  "nearMisses": [ { "path": "~/notes/design/retry-notes.md", "rule": "SND-LIB-019", "text": "..." } ],
  "unreadableRoots": [ { "root": "~/notes/vault", "reason": "permission denied" } ],
  "cappedText": null
}
```

- `docs` is every doc the caller may read; `withErrors` is how many of them carry an error, however many `broken` lists.
- `broken[].doc` is the doc's front-matter `id`, or its file name when the front matter has none. `path` is the file with the home folder written `~`.
- An error's `text` is context's message, then its fix, joined by `. ` (a trailing `.` of either is dropped), with the record key in the message (`doc:<root>/<path>`) replaced by the file.
- `unreadableRoots[].root` is the folder as the profile names it, home folded to `~`; `reason` is the one context's root status holds.
- `nearMisses` lists files that look like design docs but are not read (`SND-LIB-019`), sorted by `path`: the file under the folder the last pass read (home written `~`), the rule, and `text` built like an error's (`<path> line <n>: <message>. <fix>`, without ` line <n>` when there is no line). A near miss stored before Context kept the finding's text reads as its message alone, `a near miss (read again on the next pass)`, until the next pass. A session gets `[]`.

## The cap

At most **200** docs with errors and **200** near misses are listed. Past that, the lists keep their first 200 in key order and `cappedText` says so:

- `Showing the first 200 of 250 docs with errors; run `sindri library validate` for the rest`
- `Showing the first 200 of 300 near misses; run `sindri library validate` for the rest` (when both lists are cut, the two sentences are joined by `. `)

`cappedText` is null when nothing was cut. The counts (`docs`, `withErrors`) are never cut.

## A session

A session caller reads the same body with two differences:

- every `broken[].doc`, `broken[].path`, `broken[].errors[].text`, `nearMisses[].path` and `nearMisses[].text` is inside the untrusted fence (source `sindri:library`; spec §10): a design doc's words are not Sindri's, and a broken doc's id is whatever its front matter says;
- `unreadableRoots`, `roots` and `nearMisses` are `[]`: a folder's path, why it can't be read and the files it holds that aren't read belong to the owner. (Context also refuses a session the root states, `SND-CONTEXT-020`; the Reviews header treats that as no folders, not as an error.)

A session reads only the docs its audiences allow, so its counts can be smaller than the owner's.

## The Reviews header

`ReviewLibrary` (the `library` object of `GET /sindri/reviews`) takes the same root states: an unreadable root becomes an `unreadable[]` entry, a root still `Reading` (or a library source whose first read has not finished) makes `state` `reading`.

## One doc: `GET /sindri/library/{doc}`

One doc by its front-matter `id`: `{asOf, doc: {id, root, scope, kind, status, project, repos, issues, title, path, errors: [{rule, text}]}}`. `path` has the home folder written `~`. `errors[].rule` is the `SND-LIB-nnn` code, `text` is the finding's message with the file and line written `<path> line <n>:`, then the fix. Two scopes can share an id (the `aw` root keeps one scope per repo-slug): the doc at the lowest path answers, which is the one that keeps the id (spec §4.2), and the other shows `SND-LIB-012` in its list entry

A session caller gets the same body with `doc.id`, `doc.kind`, `doc.status`, `doc.project`, each of `doc.repos` and `doc.issues`, `doc.title`, `doc.path` and each `doc.errors[].text` inside the untrusted fence (source `sindri:library`): a broken doc keeps a front-matter value of the wrong shape as written. `doc.root`, `doc.scope` and `errors[].rule` stay plain. A doc the caller's audience may not read is as good as missing.

## Verbs

| Verb | What it does |
|------|--------------|
| `sindri library list [--errors\|--near-misses] [--json]` | `GET /sindri/library`. Line 1 `Library: 41 docs, 3 with errors` (`Library: no design docs yet` when empty), then each broken doc and its findings, the near misses, the unreadable folders and `cappedText`. `--errors` prints only the broken docs, `--near-misses` only the near misses (`No docs with errors.` / `No near misses.` when empty). Exit 0 even when docs have errors; never starts the control plane |
| `sindri library show <doc> [--json]` | `GET /sindri/library/{doc}`. Never starts the control plane |
| `sindri library validate <path\|dir> [--root <name>] [--json]` | Runs the design-doc rules over a file or folder in this process: `DesignDocParser` per `*.md` (symlinks and dot folders skipped), then `DesignDocScope.Check` (012, 015, 016) over the docs found. All docs share the scope `--root` names (default `local`). Exit 0 no findings, 1 findings, 2 usage or an unreadable path. `--json` prints `{findings: [{rule, file, line, message, fix}], passed}`. `--pr-body`, `--repo` and `--docs` need the stored library and are refused with exit 2 "not yet" |

## Codes

| Code | Meaning |
|------|---------|
| `SND-LIB-404` | No design doc the caller may read has that id: "No design doc '<id>'; `sindri library list` names them" (HTTP 404) |

The per-doc codes `SND-LIB-010`..`019` are the convention's (`docs/conventions/design-docs.md`).
