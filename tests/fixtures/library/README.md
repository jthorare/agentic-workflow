# Design-doc fixtures

Made-up docs (acme, harbor, ferries) for the design-doc convention, `docs/conventions/design-docs.md`. This README is not a design doc and has no front matter.

- `aw/` is an `aw` root: `aw/<repo-slug>/plans|proof|system-design/...`. Two repo-slugs (`acme-harbor`, `acme-ledger`) share a doc id.
- `vault/` is a vault root named "Dock Notes": a `.obsidian/` folder, a `.trash/` folder, a `private/` folder the vault denies, a symlink and a note without front matter.
- `repo/` is a repo root (`repo:harbor-web`): the spec the stage map's `parent` points at.
- `broken/` is a second vault with one broken doc per code SND-LIB-010 to SND-LIB-018 (019 is the near misses under `aw/`).
- `expected.json` is the contract: every `.md` here with whether it is read, its errors, its item ids in document order, and the refs where they are the point. The C# parser's tests run over the same folder against the same file, 
  - `refs` lists only the refs a fixture asserts; a missing `refs` key means not asserted. Refs are given after scope resolution (`to` is `<doc id>#<item>` when it resolves to another doc in the scope), carry the 1-based `line` in the fixture, and validate against `$defs/ref`.
  - `parent` and `supersedes` are not refs: they are `frontMatterLinks`.
  - The SND-LIB-019 entries are what `validate` reports; nothing from those files is stored. Each 010 entry names `badKey`, the one instance path its front matter fails at.
  - `scripts/check-library-fixtures.mjs` proves the fixtures say what the file says.

The over-1-MiB case of SND-LIB-018 is not a fixture (the 2,000-item case is); a parser test makes that file itself.

## Add a fixture

1. Write the `.md` (made-up names only). A generated doc's `source-sha256` is the sha256 of its source file: `sha256sum packet.json`.
2. Add its entry to `expected.json` (root, scope, read, errors, items, why). A new non-md file goes in `supporting`; a new code or error goes in `codes` too.
3. Run `bash scripts/tests/check-library-fixtures.test.sh`.
