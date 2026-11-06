# Design-doc front matter (shared)

officeHours, system-design and specToProvenPR write their docs in the design-doc convention (`docs/conventions/design-docs.md` in agentic-workflow), so Sindri can itemize them and a PR's `Covers:` line can claim their phases and requirements. This file is the part the three skills share.

## The block

The file's first line is `---`; the block holds these keys in this order, then `---`:

```yaml
---
aw-doc: 1
id: harbor-booking
kind: plan
title: "Harbor ferry booking"
status: draft
repos: [acme/harbor-web]
---
```

- `aw-doc: 1`: unquoted, exactly this.
- `id`: lowercase letters, digits and `-`. Make it from the slug, never the title: lowercase, anything else becomes `-`, repeated `-` squeezed, cut on a `-` boundary so the id plus any suffix the skill adds (`-product`, `-stages`, `-2`) stays within 64 characters. At least 3 characters.
- `kind`: the skill says which (`plan`, `product`, `engineering`, `design`, `tasks`, `tdd`, `stages`, `verification`).
- `title`: always in double quotes, with any `\` inside written as `\\` and any `"` as `\"`.
- `status: draft`.
- `repos`: the repo the skill runs in, from `gh repo view --json nameWithOwner -q .nameWithOwner`. When that fails (no `gh`, no GitHub remote), leave the key out; never guess.
- `issues`: only when the user named GitHub issues (plain numbers, `[412]`) or Linear keys (`[ENG-12]`). Otherwise leave it out.
- `parent` and `supersedes`: only where the skill says so.

## The check

The last step of a skill that wrote docs:

```bash
if command -v sindri >/dev/null 2>&1 && sindri library validate --help >/dev/null 2>&1; then
  sindri library validate "$OUT" --json
else
  echo "Skipped the design-doc check: sindri library isn't installed"
fi
```

`$OUT` is the file or folder the skill wrote. Fix every error it names (the code's fix is in the convention page's Errors table) and run it again. The skip line is not an error: carry on.
