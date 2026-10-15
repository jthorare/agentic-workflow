# U1 plan — catalog, headers and freshness (UI spec §18 U1)

Branch: `feat/sindri-ui-u1`, worktree `.claude/worktrees/sindri-ui-u1`, from `feat/sindri-ui-u0` @ 4e860fa7 with
`origin/feat/sindri-api-p1-cqrs` (API P1, #181, open) merged in (0e9c849d, merge not rebase). Stacks on #184 (U0) and
#181. P2 isn't built: its routes and fields are **spec-shaped fixtures** (listed below), to be replaced by P2's goldens.

Read first: UI spec rev 7 §1 (U2, U3, U10, U11), §2 rules, §3.2 (top bar, health pill), §4.1, §4.8 (health line),
§7 (control renderer, copy block), §8 (palette, `?`, keys), §9.1–§9.2, §10.1, §12.4, §14, §18 U1, §18.1 (E-8, E-14,
E-15, E-17), §18.2; API spec §2.2–§2.4, §3.1 (`/status`, `/commands`, `/services`, `/profile`), §3.2 (`RebuildViews`),
§3.3, §3.6 (`EmptyText`), §5, §6 (headers, the human token), §9; `docs/csharp/sindri-ui.md`; `.agents/rules/csharp.md`
and `csharp-testing.md`; AGENTS.md. P1's goldens: `tests/Aw.Sindri.Tests/Golden/Api/**`.

Rules (as U0): TDD — a red view-model or client test first, committed red where it is the slice's first commit, then
green; then the view; then a headless view test with key frames (`HeadlessUnitTestSession` workaround). View models
carry no Avalonia types and hold no logic beyond mapping API fields to words and visibility. Compiled bindings only,
no hex outside `Theme/`, every control named. Every timing rule on the injected `TimeProvider`. All dotnet through
`~/.agentic-workflow/bin-r1b/heavy`. Never push, never open a PR.

## What P1 already gives (no fixture needed)
- `GET /commands` with E-8 (`destructive`, `readsViews`, `shortcut`) and E-16 (checked `cli`): golden `commands/catalog.json`.
- `GET /status` with `apiVersion`: goldens `status/running-*.json`.
- The human token: `serve` mints `<home>/auth/human.token`; a `human` call without `X-Sindri-Token` (ping, status and
  events included) is 403 `SND-AUTH-001` (golden `status/human-no-token.json`). **U0's client sends `human` with no
  token, so on top of P1 the app reads nothing until U1 adds the token.**
- ProblemDetails: `title` = SND code, `detail` = message with `(fix: …)`.

## Unit C — contract (first, alone)
1. **Headers and token** (§9.1, API §6). Port `Platform/IHumanTokenSource` (`Location`, `Read`) in `SindriPlatform`; the
   desktop adapter reads `<home>/auth/human.token` (beside `control.json`, same home rule). `SindriApiClient` reads it
   before each request (a restart mints a new one) and sends `X-Sindri-Token`; never `X-Sindri-Terminal` or `Origin`.
   A missing token file sends no token, so the API answers `SND-AUTH-001` and the app shows it.
2. **Problem answers.** A non-2xx with a ProblemDetails body becomes that error (`title`, `detail` as is); anything else
   stays `SND-UI-003`.
3. **Records** (`Api/*`, `SindriUiJsonContext`): `CommandList`/`CommandRowView` (P1 golden shape), `AsOfView`
   (`cursor, ledgerHead, projectedAt, rebuilding, stalled`), `StatusView.Views?` and `StatusView.Health?`
   (`HealthView(word, problems[{code,text}])`, E-14), `ServiceList(services, empty, asOf)` with `ServiceRunView`,
   `ProfileStateView` (`GET /profile`), `CommandAcceptedView(commandId, sequence)`.
4. **`ISindriApi`** gains `Commands`, `Services`, `Profile`, `RebuildViews` (POST `/sindri/views/rebuild`); the client
   remembers the last accepted command's `sequence` and sends it once as `X-Sindri-After` on the next query.
5. **Goldens as the fake's wire** (E-17): `GoldenFiles` in `Aw.Sindri.Ui.Testing` reads P1's goldens from
   `tests/Aw.Sindri.Tests/Golden/Api` and U1's spec-shaped ones from `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped`; a
   contract test deserializes every golden through `SindriUiJsonContext`; `FakeSindriHandler` serves them for
   `/commands`, `/services`, `/profile`, `/views/rebuild`, enforces the token like P1, and records every header.
6. Red tests for Units A and B; labels (§12.4) for the new copy; `deps.yaml` notes (token file, goldens).

## Unit A — freshness, health pill, controls (after C)
- **asOf freshness** (U10): the pill's "updated Ns ago" counts from `views.projectedAt` when the status carries it,
  else from the last event/ping as in U0; the 30 s stale rule is unchanged.
- **Health pill from E-14:** `health.word` → "Healthy", "Degraded", "Stalled", "Rebuilding" (app words for the API's
  enum), then the age, then each `health.problems[].text` as served. Connection states (Not running, Stale,
  Unreadable, Version mismatch, Connecting) win, as in U0. No `health` (P1) → U0's "Running".
- **Views banner** (§4.1 Stalled / Rebuilding): when `GET /services`' `asOf` is stalled or rebuilding, a banner under
  the top bar with that view's `empty` sentence; Stalled carries **Rebuild views** (the catalog's `RebuildViews` row
  through the control renderer), Rebuilding is a progress banner. Rows stay, marked stale.
- **Control renderer** (`ViewModels/Controls/*`, §7, U3): `ControlViewModel` from a catalog row: `ready` → a button;
  `pending:<phase>` → disabled button plus a focusable `InkSubtle` line "arrives in <phase> · <cli>" with Copy;
  `cli-only:<reason>` → the copy block (terminal glyph, the `cli`, Copy, "Needs a typed confirmation at a terminal.").
  The backstop refuses to show or copy a `cli` with a control or bidi character.
- **Command lifecycle** (§4.1): `sending` (disabled, label kept, spinner; "Sending…" announced after 300 ms; a second
  click does nothing), `accepted` (check + "<label> done" style outcome built from the action label, 2 s, announced),
  `refused`/`failed` (inline `title` and `detail` as is; focus stays), Copy ("Copied" 1.5 s). Clipboard is a port
  (`Platform/IClipboard`, Avalonia adapter in `Views`).
- **Refresh on every event** (§9.2): each stream event re-queries status, services and profile, debounced to one per
  view per 250 ms; the catalog is read on connect and on a new epoch. (E-15 `invalidates` waits for P5.)
- Views: the banner in `ShellWindow`, the pill's word/problems in `TopBar`, Health page: profile view (`GET /profile`)
  and the catalog's pending rows ("N actions arrive in later phases", listed by phase).

## Unit B — palette and `?` sheet (after C, parallel with A)
- `ViewModels/Palette/CommandPaletteViewModel` + `Views/CommandPalette*`: `⌘K` / `Ctrl+K` (and a click on the ⌘K
  field) opens it; sections **Actions** (every catalog row not in Sindri, each with its status: ready, "arrives in
  <phase>" disabled, or "Copy: sindri …"), **Go to** (Home, Queue, Decisions, Services, Health, Settings with keys),
  **Sindri** (Rebuild views, Stop Sindri, Health). Fuzzy match on labels and ids; ↑/↓, Enter, Esc closes and returns
  focus. U1 has no selected row, so Actions lists the catalog (spec question Q1). Stop Sindri goes to Health (§4.8:
  it lives there; the confirm is U4) (Q2).
- `?` sheet: §8's keys and descriptions plus the catalog's `shortcut`s (none yet); Esc closes.
- Palette history (`paletteHistory`, U0's preference) records run entries.

## Unit D — fixture script (parallel)
`scripts/sindri-ui-fixture.sh` (first part; §18.2), always through `scripts/sindri-prove.sh` (its guard):
`up` (profile with heartbeat, approve as human, start), `stall` (insert one row of a registered type with an
unparseable payload into `ledger.db` with `sqlite3`), `unstall` (delete it), `drift` (edit the live profile),
`down`. `--large` is reserved. `scripts/tests/sindri-ui-fixture.test.sh`: the guard, usage, and the stall SQL against
a temp sqlite file (no dotnet). AGENTS.md lists it.

## Integration
Docs (`docs/csharp/sindri-ui.md` U1 section), `deps.yaml` (Sindri `files` gains `sindri-app/auth/human.token`; the
SindriUi file edge note names the token and the goldens), PublicAPI (new public port), the merge gate subset the lead
asked for. Proof: app against the fake (golden-serving handler) in the integration lane with key frames; the running
app against a P1 control plane for headers/token/catalog/palette; P2-dependent steps pending.

## Spec-shaped fixtures (replace with P2 goldens)
`status/running-healthy.json`, `status/running-stalled.json`, `status/running-rebuilding.json`,
`status/running-degraded.json`, `services/fixture.json`, `services/stalled.json`, `services/rebuilding.json`,
`profile/approved.json`, `views-rebuild/accepted.json`, `commands/catalog-p2.json` (P1's catalog with `RebuildViews`
`ready`).
