# The design library page

`GET /sindri/library` is the read behind the Reviews page's library sheet (PR review page spec `docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md` §6.1, §6.3). It lists what the design-doc library holds and what is wrong with it.
The Reviews header's one-line library state (`library` in `GET /sindri/reviews`) reads the same sources; this route is the long form.

## The route

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
- `nearMisses` lists files that look like design docs but are not read (`SND-LIB-019`). Context does not hand out the finding yet (it keeps only a count on the root's status), so the list is empty until context adds the finding to `LibraryRootStatus`.

## The cap

At most **200** docs with errors and **200** near misses are listed. Past that, the lists keep their first 200 in key order and `cappedText` says so:

- `Showing the first 200 of 250 docs with errors; run `sindri library validate` for the rest`
- `Showing the first 200 of 300 near misses; run `sindri library validate` for the rest` (when both lists are cut, the two sentences are joined by `. `)

`cappedText` is null when nothing was cut. The counts (`docs`, `withErrors`) are never cut.

## A session

A session caller reads the same body with two differences:

- every `broken[].path`, `broken[].errors[].text`, `nearMisses[].path` and `nearMisses[].text` is inside the untrusted fence (source `sindri:library`; spec §10): a design doc's words are not Sindri's;
- `unreadableRoots` is `[]`: a folder's path and why it can't be read belong to the owner. (Context also refuses a session the root states, `SND-CONTEXT-020`; the Reviews header treats that as no folders, not as an error.)

A session reads only the docs its audiences allow, so its counts can be smaller than the owner's.

## The Reviews header

`ReviewLibrary` (the `library` object of `GET /sindri/reviews`) takes the same root states: an unreadable root becomes an `unreadable[]` entry, a root still `Reading` (or a library source whose first read has not finished) makes `state` `reading`.
