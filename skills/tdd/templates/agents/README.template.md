{{header}}

# {{title}} · agents lane

> {{summary}}

Status **{{status}}** · rev {{rev}} ({{rev_note}}) · updated {{updated}} · code base {{repo}} at `{{sha}}` ({{sha_date}}). Built from `{{source}}` at {{generated}}. Every file here is generated: edit the TDD source and rebuild, never these files.

## Read this first
1. This file. 2. Only the slice your task needs (table below). 3. Follow `{#id}` links when a slice points elsewhere.
- Citations are `path:line` at sha `{{sha}}`: re-read the file at that sha (`git show {{sha}}:<path>`) before editing; if it drifted, report it, do not guess.
- A decision whose state is `open` needs its owner. Do not build past it; stop and ask.
- Slices are under ~{{budget}} tokens (estimate: chars/4). `tokens.json` has the count for every file.

## Which file for which task
| Task | Read |
| --- | --- |
| Build a PR | [prs/prs.md](prs/prs.md), then `prs/pr-NN.md`, its `decisions/`, the `sql/` and `code/` it names |
| Iterate on a section | `sections/NN-<id>.md` (front matter lists its anchors; footer defines what it references) |
| Semantic search, embeddings | [tdd.txt](tdd.txt) (one paragraph per line) or `sections/*.md` |
| Find a fact | `grep -rn "<term>" sections/`; anchors are written `{#id}` |
| SQL review | `sql/*.sql` ({{n_sql}}), [schema/schema.md](schema/schema.md) when present |
| Image review | [images/index.json](images/index.json), then `images/diagrams/<key>.md` (every box's text) |
| Check a citation | [citations.md](citations.md) (by file), [citations.json](citations.json) |
| Follow D3, E1, PR 7 | [refs.json](refs.json): token -> anchor -> slice |
| Program over the design | [map.json](map.json) (entities and edges) |

## Sections
{{sections}}

## Decisions
{{decisions}}

## Files
| File | What | ~tokens |
| --- | --- | --- |
{{files}}

## Counts
{{counts}}

Path aliases used on the page (written out in full here): {{aliases}}. Tokens with no definition on the page: {{unlinked}}.

## Build notes
{{notes}}
