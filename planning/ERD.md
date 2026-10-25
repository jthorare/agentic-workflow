# Entity-Relationship Diagram -- SQLite stores

Every SQLite store in the toolkit is C#, opened through `Aw.Kernel.Data` (WAL, migrations embedded in the owning package
and never edited once merged, the schema version in `PRAGMA user_version`). The schemas are defined by those
migrations; this file is the map to them. The TypeScript ledger and code-index schemas that used to be drawn here left
with the TypeScript packages at the switch, and no TS state is migrated.

| Store | Path | Owner | Schema and contract |
|-------|------|-------|---------------------|
| Sindri ledger | `$AW_SINDRI_HOME/ledger.db` | `Aw.Sindri.Infrastructure` | `events`, `lease`, `outbox_commands`; Sindri is the only writer, hooks append to spool files instead (`docs/csharp/sindri.md` "State") |
| Sindri views | `$AW_SINDRI_HOME/views.db` | the projector | `projector_state` and one `view_<name>(key, row_json, seq)` table per view; derived, rebuilt from the ledger by `sindri views rebuild` (`docs/csharp/sindri.md`) |
| Temporal dev server | `$AW_SINDRI_HOME/temporal.db` | the pinned `temporal` binary | Not ours; the workflow engine's history (`docs/csharp/sindri-workflow.md`) |
| Code index | per repo, under the Sindri home | `src/CodeIndex` | Symbols, callers and callees; derived data (`docs/csharp/code-index.md`) |
| Judge decisions | `$AW_STATE_DIR/judge/decisions.sqlite` | `Aw.Judge.Infrastructure` | `decisions`, `decision_details`, `failures`, `briefs` (`docs/csharp/judge.md` "State") |

`$AW_SINDRI_HOME` defaults to `$AW_STATE_DIR/sindri-app`.
