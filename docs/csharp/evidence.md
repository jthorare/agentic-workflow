# Evidence — `ui-evidence`

`Aw.Evidence` (`src/Evidence`, tier 2, full shape: `Core`, `Features`, `Infrastructure`, `Host`) drives a web app in a
headless browser and records what it saw: a scripted run's steps, screenshots, lint findings and visual verdict
(`summary.json`), and design parity against exported design frames (`parity.json`). The binary is `ui-evidence`
(`Aw.Evidence.Host`, framework-dependent). Until the switch the installed toolkit keeps running the TypeScript
`skills/ui-evidence`; nothing installs or calls this binary yet ("At the switch" below).

```bash
dotnet run --project src/Evidence/Aw.Evidence.Host -- <command> [arguments]
```

## Commands

Every command binds its arguments, calls one use case and maps the result to an exit code. Arguments are strict: an
unknown flag, a flag without its value (a value never starts with `--`), a repeated flag or the wrong number of
positionals is a usage error. Paths may be relative; the binary makes them absolute against the working directory, and
every path it writes into a run file is absolute.

| Command | Does | Exit |
|---|---|---|
| `run <script.json> <run-dir> [--baseline png] [--app-build sha] [--fixtures id] [--cache verdict-cache.json] [--host url] [--allow-preview-host] [--allow-writes] [--lock-timeout s]` | Once both locks are held, deletes `<run-dir>/summary.json`; runs every step of a UI script in each of its viewports, writes `summary.json` and prints it | 0 every step passed · 2 a step failed or broke · 1 · 4 · 75 |
| `api-run <script.json> <run-dir> --at base\|head [--repo dir] [--app-build sha] [--host url] [--allow-preview-host] [--config file] [--lock-timeout s]` | Deletes `<run-dir>/summary.json`; vets the script (host, redirects, CLI targets, write verbs, environment) before any lock; once both locks are held, runs the HTTP requests, then the CLI calls, in order; writes `summary.json` (`evidence.api.summary/1`) and prints it | 0 every expectation held · 2 one did not · 1 · 4 · 75 |
| `db-run <script.json> <run-dir> --at base\|head [--repo dir] [--app-build sha] [--config file] [--lock-timeout s]` | Deletes `<run-dir>/summary.json`; vets the script (the database target, the pre-run write refusal) before any lock; once both locks are held, opens the database read-only (SQLite: a copy in the run directory; Postgres: after the identity and role checks), runs each query, checks its rows; writes `summary.json` (`evidence.db.summary/1`) and prints it | 0 every expectation held · 2 one did not · 1 · 4 · 75 |
| `desktop-run <screens.json> <run-dir> [--repo dir] [--app-build sha] [--config file] [--screens a,b] [--lock-timeout s]` | Deletes `<run-dir>/summary.json`; reads the screens manifest and the repo's `desktop` config (a problem with either is a refusal); under both locks it makes a scratch directory (passed to every command in the variable `desktop.homeEnv`), runs the fixture steps, then for every screen at every window size starts the app with the UI driver on (`AW_UI_DRIVER=1`, its own `AW_UI_DRIVER_DIR`) and runs the screen's script through `aw-ui-driver`, stops the app, runs `fixture down` (always), then renders each screen's mockup in the browser at the same size and compares the two frames (section "Desktop evidence"). Writes `summary.json`, `report.md` and four images per screen and size; stdout is the summary | 0 every screen within its limit · 2 a screen over its limit, whose script failed or not comparable · 1 refused (usage, manifest, config) · 4 errored (a fixture command, the app or its driver, the browser) · 75 lock busy |
| `parity <manifest.json> <run-dir> [--host url] [--allow-preview-host] [--allow-writes] [--app-build sha] [--db-provenance seeded\|unknown] [--lock-timeout s]` | Once both locks are held, deletes `<run-dir>/parity.json`; measures each design frame, writes `parity.json` and prints it; each failed box check is also a stderr line `ui-evidence: geometry: <frame> <target>: <failure>` | 0 every geometry check passed · 2 one failed · 1 · 4 · 75 |
| `parity-plan <run-dir> [--reviewed file,file]` | Prints the Linear attachment plan (one side-by-side image per frame) | 0 every image reviewed · 3 an image is unreviewed · 1 provenance unknown (AW-EVIDENCE-016: the evidence stays local) |
| `parity-comment <run-dir> --for linear\|github` | Prints the comment as markdown (Linear: a table with geometry; GitHub: a table, images stay on Linear) | 0 · 1 |
| `parity-stale --attachments <file.json> --keep id,id` | Prints the JSON array of `Pixel diff: ` attachments not kept, from a JSON array of `{id, title}` (other fields ignored) | 0 · 1 |
| `parity-put --upload <file.json> --file <png>` | Prints the JSON argv of the signed PUT (`curl … -H "<name>: <value>"` for every signed header, verbatim; run it within a minute of signing) from `{url, headers}` | 0 · 1 |
| `publish-plan <run-dir> --provenance seeded\|unknown [--linear-issue id]` | Prints the publish plan for `summary.json` (below) | 0 · 1 |
| `gh-publish <run-dir> --repo owner/name --pr N [--provenance seeded\|unknown] [--reviewed file,file]` | **Contract only (cutover unit 1.2 builds it; until then it answers AW-EVIDENCE-015, exit 4).** Pushes a run's images (the step screenshots of `summary.json`, else the side-by-side image of each `parity.json` frame) to the PR's evidence branch `ui-evidence/pr-<n>` through `gh api` (the git data API: no checkout, never force-pushed, on top of earlier runs) under `pr-<n>/<run-id>/`, writes `<run-dir>/pr-comment.md` with each image embedded by a commit-pinned `https://github.com/<repo>/blob/<sha>/…?raw=true` URL, and prints `{provenance, branch, commit, urls, commentFile, post}` (`post` is the `gh pr comment` argv). Seeded data only: a `summary.json` run needs `--provenance seeded` on a local host, a `parity.json` run carries its own; every image must be in `--reviewed` first | 0 pushed · 1 bad usage or input, or provenance not seeded (nothing pushed) · 3 an image is not in `--reviewed` (nothing pushed; `unreviewed` lists them) · 4 `gh` failed (AW-EVIDENCE-025) |
| `version` | **Contract only (cutover unit 1.4 builds it).** Prints the build stamp on one line, `ui-evidence commit <sha>, built <time>` (Sindri's stamp: the binary ships in Sindri's install) | 0 · 1 usage |
| `doctor` | Prints `{healthy, failures[]}` for the local stack; each failure is also a stderr line `ui-evidence: doctor: <failure>` | 0 healthy · 2 not · 1 usage |
| `provenance` | Prints `{provenance: seeded\|unknown, reason}` for the local database | 0 |
| `install-browser` | Installs the browser the run and parity commands drive; the installer's download progress goes to stderr, so stdout is only `{installed}`. Ctrl-C returns at once (a download already running finishes in Playwright's driver) | 0 · 4 |

`--lock-timeout` is whole seconds, 0 to 86400 (default 120; 0 tries once), and applies to each lock in turn, so exit 75 can come after up to twice the value; anything else is a usage error. A `--baseline` that doesn't exist is AW-EVIDENCE-010 (exit 1) before any lock is taken; one that exists but doesn't decode as a PNG leaves the verdict `unchecked` (never `unchanged`). `--host` takes a bare origin: `http://localhost:<port>`
or `http://127.0.0.1:<port>` (local: no extra flag), or an https preview host matching `UI_EVIDENCE_PREVIEW_HOST_PATTERN`
with `--allow-preview-host` (the host guard below). Without `--host`, `run` and `parity` drive `http://localhost:3000`; `api-run` has no default host and refuses a script with requests (AW-EVIDENCE-004) unless `--host` names one.

### Exit codes

One scheme for every command:

| Exit | Meaning | Errors |
|---|---|---|
| 0 | Done; every check passed | — |
| 1 | Usage error, invalid input, or a guard refused | `ui-evidence: usage: ui-evidence <shape>`; AW-EVIDENCE-001…006, 010, 012, 016, 017, 018, 019, 020, 021, 022 |
| 2 | A check failed: a step failed or broke (`run`), an expectation did not hold (`api-run`, `db-run`), a box check failed (`parity`), the stack is unhealthy (`doctor`) | the document is still written and printed |
| 3 | Blocked on review (`parity-plan`, `gh-publish`) | the plan is still printed |
| 4 | The run itself errored, or the environment's I/O failed: the browser failed to start or crashed, a frame's anchor is missing, a region runs past the viewport, a file couldn't be read or written (the script included), a lock directory couldn't be made; `api-run`: a request failed, timed out or was redirected off its origin (AW-EVIDENCE-023), a golden file could not be read (011), a CLI call would not start or ran past its limit, or its temporary directory could not be made (AW-EVIDENCE-015, the same code the unexpected-exception line uses); `db-run`: the database cannot be reached or copied (AW-EVIDENCE-024), a script, seed or golden file could not be read (011) or a query fails for a reason other than a refused write (AW-EVIDENCE-015) | AW-EVIDENCE-011, 013, 015, 023, 024 (a stack probe failure, AW-EVIDENCE-014, is never an exit: `doctor` and `provenance` report it in their document) |
| 75 | A machine lock stayed busy past `--lock-timeout` | AW-EVIDENCE-009 |
| 130 | Interrupted (SIGINT, SIGTERM, SIGHUP, SIGQUIT): a browser call in flight is abandoned at once and the locks are released on the way out. A second signal terminates the process without waiting (a lock it held is then left for a person to remove) | `ui-evidence: interrupted` |

The mapping is by error kind: `timeout` → 75, `unavailable` → 4, anything else → 1. An unexpected exception (a bug,
or a failure outside a result) is `ui-evidence: AW-EVIDENCE-015 the run failed unexpectedly: <first line>` and exit 4,
never the runtime's crash exit.

`db-run` returns AW-EVIDENCE-001 (bad arguments), 006 (a `passwordEnv` variable that is unset), 010 (a SQLite source, a seed or golden file, or a `--config` file that isn't there), 022 (an invalid targets config: the repo's `evidence.json` or `--config`), 018 (database script, a SQLite source that isn't a database, or a seed that fails on the run's copy), 020 (database refused: outside the repo, not disposable, a role that can write, a write statement before connecting, or a write the server refused), 021 (server identity mismatch), 024
(database cannot be reached or copied), 011 (a script, seed or golden file that exists but can't be read, or a file that can't be written), 015 (an unexpected exception or a query that fails for a reason other than a refused write) and 009 (a lock that stayed busy, exit 75).

### stdout and stderr

stdout carries exactly one JSON document, written through the Kernel `AwJson` profile (camelCase, UTC `O` timestamps,
nulls written), or markdown for `parity-comment`. stderr carries one-line messages prefixed `ui-evidence: `:
`ui-evidence: <code> <message>` for an error (an invalid script or manifest names the file, the JSON location and
the problem in the input's own terms: `ui-evidence: AW-EVIDENCE-002 script /work/script.json: $.steps[0]: missing
required property "expectedState"`; "Inputs" below), the `geometry:` and `doctor:` lines above, `ui-evidence: waiting on lock:
<dir>` (once, while a lock is busy), and the Kernel telemetry's JSON lines at `AW_LOG_LEVEL`. No message carries a
sign-in credential: a parity failure's text is scrubbed of the email and password before it leaves the use case.

## Inputs

`run` reads a UI script and `parity` reads a design manifest, both written by the planner (or by hand). Each is one
JSON object read through the package's `AwJson` options into the Core records (`UiScript`, `DesignManifest`); a key
marked required must be present and not null, an optional key may be left out (or `null` where the type says so), and
names are camelCase. A document that doesn't read, or breaks a rule below, is refused before any lock is taken:
AW-EVIDENCE-002 for a script, AW-EVIDENCE-003 for a manifest, exit 1. The message names the file, the JSON location
and the problem (`script <path>: $.steps[0]: missing required property "expectedState"`, `manifest <path>:
$.frames[0].zoom: unknown property`, `$.role: "intern" is not a valid value`, `$: not valid JSON (line 1, column 2)`);
a rule broken after reading is named by its place instead (`step 2: …`, `frame "empty-state": …`), and every broken
rule is listed, joined by `; `.

### Run script (`run <script.json>`)

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `route` | string | yes | the route under test; recorded in `summary.json` (the run navigates only through `goto` steps) |
| `role` | lower-case kebab-case string (the app's own role name, e.g. `admin`) | yes | the app role; recorded in `summary.json`, not interpreted |
| `viewports` | (`desktop` \| `phone`)[] | yes | at least one; every step runs in each (desktop 1280x800, phone 375x812) |
| `steps` | step[] | yes | at least one, run in order (below) |
| `planning` | object \| null | no | the planner's own model call, recorded as the first `invocations[]` entry: `pr` (string \| null, also `summary.json`'s `pr`), `model` (string \| null), `elapsedMs`, `inputTokens`, `outputTokens` (integer \| null each, not negative; null is unknown, never zero) |

A step:

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `action` | `goto` \| `click` \| `fill` \| `expect-visible` | yes | what the step does |
| `target` | string | yes | `goto`: a path on the host (one leading `/`; the host guard below); `click`, `fill`: a `data-testid`; `expect-visible`: visible text |
| `expectedState` | object | yes | the state that must hold after the action (below) |
| `value` | string \| null | no | the text a `fill` types; null or absent types nothing |

`expectedState` is one of five kinds, chosen by its `kind` key (anywhere in the object):

| `kind` | Other keys (all required) | Holds when |
|---|---|---|
| `text-visible` | `text` (string, not empty) | some visible element shows the text |
| `text-absent` | `text` (string, not empty) | no visible element shows the text |
| `testid-visible` | `testId` (string, not blank) | the element with that `data-testid` is visible |
| `url-path` | `path` (string, not empty) | the page's URL path is `path` |
| `input-value` | `testId` (string, not blank), `value` (string, may be empty) | that input holds exactly `value` |

A missing `kind` is `missing required property "kind"`; another value names the five kinds. Enum values are matched by
name only (a number is refused). Unknown keys are refused in every script object (the script, `planning`, each step
and each `expectedState`, whatever its kind and wherever `kind` sits): a misspelt key is one error naming the file, the
JSON location and the key (`script <path>: $.steps[0].vaule: unknown property`), never a silently ignored setting such
as a fill that types nothing. The script's SHA-256 is `summary.json`'s `scriptSha256`.

### Design manifest (`parity <manifest.json>`)

Unknown keys are refused in every manifest object (the manifest, `ready`, `viewport`, `neutralClick`, `login`, each
frame and its `designRegion`, `anchor`, `anchorOffset`, `waitFor`, `formValues[]` and `expectBoxes[]` with their
`target`, `relativeTo` and `expected`): a misspelt key is an error, not a silently ignored setting. A frame's `steps[]`
use the run script's step format above and are just as strict (`$.frames[0].steps[0].vaule: unknown property`).

| Key | Type | Required | Default | Holds and rules |
|---|---|---|---|---|
| `route` | string | yes | — | the route opened first; starts with `/` |
| `ready` | selector | yes | — | visible once the route has loaded (past any spinner) |
| `frames` | frame[] | yes | — | at least one; names unique (`duplicate frame name "<name>"`) |
| `viewport` | `{w, h}` (integers) | no | 1512x982 | the browser viewport; both positive |
| `timeoutMs` | integer | no | 60000 | the wait for `ready`, a frame's `waitFor` and its anchor, in ms; positive |
| `settleMs` | integer | no | 600 | the settle time before and after capture preparation, in ms; not negative |
| `threshold` | number | no | 0.1 | the per-pixel colour tolerance of the pixel diff; 0 to 1 |
| `includeAA` | boolean | no | false | count anti-aliased pixels as differences |
| `neutralClick` | `{x, y}` (numbers) | no | 22 px in from the viewport's bottom-right corner | where to click to drop focus before capture |
| `skipNeutralClick` | boolean | no | false | skip that click |
| `designNode` | string \| null | no | null | the design file or node every frame comes from; copied to `parity.json` |
| `login` | object \| null | no | null (no sign-in) | how to sign in first (below) |

A selector (`ready`, `anchor`, `waitFor`, a box's `target` and `relativeTo`) is `{testId}` or `{text}`, exactly one:
`testId` a `data-testid` (not blank), `text` visible text (not empty; the first visible match).

`login`:

| Key | Type | Required | Default | Holds and rules |
|---|---|---|---|---|
| `emailEnv`, `passwordEnv` | string | yes | — | the environment variables holding the credentials; UPPER_SNAKE (`^[A-Z][A-Z0-9_]*$`). The values come only from the environment, never the manifest |
| `emailSelector`, `passwordSelector`, `submitSelector` | string (CSS) | yes | — | the form's fields and submit control; not empty |
| `nextSelector` | string (CSS) \| null | no | null | clicked after the email on a two-step form |
| `loginPath` | string | no | `/login` | sign-in is done once the URL path no longer contains this |

A frame:

| Key | Type | Required | Default | Holds and rules |
|---|---|---|---|---|
| `name` | string | yes | — | letters, digits, `_` and `-`, starting with a letter or digit; names the frame's output files |
| `designPng` | string | yes | — | the design export (PNG at 1x); not empty. A relative path resolves against the manifest's directory, an absolute one is used as is |
| `designRegion` | `{x, y, w, h}` (integers) | yes | — | the compared region of the export; `x`, `y` ≥ 0, `w`, `h` > 0 |
| `anchor` | selector | yes | — | the element whose bounding-box origin locates the implementation crop |
| `anchorOffset` | `{x, y}` (numbers) \| null | no | no offset | added to the anchor's origin |
| `waitFor` | selector \| null | no | null | visible before the frame's steps run |
| `steps` | step[] | no | [] | steps that bring the page into this state (run script steps, same rules; the write guard applies on every host) |
| `formValues` | object[] | no | [] | values typed into the form (never submitted): `value` (string, required) and exactly one of `testId` (not blank) and `css` (not empty) |
| `expectBoxes` | object[] | no | [] | geometry checks: `target` (selector, required), `expected` (required: `{w, h, x, y}`, numbers, at least one set), `relativeTo` (selector: measure `x`, `y` from its top-left instead of the page's), `tolerance` (number ≥ 0 px per axis; absent is 0) |
| `knownDifferences` | string[] | no | [] | intentional differences that explain a non-zero diff; no empty entry |
| `designNode` | string \| null | no | null | the design node (id or URL) this frame was exported from |

### API script (`api-run <script.json>`)

Schema `evidence.api.run/1`, read like the UI script (camelCase, unknown keys refused at every level, enum-like values
matched by name), under AW-EVIDENCE-017 (exit 1) and checked before any lock. The message names the file, the JSON
location and the problem (`script <path>: $.requests[0].expect.pointers[0].pointr: unknown property`). The script's
SHA-256 is the summary's `scriptSha256`. Writing the script is the plan Step's job; `api-run` only reads and checks it.

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `schema` | string | no | `evidence.api.run/1` when given |
| `requests` | request[] | no | HTTP requests, run first, in order |
| `cli` | call[] | no | CLI calls, run after the requests, in order. `requests` and `cli` together hold at least one entry |
| `declaredWrites` | string[] | no | words (`add`, `delete`) the script declares as its writes; a CLI argument that reads as a write verb is refused unless it is declared here. Shown in the summary |
| `passEnv` | string[] | no | UPPER_SNAKE names of environment variables a CLI call may inherit, on top of the targets file's `cli.passEnv`. A name that reads as a secret is refused |

A request:

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `method` | `GET` \| `HEAD` \| `POST` \| `PUT` \| `PATCH` \| `DELETE` \| `OPTIONS` | yes | upper case |
| `path` | string | yes | a path on the host: one leading `/`, not `//`, no backslash, no space, no `?` or `#`; it can never name another host |
| `headers` | `{ <name>: { "env": "<VAR>" } }` | no | a header's value comes only from the named environment variable (UPPER_SNAKE). A literal string is refused (AW-EVIDENCE-017) so no secret sits in a script. A variable that is unset is AW-EVIDENCE-006, naming the variable, never a value |
| `body` | any JSON value | no | sent as `application/json`; not on `GET` or `HEAD` |
| `expect` | expectation | yes | below; `status` is required |

A CLI call:

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `argv` | string[] | yes | the program, then its arguments; no shell. `argv[0]` is a path under a build output (below), never a bare name |
| `stdin` | string \| null | no | text written to its standard input |
| `expect` | expectation | yes | `exitCode` is required |

An expectation (`status` on a request, `exitCode` on a CLI call; the other is refused):

| Key | Type | Holds and rules |
|---|---|---|
| `status` | integer 100 to 599 | the final response's status |
| `exitCode` | integer 0 to 255 | the process's exit code |
| `pointers` | assertion[] | JSON Pointer (RFC 6901) assertions over the response body, or over the CLI's stdout, read as JSON |
| `golden` | string | a file the body or stdout must equal (as canonical JSON when both are JSON, else as text); a relative path resolves against the script's directory. A missing file is AW-EVIDENCE-010 before any lock |

An assertion is `{ "pointer": "/items/0/title", ... }` with exactly one of `equals` (any JSON value), `exists` (`true`
or `false`) and `matches` (a .NET regular expression, matched against a string value, or the JSON text of another
scalar, within 100 ms). A pointer is `""` or starts with `/`; `~0` is `~` and `~1` is `/`; any other `~` escape is
refused. An array index is digits without a leading zero; `-` never resolves. A document that does not parse as JSON
fails its pointer assertions (`actual: "not JSON"`).

### DB script (`db-run <script.json>`)

Schema `evidence.db.run/1`, read exactly like the API script (camelCase, unknown keys refused at every level), under
AW-EVIDENCE-018 (exit 1) and checked before any lock. The script's SHA-256 is the summary's `scriptSha256`. Writing the
script is the plan Step's job; `db-run` only reads and checks it.

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `schema` | string | no | `evidence.db.run/1` when given |
| `connection` | string | one of the two | the name of an entry in the targets config's `databases` (Postgres). Not together with `sqlite` |
| `sqlite` | string | one of the two | a SQLite file: a path below `--repo`, or an absolute path that, after every symlink, is inside the repo or the run directory (else AW-EVIDENCE-020). Not together with `connection` |
| `seed` | string | no | a `.sql` file (relative to the script's directory) run against the run's copy of a SQLite file before the queries (below); it may not `ATTACH`, `DETACH`, `VACUUM` or `load_extension` (AW-EVIDENCE-020). Not with `connection`: Postgres is never seeded |
| `rowLimit` | integer 1 to 1000 | no | rows kept per query; default 200. More rows than that end the query's rows there and set `truncated` |
| `queries` | query[] | yes | at least one, run in order |

A query:

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `sql` | string | yes | exactly one statement (a trailing `;` is fine; a second statement is AW-EVIDENCE-018). Use `$1`-free literal SQL: there are no parameters |
| `expect` | object | yes | at least one of the three below |

`expect`: `rowCount` (integer, at least 0: the number of rows returned, after the cap), `pointers` (JSON Pointer
assertions, the API script's `equals`, `exists` and `matches`, over the rows read as a JSON array of objects keyed by
column name: `/0/text` is the first row's `text`), `golden` (a file the rows' JSON must equal, canonically; resolved like the API script's).

## Desktop evidence (`desktop-run`)

Desktop apps (Avalonia and the like) have no URL to drive. `desktop-run` starts the **real app** on **seeded data**, drives each screen into view
through the app's own UI with the UI driver (`aw-ui-driver`, docs/csharp/ui-driver.md: the same elements and commands a person uses), takes the
window's picture and compares it with the owner's mockup, at the same window size, pixel for pixel. It knows no app: the repo names its own commands, and
the app opts into the driver with one call (`UseAwUiDriver`), which does nothing unless the run sets `AW_UI_DRIVER=1`.

**Config**: the `desktop` part of `<repo>/.agentic-workflow/evidence.json` (unknown keys are AW-EVIDENCE-022):

```json
{ "desktop": { "manifest": "docs/ui-evidence/screens.json", "homeEnv": "APP_HOME",
    "fixture": { "command": ["scripts/fixture.sh"], "up": [["up"]], "down": ["down"] },
    "app": { "command": ["scripts/start-app.sh"] },
    "driver": { "command": ["scripts/aw-ui-driver.sh"] },
    "sizes": ["1440x900", "900x900"], "threshold": 10, "timeoutSeconds": 300, "env": { "APP_FLAG": "1" } } }
```

`manifest` is relative to the repo. `homeEnv` carries a new scratch directory (below the system temp directory, removed at the end) to every command: it is
where the fixture keeps its state and where the app finds it. `fixture.command` plus each `up` step's arguments run in the repo, in order; every screen's own
`fixture` steps follow, once each (first use first); `down` runs last even after a failure and a cancel. `app.command` starts the app (it runs until
`desktop-run` stops it, with `AW_UI_DRIVER=1` and `AW_UI_DRIVER_DIR` set to a fresh folder per launch). `driver.command` is the driver's command line (default
`aw-ui-driver` on the PATH); `desktop-run` adds `--dir <that folder>` and the verb. The display is the user's `DISPLAY` when set (always on macOS and
Windows), else `xvfb-run -a` (1920x1200x24); neither is an error that says so, and the choice is recorded as a `display: ...` line in the summary's `notes`.
`threshold` is the default share of differing pixels, in percent (default 10); `timeoutSeconds` bounds each fixture and driver command. Commands are
argument lists, never a shell; a program with a directory in its name is the repo's. `env` is literal, never a secret.

**Screens manifest** (`schema: "ui-evidence.desktop-screens/1"`, unknown keys refused, AW-EVIDENCE-026):

| Key | Meaning |
|---|---|
| `designRoot` | The mockups' folder, relative to the manifest's folder |
| `threshold` | Default limit for the manifest (0 to 100) |
| `screens[].name` | Lowercase letters, digits, `-`; unique; names the screen's images |
| `screens[].steps` | The screen's script (below) |
| `screens[].design` | The mockup's `.html` below `designRoot` |
| `screens[].fixture` | Extra fixture steps this screen needs (each an argument list) |
| `screens[].threshold`, `sizes`, `settleMs` | Per-screen limit, window sizes (`WxH`) and the design page's settle time (default 1000 ms) |
| `screens[].masks` | `{x, y, w, h, why, size?}` regions painted over in both frames (live data, clocks, ids) |
| `screens[].accepted` | Owner-accepted differences in words, carried into the summary for the review step to skip |

**A screen's script** has the shape of a ui-evidence step: `{ "action", "target", "expectedState", "value" }`, with the desktop actions `launch`, `click`,
`type`, `key`, `wait-for` and `screenshot`. Every screen is driven on its **own launch** of the app, at the window size of the run (the `window` verb), so a script
starts from the app as it opens. `launch` may be the first step and is implied when absent. `target` is the driver's selector (`automationId`, `name`, `type`, `text`,
`contains`, `index`; every field given must match); `expectedState` is `{ "state", "text?" }` (`exists`, `visible`, `hidden`, `gone`, `enabled`, `disabled`,
`textEquals`, `textContains`), checked after the action with the driver's `wait-for` (a `wait-for` step is that wait), and `timeoutMs` bounds it (the driver's 10 s
otherwise). `type` and `key` carry the text or chord in `value` (`replace: true` replaces a text box's text); the script ends with exactly one `screenshot`, the frame
that is compared (of the whole window, or of the element a `target` names). A value, text or chord may not start with `--` (the driver's command line refuses it).
A step the driver reports as a failed check (no element matches, the element cannot be clicked, a wait timed out: driver exit 2) fails that screen with
`step N (action) failed: <the driver's line>` and the other screens still run; a driver that does not answer, a window that will not resize or an app that
will not start is an errored run (exit 4), with how the app ended.

**How a screen is judged**: the mockup is served read-only on a loopback port and rendered by the Evidence browser at the same size; both
frames are masked, cropped to what they share and compared (the `parity` pixel match, tolerance 0.1). A screen passes when its script reached the screenshot, the frames are the same size and
the differing share is within its limit. This is the pixel half of the verdict; the structural half (an agent lists layout, element, band, colour and
label mismatches from the side-by-side image) is the `step.evidence.desktop-review` block of the `workflow.evidence-desktop` recipe.

**Run files** in `<run-dir>`: `summary.json` (`schema: "ui-evidence.desktop/1"`, `kind: "desktop"`, `runId`, `startedAt`, `finishedAt`, `appBuild`,
`manifestSha256`, `outcome` passed|failed|refused|errored, `headline`, `screens[]` with `name`, `size`, `diffPercent`, `threshold`, `passed`, `problem`,
`files` {`app`, `design`, `diff`, `sideBySide`}, `accepted`, `masked`, `notes` (how the app got its display), and `refusal`/`error` as `{code, message}`), `report.md`, and
`<screen>-<WxH>/app.png`, `design.png`, `diff.png`, `side-by-side.png`. Ports: `IDesktopCommands` (process adapter: the fixture and the driver's command line),
`IDesktopApps` (starts the app on a display and keeps it until disposed; its fake and a contract), `IScratchDirectories` (temp directories), `IStaticSite`
(loopback HTTP listener), beside the existing `IBrowser`. Evidence has no project reference to the driver: it runs the `aw-ui-driver` command (a `cli` edge in deps.yaml).

**Sindri's own setup**: `sindri-ui` calls `UseAwUiDriver(args)` (Aw.Sindri.Ui.Desktop `Composition.Compose`), so it is drivable when started with `AW_UI_DRIVER=1` or
`--ui-driver`; a normal launch opens no port. `scripts/sindri-ui-app.sh` is the app command: it runs the `sindri-ui` named by `AW_SINDRI_UI`, else publishes it
(`scripts/sindri-app.sh --publish-only`) and runs the newest build, against the fixture home in `AW_SINDRI_HOME`. `scripts/aw-ui-driver.sh` is the driver command: the
`aw-ui-driver` named by `AW_UI_DRIVER_BIN`, else the CLI published into `$AW_STATE_DIR/bin/aw-ui-driver` when its sources changed. The page screens click the top bar's tab
(`ListBoxItem` named for the page); the screens behind a row click the row by its name, so a screen whose element is not found fails with the driver's `AW-UIDRIVER-002`
line, which names the step to fix in `docs/sindri/ui-evidence/screens.json` (all eleven mockups of the screens design).

## Run files

Scorer and bugfix-state read these files after their own ports; the fields they read are marked †.

### `summary.json` (`schema: "ui-evidence.run/1"`)

| Field | Type | Holds |
|---|---|---|
| `schema` | string | `ui-evidence.run/1` |
| `runId` | string | the run directory's name |
| `runInstance` | string | this invocation: a new UUID every time, so a rerun in the same directory has another |
| `startedAt`, `finishedAt` † | string (UTC `O`) | when the run started and ended |
| `route` | string | the script's route |
| `role` | string | the script's role |
| `host` | string | the origin driven |
| `appBuild` † | string \| null | `--app-build` (the commit under test, when verified) |
| `fixtures` | string \| null | `--fixtures` |
| `scriptSha256` † | string | SHA-256 (hex) of the script file's bytes as read: proves which script a run executed |
| `pr` | string \| null | the script's `planning.pr` |
| `viewports` | (`desktop` \| `phone`)[] | the script's viewports (1280x800, 375x812) |
| `steps[]` † | object[] | viewport by viewport, in order: `viewport`, `index` (1-based), `action` (`goto` \| `click` \| `fill` \| `expect-visible`), `target`, `status` † (`passed` \| `failed` \| `broken`), `screenshot` (`<run-dir>/<viewport>-<index>-<action>.png`) |
| `visual` | object | `verdict` † (`unchecked` \| `unchanged` \| `looks-right` \| `looks-off` \| `sloppy`), `reasons` (string[]), `diffScore` (number \| null: the fraction of pixels differing from the baseline, 1 for a size change), `diffImage` (string \| null: `<run-dir>/visual-diff.png`) |
| `lintFindings[]` | object[] | `viewport`, `rule` (`overlap` \| `misaligned-edge` \| `phone-overflow` \| `truncated-label` \| `clipped` \| `missing-hover-focus` \| `unresolved-placeholder` \| `non-token-color`), `selector`, `detail` |
| `invocations[]` † | object[] | every model call actually made, in order (none when no judge is wired: "Judge" below): `phase` (`planning` \| `selector-repair` \| `visual-critique`), `model` (string \| null), `elapsedMs` (number \| null), `ok` (false: the judge was called and failed or answered nothing), `inputTokens`, `outputTokens` (number \| null: null is unknown, never zero), `cacheHit` (a verdict-cache hit stands in for a critique) |
| `planning` | object \| null | the script's planning block: `pr`, `model`, `elapsedMs`, `inputTokens`, `outputTokens` |
| `evidence` | object | `traces` (string[]: `<run-dir>/<viewport>-trace.zip`), `videos` (string[]) |

A step's `status`: `passed` (the action ran and the expected state held), `failed` (the action ran, the expected state
didn't hold: a behaviour failure, never repaired; also a repaired action that ran but missed the expectation, which is
never repaired again), `broken` (the action itself failed and no repair made it run). Every
step runs whatever an earlier one did. The visual verdict comes from the last passed step's screenshot: no pixel
differing from `--baseline` beyond the diff tolerance (threshold 0.1, anti-aliased pixels ignored) is `unchanged` with no model (never for a run with a failed step); otherwise the verdict cache
(`--cache`, default `<run-dir>/verdict-cache.json`; keyed on the pixels' and baseline's hashes, the prompt version,
`AW_VISUAL_MODEL`, the app build, the script's scenario hash, fixtures, viewports and the browser build; an entry without
an app build is trusted only inside the run invocation that wrote it, its `runInstance`, so a rerun in the same directory
never reuses it), then one critique of the changed region. No passed step, no
judge wired, or no answer from the judge, is `unchecked`.

### `summary.json` for `api-run` (`schema: "evidence.api.summary/1"`)

The UI run's shape for the fields the readers share, so one reader and one judge input serve both. Every key is always
written (`null` or `[]`, never missing), in this order. The run directory is made and an earlier `summary.json` in it
deleted first, before the script is read, so no exit leaves a stale one. A summary is then written on every exit except these, which
write none: 75 (a lock stayed busy), a script that cannot be read, a run directory that cannot be made, an unexpected exception
(AW-EVIDENCE-015, exit 4) and 130 (interrupted). Any other failure is in the summary: an error of the input (a script, host, header,
CLI target or a golden file that is missing) is `refused` (exit 1); a failure of the environment's I/O (an unreadable golden file, a
lock directory that cannot be made) is `errored` with `error` set (exit 4). Nothing is written outside the absolute `<run-dir>`
and the box-wide locks, except that each CLI call runs in a fresh temp directory under the system temp dir, removed when the call ends.

| Field | Type | Holds |
|---|---|---|
| `schema`, `kind` | string | `evidence.api.summary/1`; `api` |
| `runId`, `runInstance`, `startedAt`, `finishedAt`, `appBuild`, `scriptSha256` | | as in `ui-evidence.run/1` |
| `at` | `base` \| `head` | the side `--at` named; the caller checks out and starts the app (Sindri's recipes do); `api-run` only records it |
| `host` | string \| null | the origin the requests went to; null when the script has no requests |
| `outcome` | `passed` \| `failed` \| `refused` \| `errored` | exit 0 · 2 · 1 · 4. A test pins that exit and `outcome` agree: the exit code alone decides |
| `headline` | string | one line, 200 characters at most, scrubbed: `passed: 7 assertions`, `GET /api/items: status 500, expected 201 (assertion 3 of 7)` |
| `steps[]` | object[] | one per request, then per CLI call: `index` (1-based), `action` (`request` \| `cli`), `target` (`GET /path`, or the argv without the program's arguments' values: `out/tool` and its words), `status` (`passed` \| `failed` \| `broken` \| `skipped`), `code` (the HTTP status or exit code, or null), `output` (the body or stdout, scrubbed and capped at 2,000 characters, or null), `headerNames` (names only) |
| `failures[]` | object[] | one per assertion that did not hold: `step`, `pointer` (string \| null), `expected`, `actual` (text, scrubbed, capped at 200 characters; `missing` for an absent pointer) |
| `declaredWrites[]` | string[] | the script's declaration, so a report shows it |
| `passedEnv[]` | string[] | names only, of the variables beyond `PATH`, `HOME`, `TMPDIR` and `LANG` a CLI call received |
| `provenance`, `seedSha256` | `unseeded`; null | the run seeds nothing |
| `refusal` | `{code, message}` \| null | set on exit 1: the AW-EVIDENCE code and the message of the stderr line |
| `error` | `{code, message}` \| null | set on exit 4 |
| `invocations[]` | | always `[]`: no model runs |

A step's `broken` is a request that failed or a program that would not start (exit 4); `skipped` is a step after it.
A run that is refused, or errored before it starts (an unreadable golden file, a lock that failed), ran nothing: its `steps` is `[]`,
and `refusal` or `error` names what stopped it. An assertion that does not hold marks its step `failed`, and the
remaining steps still run (a request or call is an action that already happened or never will). Recorded text — each step's
output, each failure's `pointer`, `expected` and `actual`, and the `headline` (built from them, or from the refusal or error message) — goes through the run's scrubber and
`Aw.Redaction` as it is recorded: the run's own values are replaced by value (each resolved header value with
`[REDACTED:header-value]`, and for a `<scheme> <credential>` value such as `Bearer <token>` the credential alone too; each
pass-through variable value with `[REDACTED:env-value]`; wherever an app echoes them, including their JSON-escaped forms; a value
under 6 characters is left to the patterns) together with the pattern scrub, after the value of every secret-named JSON member is
hidden (first, on the text as written, so a learned numeric value replaced by an unquoted placeholder cannot make the JSON invalid and switch member hiding off), and the text is capped. A failure whose `pointer` runs through a secret-named member has its `expected` and `actual` replaced whole by `[REDACTED:secret-field]`. The refusal and error messages are the tool's own text, not
a response body: they get the value and pattern scrub, not member hiding or the cap. Every other field is written as it is: it comes
from the command line, the repo, the script or the tool itself (`schema`, `kind`, `at`, `host`, `appBuild`, `runId`, `runInstance`, timestamps,
`scriptSha256`, `seedSha256`, `declaredWrites`, `passedEnv` names, `provenance`, step `index`, `action`, `target`, `headerNames`,
`failures[].step`, a refusal's or error's `code`, the always-empty `invocations`), or it is a number or enum value that can't carry text (a step's HTTP status or exit
`code`, its `status`, the run's `outcome`). stdout is exactly the file's content (no trailing newline) and logs go to stderr. The JSON Schemas of the script and the summary are generated from the records and committed as
`schemas/evidence/api-script.schema.json` and `schemas/evidence/api-summary.schema.json`; a test validates a produced summary against its schema.

The secret-name rule (`Aw.Redaction`) matches whole tokens: a name is split at `_`, `-`, `.`, camelCase humps and where letters turn into digits (`password2` is `password`, `2`; `oauth2`, `sha256`, `base64`, `utf8` and `x509` stay ordinary), and a token that equals a secret word (`password`, `passwd`, `pwd`, `passphrase`, `credential`, `secret`, `token`, `apikey`, each also plural) or neighbours that spell one (`api`, `access`, `private`, `secret`, `encryption`, `signing`, `hmac`, `master` or `client`, then `key`) make it secret. `token(s)` is a count, not a secret, only when a count word is directly beside it (`max_token`, `max_tokens`, `tokens_used`, `input_tokens`, `totalTokens`); `used_refresh_tokens` is secret. Only `token` has a count reading: `password_min_length` and `password_changed_at` stay secret. A column the result repeats keeps hiding under its numbered key (`cookie_2`) because the original name is secret.

### `summary.json` for `db-run` (`schema: "evidence.db.summary/1"`)

The same engine contract as `api-run`: every key always written (`null` or `[]`, never missing), in this order; the run
directory made and an earlier `summary.json` deleted first; a summary on every exit except 75, an unreadable script, a
run directory that cannot be made, an unexpected exception and 130; stdout exactly the file's content; logs on stderr;
nothing written outside the absolute `<run-dir>` and the box-wide locks; the exit code alone decides the outcome (a test
pins that they agree). Recorded text — each step's `output` (the rows as JSON), each step's `target` (the SQL), each failure's
`pointer`, `expected` and `actual`, and the `headline` (built from them, or from the refusal or error message) — goes through the run's
scrubber and `Aw.Redaction` as it is recorded, in this order: (1) the value of every secret-named JSON member is hidden first, on the text as written (so a learned numeric value, replaced by an unquoted placeholder, cannot make the JSON invalid and switch member hiding off); (2) the run's own values are replaced by value, together with the pattern scrub:
the role's password, resolved from the entry's `passwordEnv`, with `[REDACTED:env-value]` (including its JSON-escaped forms), and every value
read from a secret-named column (below) with `[REDACTED:secret-field]`; a value under 6 characters is left to the patterns (a header-value
placeholder, `[REDACTED:header-value]`, exists in the shared scrubber but a db-run has no headers); step (1) is the hiding of the value of every secret-named
JSON member, wherever it occurs and whatever the document repeats (a streaming pass over the text, not a parsed tree, so a body or stdout that repeats a key still has every secret-named member hidden; text that isn't JSON keeps only the value and pattern scrub) (a result column called `password`, `api_key`, `accessToken`, …) is hidden with `[REDACTED:secret-field]`; (3) the text is capped. A failure whose `pointer` runs through a secret-named column has its `expected` and `actual`
replaced whole by `[REDACTED:secret-field]`. The refusal and error messages are the tool's own text, not rows: they get the value and
pattern scrub, not member hiding or the cap.

Which values count as secret column values. A value is hidden by value, in every text recorded from that query on, when it was read from a
column that is secret-named by its output name, or by the table column it reads when the engine can say: Postgres reports each result
column's table and attribute number (RowDescription), which the session looks up in `pg_attribute`; SQLite reports the origin column of a
direct column reference. So `SELECT api_key AS copy_of_key` is hidden in both engines, and `'key is ' || api_key AS sentence` is hidden when
`api_key` is in the same result set (its value is learned from that column). Bytes are learned in the `\x`-hex form the rows are recorded in, and every element of an array column; dates and times in the ISO form they are recorded in (`yyyy-MM-dd`, `HH:mm:ss[.fffffff]`, invariant culture; a DateTime or DateTimeOffset in the round-trip `O` form). A secret-named column is one whose name holds a secret word as a whole token (the shared rule below), so `password_hash` and `api_tokens` count. A result that repeats a column name (`SELECT a.id, b.id`, Postgres's `?column?`) is recorded with unique keys: the first keeps its name, later ones become `id_2`, `id_3` (skipping any name the result really has), so a pointer such as `/0/id_2` reaches every column. **The limit:** a value that comes out of a computed
expression, under a name that is not secret, from a column the query does not also select, can't be traced to its source (`SELECT 'key is ' || api_key
AS sentence FROM accounts` alone). Such a value is caught only if it matches a secret pattern (a token shape, an `Authorization`
value, …) or is the role's password. The scrub does not claim to hide a secret wherever it sits in the database, only the values this run
read from secret-named columns. Every other field is written as it is: it comes from the command line, the repo, the script or the tool itself
(`schema`, `kind`, `at`, `appBuild`, `runId`, `runInstance`, timestamps, `scriptSha256`, `seedSha256`, `engine`, the top-level `target` (the connection name or the
SQLite path as the script gave it), `provenance`, step `index`, `action`, `failures[].step`, a refusal's or error's `code`, the always-empty
`invocations`), or it is a number or enum value that can't carry text (a step's `status`, `rowCount` and `truncated`, the run's `outcome`).
A password that happens to equal the connection name leaves that field intact.

| Field | Type | Holds |
|---|---|---|
| `schema`, `kind` | string | `evidence.db.summary/1`; `db` |
| `runId`, `runInstance`, `startedAt`, `finishedAt`, `appBuild`, `scriptSha256`, `at` | | as in `evidence.api.summary/1` |
| `engine` | `sqlite` \| `postgres` \| null | the engine of the database the script names; null when the script could not be read |
| `target` | string \| null | the connection name, or the SQLite path as the script gave it |
| `outcome`, `headline` | | as in `evidence.api.summary/1`: `passed`, `failed` (exit 2), `refused` (1), `errored` (4); `headline` is one line, 200 characters at most (`passed: 3 assertions`, `query 2: rowCount 1, expected 2 (assertion 4 of 5)`) |
| `steps[]` | object[] | one per query: `index` (1-based), `action` (`query`), `target` (the SQL, one line, scrubbed, 200 characters at most), `status` (`passed` \| `failed` \| `refused` \| `broken` \| `skipped`), `rowCount` (rows kept, or null), `truncated` (the cap cut the rows), `output` (the rows as JSON, scrubbed and capped at 2,000 characters, or null) |
| `failures[]` | object[] | as in `evidence.api.summary/1`: `step`, `pointer`, `expected`, `actual`; a `rowCount` or golden failure has a null `pointer` |
| `provenance`, `seedSha256` | `unseeded` \| `seeded`; string \| null | `seeded` with the seed file's SHA-256 when a seed ran against the copy; else `unseeded` and null |
| `refusal` | `{code, message}` \| null | set on exit 1 (including a write the server refused, with its SQLSTATE in the message) |
| `error` | `{code, message}` \| null | set on exit 4 |
| `invocations[]` | | always `[]` |

A step is `refused` when the database or the connection refused it (the run is then exit 1 and later steps are
`skipped`), `broken` when it failed for another reason, such as a table that does not exist or a connection lost
(exit 4). The schemas of the script and the summary are generated from the records and committed as
`schemas/evidence/db-script.schema.json` and `schemas/evidence/db-summary.schema.json`; a test validates a produced summary against its schema.

### `parity.json` (`schema: "ui-evidence.parity/1"`)

| Field | Type | Holds |
|---|---|---|
| `schema` | string | `ui-evidence.parity/1` |
| `runId` | string | the run directory's name |
| `host` | string | the origin driven |
| `provenance` | `seeded` \| `scrubbed` \| `unknown` | a preview host is `scrubbed` (or `unknown` when `--db-provenance unknown`); a local host is `seeded` only with `--db-provenance seeded` |
| `appBuild`, `designNode` | string \| null | `--app-build`; the manifest's design node |
| `viewport` | `{w, h}` | the manifest's viewport |
| `frames[]` | object[] | one per manifest frame, in order (below) |

Each frame: `name`, `designRegion` `{x, y, w, h}` (in the design export), `implementationOrigin` `{x, y}` (where the
same-size region starts on the page: the anchor plus `anchorOffset`), `anchorBox` `{x, y, w, h}`, `anchorOffset` `{x, y}`,
`sizeDelta` `{w, h}` (anchor size minus design region size), `diffPixels`, `total`, `diffPercent` (two decimals),
`threshold`, `includeAA`, `boxChecks[]` (`target`, `relativeTo`, `expected` `{w, h, x, y}`, `actual` (or null when an
element is missing), `tolerance`, `pass`, `failure`), `knownDifferences[]`, `files` (`design`, `implementation`, `diff`,
`sideBySide`: `<run-dir>/<frame>-{design,implementation,diff,side-by-side}.png`), `designNode`.

A relative `designPng` in the manifest resolves against the manifest's directory. `parity.json` is deleted before the
run, so a failed run never leaves an earlier summary for `parity-plan` to publish. `run` does the same with
`summary.json`: once the locks are held, an earlier `summary.json` in the run directory is deleted, so exit 4 leaves none.

### `publish-plan` output

`provenance` (`seeded` \| `unknown`: the claim, downgraded to `unknown` for any host that isn't local), `question` (the
ask-first question; it states what will happen: a Linear upload only when seeded with an issue given, otherwise why
there is none), `linearUploadAllowed` (seeded and an issue given), `files` (the step screenshots), `note`, and
`prComment` (the markdown body).

## Data evidence guards

`api-run` is held to these, in this order, before it takes a lock; each refusal is exit 1 with its code, and the
summary says `refused`. Honest mistakes are what they stop (a request sent to the wrong host, a program that deletes
something), not a determined agent.

| Guard | Rule | Code |
|---|---|---|
| Host | requests go only to `--host`, which is required and has no default (a script with `requests` and no `--host` is refused). The host guard above decides it: a bare loopback origin, or a preview host with `--allow-preview-host` | AW-EVIDENCE-004 |
| Redirects | a redirect to the same origin is followed (up to five hops) unless the expectation's `status` is itself a 3xx; a redirect to any other origin is never followed, and ends the run (exit 4) naming the origin it refused | AW-EVIDENCE-023 |
| Writes | none refused: API evidence allows any HTTP method on the host the script names (owner decision, 2026-10-15). There is no `--allow-writes` for `api-run` | |
| Header secrets | values come only from named environment variables; only the names are recorded, and the values (and a scheme value's credential alone) are scrubbed by value from every free-text field the run records | AW-EVIDENCE-017 (a literal), AW-EVIDENCE-006 (unset) |
| CLI target | `argv[0]` contains a `/`: it resolves, against `--repo` (default: the working directory) and after following every symlink, to an executable file inside a build output directory or equal to a `cli.allow` path. A bare name is never looked up on `PATH`. With nothing configured, every CLI call is refused, and the message names the files to write | AW-EVIDENCE-019 |
| CLI writes | an argument that reads as a write verb (the UI guard's list, plus `push`, `drop`, `rm`, `truncate`, `destroy`, `purge`, `wipe`, `kill`, `commit`, `put`, `patch`, `write`, `rollback`, `revert`, `uninstall`) is refused unless the script's `declaredWrites` names it | AW-EVIDENCE-019 |
| CLI environment | each call runs in a fresh temporary directory (its `HOME` and `TMPDIR`) with an empty environment plus `PATH`, `LANG` and the declared pass-through names. A pass-through name that reads as a secret (it contains `TOKEN`, `SECRET`, `PASSWORD`, `CREDENTIAL`, `API_KEY` or `PRIVATE_KEY`, starts with `AWS_`, `AZURE_`, `GCP_`, `GOOGLE_`, `SSH_` or `GITHUB_`, or is `DATABASE_URL`) is refused | AW-EVIDENCE-019 |
| Redaction | recorded text (step output, failures' pointer, expected and actual, and the headline) is scrubbed by the run's own values and `Aw.Redaction`'s patterns, has secret-named JSON members hidden, and is capped; refusal and error messages get the value and pattern scrub; every other field is written as it is (see the summary section) | |

### Database guards (`db-run`)

Read-only is enforced where the database accepts a connection, not by parsing statements; the statement check below
exists only to give a clear early error. Order: the script and the targets config are read (for every run, SQLite too: a missing `--config` is AW-EVIDENCE-010, an invalid file 022), the write statements and the seed's reaching statements are refused, the
target is resolved and its file or entry checked, then the locks are taken, then the database is opened, and only then does a
query run. Each refusal is exit 1 with its code and the summary says `refused`.

| Guard | Rule | Code |
|---|---|---|
| Pre-run write refusal | a script query that is not exactly one statement never gets here: the script reader refuses it first as AW-EVIDENCE-018 (`must be exactly one statement`). Before anything connects, each query is split into statements (quotes, comments and dollar quoting respected) and refused when it is not one statement starting `SELECT`, `WITH`, `VALUES`, `TABLE`, `SHOW`, `EXPLAIN` or a read `PRAGMA` (`table_info`, `table_xinfo`, `index_list`, `index_info`, `index_xinfo`, `foreign_key_list`, `database_list`, `table_list`, never one that assigns), or when `INSERT`, `UPDATE`, `DELETE` `MERGE` or `INTO` (a `SELECT … INTO` creates a table) appears in it outside a string, so a writing CTE is caught. `COPY`, `ATTACH`, `VACUUM`, DDL, `SET` and transaction control fall to the first rule. A function that writes (`bump()`) and `nextval` are **not** caught here on purpose: only the connection can know | AW-EVIDENCE-020 |
| SQLite source | the file's real path (every symlink followed) is inside the repo or the run directory; a file that is not there is AW-EVIDENCE-010 (exit 1, like a missing seed or golden) | AW-EVIDENCE-020, 010 |
| SQLite connection | the source is copied as a file (`File.Copy` of the database and, when present, its `-wal` and `-journal` files, never `-shm`) into `<run-dir>/db/`, and no SQLite connection ever opens the source, so a WAL-mode database gets no `-wal` or `-shm` file beside it and any page size copies; a source inside `<run-dir>/db/` is refused (both paths are compared with every symlink resolved, so a symlinked run directory or source can't get past it). The copy takes none of SQLite's locks, so it is verified: the main file, `-wal` and `-journal` are stat'ed (length and modification time) before and after and the copy is made again, up to three times, if any changed; the copy is then opened read-write once, which rolls a hot `-journal` back on the copy (the source keeps its journal), and `PRAGMA quick_check` must say `ok`. A source that keeps changing, or a copy that fails the check, is AW-EVIDENCE-024 (`changed while it was copied` / `not a consistent database`): stop the app's writes to the file, or retry. A file SQLite says is not a database at all (SQLITE_NOTADB, such as a text file) is the script's input, AW-EVIDENCE-018 (exit 1), not 024. Block comments do not nest for SQLite (they do for Postgres) in the statement check. The query connection is that copy opened `Mode=ReadOnly` with `query_only` (`Aw.Kernel.Data`'s `OpenReadOnly`). SQLite has no connection setting that stops `ATTACH` or `VACUUM INTO` from creating a file, so the adapter refuses a statement that is not a single read, or that attaches, detaches, vacuums or assigns a pragma, before SQLite sees it | AW-EVIDENCE-020 |
| Postgres entry | only an entry in the targets config, with `disposable: true` and an `identity`; the password comes from the environment variable `passwordEnv` names, never the script, and only that variable's name is recorded | AW-EVIDENCE-020 |
| Postgres identity | before any script query, `inet_server_addr()`, optionally `inet_server_port()`, and `current_database()` must equal the entry's `identity`; a mismatch sends no query and ends the run | AW-EVIDENCE-021 |
| Postgres role | the role must not be a superuser or hold `rolcreaterole`, `rolcreatedb`, `rolreplication` or `rolbypassrls`; must not be a member of `pg_write_server_files` or `pg_execute_server_program`; and must hold no `INSERT`, `UPDATE`, `DELETE` or `TRUNCATE` privilege on any table, view or the like outside `pg_catalog`, `information_schema` and the toast and temp schemas, as `has_table_privilege` answers it, so a grant to the role itself, to `PUBLIC` and one inherited through a group it is a member of (`IN ROLE writers`) all count (column-level and sequence grants are not counted). That is what makes `COPY … TO` fail at the server | AW-EVIDENCE-020 |
| Postgres connection | every connection field is set explicitly, so no `PG*` environment variable or password file applies; `Options=-c default_transaction_read_only=on` and a statement timeout; no pooling; each query runs on its own in `BEGIN READ ONLY` (followed by a `SELECT 1`, because Postgres lets the first statement of a transaction switch it back to read-write) over the extended protocol: the adapter refuses a second statement (`COMMIT; INSERT …`) before sending it (the driver would split it into separate commands, so the server never sees one). The layers that stop a write are the session default `default_transaction_read_only`, the `BEGIN READ ONLY` transaction and the role check made when the connection opens. The transaction is always rolled back. A write the server refuses (SQLSTATE 25001, 25006, or 42501 on a statement the pre-run guard would have refused, such as `COPY … TO`) is recorded as a refused step; a 42501 on a plain read is `the server refused the query`, a broken step (errored, exit 4). Server error text is recorded with its SQLSTATE and without the values it quotes (outside SQLSTATE class 42 everything from the first quote character to the last, a double quote or a localized pair such as `„…“`, becomes `"…"`, because the server does not escape a quote inside the value it quotes), so a data error such as `invalid input syntax for type uuid` can't carry a secret; a cell the driver can't read (numeric `NaN`, a BC date, an unmapped type) is read as text when possible, else recorded as `<type: not representable>` | AW-EVIDENCE-020 |
| Seeds | SQLite only, and only into the run's copy, over a separate read-write connection that is closed before the read-only one opens. The seed gets the statement guard before any lock (a statement that uses `ATTACH`, `DETACH`, `VACUUM` or `load_extension` is refused with its code, so a seed can't name another file) and the connection allows no attached database at all (`SQLITE_LIMIT_ATTACHED` 0), so no other database file is reachable. The seed file's SHA-256 is the summary's `seedSha256` and `provenance` is `seeded`. The source is never opened. The `seedSha256` is the hash of the file's own bytes | AW-EVIDENCE-018 (a seed with `connection`, or a seed statement that fails on the copy), AW-EVIDENCE-020 (a seed that reaches beyond the copy) |
| Rows | at most `rowLimit` rows per query are read; the recorded rows are scrubbed (secret column values by value, then secret-named columns hidden, in the order the summary section gives) and capped at 2,000 characters | |

A Postgres `host` must be a TCP host (a Unix socket directory is not supported by this stage).

### Targets config

What a script may reach comes from configuration beside the repo under test, not from the script; there is no built-in default (no
`bin/Release`, `dist` or `target`).

- **Per repo**: `<repo>/.agentic-workflow/evidence.json` (`--repo`, default the working directory), or the file `--config <file>`
  names instead (an explicit override; a missing file is AW-EVIDENCE-010). There is no machine-wide default file. A missing
  repo file is an empty config.
  ```json
  {
    "schema": "evidence.targets/1",
    "cli": { "buildOutputs": ["out", "build/bin"], "allow": ["/abs/path/to/tool"], "passEnv": ["LANG"] },
    "databases": { "<name>": { "engine": "postgres", "host": "127.0.0.1", "port": 55432, "database": "fixture",
                               "user": "evidence_ro", "passwordEnv": "EVIDENCE_PG_PASSWORD", "disposable": true,
                               "identity": { "serverAddress": "172.17.0.2", "serverPort": 5432, "database": "fixture" } } }
  }
  ```
  `schema` is `evidence.targets/1` or `evidence.repo/1`. `cli.buildOutputs` are relative directories that, after symlinks, stay
  inside the repo; `cli.allow` holds absolute paths; `cli.passEnv` names variables every CLI call may inherit. `databases`
  is read by `db-run` (each entry: `engine` `postgres`, `host`, `port` 1 to 65535, `database`, `user`, `passwordEnv`
  (UPPER_SNAKE), `disposable` (must be true), `identity` with `database` and `serverAddress` required and `serverPort`
  optional); `api-run` reads and validates the file whole. Unknown keys, an absolute or escaping
  build output, a relative allow path, and invalid JSON are AW-EVIDENCE-022 (the message names the file and the JSON location).

The use cases see only the Core port `IEvidenceTargets`; the file names and the state directory are the adapter's.

## Locks

`run`, `parity`, `api-run` and `db-run` hold two box-wide locks for the whole browser run, through Evidence's own Core port
`IMachineLocks` (`AcquireAsync(LockKind, timeout) → Result<ILockLease>`), in a fixed order: the QA-stack lock (the wider
one) first, then the heavy-job lock; failing to take the second releases the first. A guard refusal or a bad input is
reported before any lock is taken. The leases are released on every path: success, failure, exception and
interruption.

The adapter (`DirectoryLocks`) is the same lock the shell tools take, so the two exclude each other both ways:

| | |
|---|---|
| QA-stack lock | `$AW_QA_STACK_LOCK`, else `${AW_STATE_DIR:-~/.agentic-workflow}/locks/qa-stack.lock` |
| Heavy-job lock | `$AW_HEAVY_JOB_LOCK`, else `${AW_STATE_DIR:-~/.agentic-workflow}/locks/heavy-job.lock`: slot 1. Slot k (2 … `$AW_HEAVY_SLOTS`) is `<lock>.k` |
| Path form | A configured path is made absolute (against the working directory) and loses any trailing separators, so `/x/qa.lock//` is the lock `/x/qa.lock` |
| Heavy-job slots | `$AW_HEAVY_SLOTS`, default 2: at most that many heavy jobs at once, the same count the `heavy` wrapper uses. A value that isn't a whole number of at least 1 isn't refused: the lock uses 2 and says once on stderr `ui-evidence: AW_HEAVY_SLOTS='<value>' isn't a whole number of at least 1; using 2 slots` |
| Acquire | an atomic `mkdir` of a slot directory (libc `mkdir`; `EEXIST` means that slot is busy). Each poll tries slot 1, then 2 … N, the wrapper's order, so a holder that knows only `<lock>` (sindri's lock, a plain `acquire_lock` caller) occupies slot 1 and the total never exceeds the slot count. Polled every second until the timeout; `ui-evidence: waiting on lock: <lock>` once. The QA-stack lock has one slot |
| Owner | `<slot>/owner`: `<pid> <UTC time> ui-evidence`, the file the `heavy` wrapper writes too |
| Release | delete our `owner`, then `rmdir` the slot we hold; a directory something else wrote into, or whose `owner` line names another process (ours was removed by hand and another holder took the slot), is left alone |
| `AW_HEAVY_LOCK_HELD=1` | set by the `heavy` wrapper: the parent already holds a heavy-job slot, so that lease is a no-op (it takes no second slot) |
| Stale locks | never taken over (the shell primitive doesn't either); remove a dead holder's directory by hand |

The `heavy` wrapper is the owner's machine script (`~/.agentic-workflow/bin-r1b/heavy`, outside this repo); its slot
layout and owner line are the ones above. `config/lib/locks.sh` (`acquire_lock` / `release_lock`) is the in-repo shell
side of the same format; the integration tests
source it to prove the exclusion in both directions, and that a C# holder takes `<lock>.2` while bash holds `<lock>`.

## Judge

Selector repair and visual critique go through the Core port `IJudge` (`IsAvailable`, `RepairSelectorAsync`,
`CritiqueAsync`). `IsAvailable` says whether a judge is wired at all; callers check it first, and with no judge they
don't call it and record nothing in `invocations[]`, because no model ran. A wired judge's null answer means the call
failed (timed out, errored or answered out of contract); that call is recorded with `ok: false`. Either way every caller
fails closed: no repair (the step stays `broken`), and the verdict stays `unchecked`. A recorded call carries null
token counts when the answer reports none. The CLI adapter over the C# `judge` binary (`JudgeCliJudge`, cutover unit 1.3) runs
`judge <question>` with the input record on stdin and parses the envelope (`docs/csharp/judge.md`, 'Questions': `ui-element-repair`,
`visual-critique`); until it lands the composition registers its stub, which reports `IsAvailable` false (every answer null), so a run
records no `selector-repair` or `visual-critique` invocation and the verdict stays `unchecked`. `UnavailableJudge` stays in the tree as
the contract's reference for an unwired judge.

## Host guard

| Host | Accepted as | Write guard |
|---|---|---|
| none | `http://localhost:3000` (local) | none |
| `http://localhost:<port>`, `http://127.0.0.1:<port>` (bare origin) | local | none |
| `https://<host>` (bare origin) whose whole hostname matches `UI_EVIDENCE_PREVIEW_HOST_PATTERN`, with `--allow-preview-host` | preview | write-looking steps refused without `--allow-writes` |
| anything else (dev, staging, prod, a path, a query, credentials in the URL; every non-loopback host when no pattern is configured, or the pattern doesn't compile) | refused, AW-EVIDENCE-004 | — |

Every `goto` target must be a path on the host (one leading `/`); a step that leaves the host is refused
(AW-EVIDENCE-005), and the browser aborts any off-origin navigation it is asked to make. Playwright never routes a
redirect hop, though, so a server's `3xx` (or a canonical-host or auth redirect) can still take the page elsewhere. The
session therefore also watches where the page actually is: every main-frame navigation, and the page's URL before and
after each browser call. Once the page has been off the vetted origin, that call and every later one in the session
fail with `AW-EVIDENCE-013 <call>: the page left <vetted origin> for <other origin> …`, so `run` and `parity` end with
exit 4 and write no `summary.json` / `parity.json`. In `run` that message is the one reported; `parity` reports every
run failure as AW-EVIDENCE-015, naming the origin when the page left during sign-in or route opening, and naming the
frame and its step (`step N … broken`) when it left during a frame step. Sign-in checks again after loading the host and right before it
types each credential, so a credential is never typed into a page off the vetted origin. A write-looking step is a click (or a `goto`) whose
target has a write verb (`save`, `submit`, `delete`, …, including fused control names such as `saveBtn`). Selector
repair is held to the same guard: where writes are refused, a candidate the judge picks whose test id, accessible name
or text reads like a write is never clicked or filled (it counts as no candidate, so the step stays `broken`). `parity`
applies the write guard on every host, local included, to its manifest `route` (vetted as the first `goto`) and every
frame step. Sign-in credentials come only from the environment variables
the manifest's `login` names (AW-EVIDENCE-006 lists the missing names, never values).

## Environment

These are read by Infrastructure's `EnvironmentEvidenceSettings` and lock settings; the use cases see only the typed
`IEvidenceSettings` port, never a variable name. Blank counts as unset.

| Variable | Read by |
|---|---|
| `UI_EVIDENCE_USER_AGENT` | `run`, `parity`: the user agent to present instead of desktop Chrome at the browser's build |
| `AW_VISUAL_MODEL` | `run`: the model label in the verdict-cache key (default `judge-default`) |
| `UI_EVIDENCE_APP_TITLE`, `UI_EVIDENCE_CONFIG` | `doctor`: the title the app on :3000 must show at `/login`; else the `NAME=value` env file `UI_EVIDENCE_CONFIG` names (an `export ` prefix and quotes are allowed). Neither set (or an unreadable file) is itself a doctor failure: `the app title is not set: set …` |
| `UI_EVIDENCE_PREVIEW_HOST_PATTERN` | `run`, `parity`: the regex a preview `--host`'s whole hostname must match (e.g. `pr-[0-9]+\.preview\.example\.com`; write it so dev, staging and prod never match). Unset: every non-loopback host is refused |
| `UI_EVIDENCE_API_PORT` | `doctor`: a localhost port (an API server) that must accept connections, 1-65535. Unset: not checked |
| `UI_EVIDENCE_DB_CONTAINER`, `UI_EVIDENCE_DB_USER` | `doctor`, `provenance`: the Docker container running the local Postgres and its user (default `postgres`). No container: `doctor` skips the database and `provenance` is `unknown` |
| `UI_EVIDENCE_SEED_TABLE` | `provenance`: the `[schema.]table` whose `email` column is read (a plain identifier only, since it is spliced into the SQL; anything else counts as unset). Unset: `unknown` |
| `UI_EVIDENCE_SEED_DOMAINS`, `UI_EVIDENCE_SEED_EMAIL_PATTERN` | `provenance`: the seed policy (defaults match only `example.com`, so an unconfigured install stays `unknown`) |
| `AW_QA_STACK_LOCK`, `AW_HEAVY_JOB_LOCK`, `AW_HEAVY_SLOTS`, `AW_HEAVY_LOCK_HELD`, `AW_STATE_DIR` | the lock adapter |
| the script's `headers.*.env` and `passEnv`, the targets file's `cli.passEnv` | `api-run`: values by the name the script or file gives, never recorded |
| a database entry's `passwordEnv` | `db-run`: the Postgres password by the name the entry gives, never recorded and scrubbed by value |
| the manifest's `login.emailEnv` / `login.passwordEnv` | `parity` sign-in |

`doctor` checks `http://localhost:3000/login`'s title, `docker exec <container> pg_isready -U <user>` when a container is
configured, and a TCP connect to `localhost:<UI_EVIDENCE_API_PORT>` when a port is. `provenance` runs
`docker exec <container> psql` against the seed table with the seed pattern passed as a psql variable, never
interpolated into the SQL; a failed query (or no container or table configured) is `unknown` with the reason. A row
with a NULL email reads as an empty email (not on a seed domain), so it makes the data `unknown` rather than being
skipped. The TypeScript skill also reads these from a committed `deployment.env` next to its SKILL.md; the binary
reads the environment only until the switch.

## AOT scope

The Host is framework-dependent, not an AOT entry point: Playwright for .NET has no NativeAOT or trimming support.
`Aw.Evidence.Infrastructure` opts out of AOT analysis (`AwAotCompatible=false`) for the same reason. `Core` and
`Features` stay AOT-compatible (no reflection; JSON through the package's source-generated context), so they could
move behind an AOT host if the browser adapter ever allowed it.

## Ports and adapters

| Port (Core) | Adapter (Infrastructure) | Fake (Testing) |
|---|---|---|
| `IBrowser`, `IBrowserSession` | `PlaywrightBrowser` (headless Chromium) | `FakeBrowser`, `FakeBrowserSession`, `FakePage` |
| `IBrowserInstaller` | the Playwright installer (`install chromium`) | `FakeBrowserInstaller` |
| `IRunFiles` | `FileRunFiles` | `InMemoryRunFiles` |
| `IImageCodec` | `PngCodec` | `FakeImageCodec` |
| `IVerdictCacheStore` | `FileVerdictCacheStore` | `InMemoryVerdictCacheStore` |
| `IMachineLocks` | `DirectoryLocks` | `InMemoryMachineLocks` |
| `IJudge` | `JudgeCliJudge` (a stub until cutover unit 1.3; `UnavailableJudge` is the unwired reference) | `FakeJudge` |
| `IGhRunner` (`gh`, one call: arguments and stdin in, exit code and both streams out) | `ProcessGhRunner` (a stub until cutover unit 1.2) | `FakeGhRunner` (scripted, or an in-memory repository answering the git data API) |
| `IStackProbe`, `IProvenanceQuery` | `ProcessStackProbe`, `DockerPsqlProvenance` | `FakeStackProbe`, `FakeProvenanceQuery` |
| `ISecretsSource` (credentials by the name the manifest gives) | `EnvironmentSecrets` | `FakeSecrets` |
| `IEvidenceSettings` (user agent, visual model, preview host pattern, API port, seed policy, app title) | `EnvironmentEvidenceSettings` | `FakeEvidenceSettings` |
| `IApiTransport` (one HTTP request, never following a redirect) | `HttpApiTransport` | `FakeApiTransport` |
| `ICliRunner` (one program, a fresh temporary directory, an exact environment) | `ProcessCliRunner` | `FakeCliRunner` |
| `IExecutableLocator` (the real path of an executable file or a directory, symlinks followed) | `FileExecutableLocator` | `FakeExecutableLocator` |
| `IDatabaseReader` (`OpenAsync(DbTarget)` → a read-only `IReadSession`; `QueryAsync(sql, rowLimit)` runs one statement) | `SqliteDatabaseReader` (`Aw.Kernel.Data`), `PostgresDatabaseReader` (Npgsql; `AddEvidencePostgresInfrastructure`), behind a router that picks by target | `FakeDatabaseReader` |
| `IEvidenceTargets` (the merged targets config) | `FileEvidenceTargets` | `FakeEvidenceTargets` |
| `ITextScrubber` | `RedactionScrubber` (`Aw.Redaction`) | `FakeTextScrubber` |
| `IIdGenerator` (Kernel; the run instance) | `RandomIds` | `SequentialIds` |

Each port with behaviour to pin has a contract suite in `tests/Aw.Evidence.Testing` that the fake and the adapter both
run: `BrowserSessionContract` (fake: unit; Playwright on the fixture site: heavy), `BrowserInstallerContract` (fake: unit;
Playwright: heavy), `RunFilesContract`, `ImageCodecContract`, `VerdictCacheStoreContract`, `MachineLocksContract`,
`JudgeContract`, `GhRunnerContract`, `StackProbeContract` (the adapter over a local HTTP server, a stub `docker` and a loopback listener),
`ProvenanceQueryContract` (the adapter over a stub `docker` playing `psql -At`), `SecretsSourceContract`, `IdGeneratorContract`, `ApiTransportContract` (the adapter over a loopback server), `CliRunnerContract` (over stub programs),
`ExecutableLocatorContract`, `EvidenceTargetsContract`, `TextScrubberContract` and `ReadOnlyDatabaseContract` (a writing CTE, a side-effecting function, `nextval`, `COPY … TO`, a `PRAGMA` assignment, `VACUUM INTO`, `ATTACH`, `SET transaction_read_only = off` and `COMMIT; INSERT …` each refused with the data and the directory unchanged; the fake and the SQLite adapter run it as unit and integration, Postgres against a throwaway container as heavy). `IEvidenceSettings` has none: its fake is a set of plain values with no behaviour that could
drift; the adapter's names, defaults, blank-as-unset and env-file fallback are pinned by `EnvironmentEvidenceSettingsTests`.

## At the switch

What changes for the callers when the install moves to the C# build:

- **Run directories.** `run` and `parity` write wherever `<run-dir>` points; Evidence keeps no state directory of its
  own. Scorer finds runs by enumerating `${AW_STATE_DIR:-~/.agentic-workflow}/ui-evidence/runs/*/summary.json` (the
  Evidence `files` edge in `deps.yaml`), so a caller that wants a run counted passes
  `<run-dir>` = `$AW_STATE_DIR/ui-evidence/runs/<id>`, a new `<id>` per run. A caller that wants verdict-cache hits across
  runs passes a stable `--cache` together with `--app-build <sha>` (the default cache lives in the run directory, so it
  never hits across runs, and an entry written without an app build is trusted only by the invocation that wrote it).
- **SKILL.md** (`skills/ui-evidence`): `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js <script> <run-dir> …`
  becomes `ui-evidence run <script> <run-dir> … [--lock-timeout 120]`. The binary takes both locks itself, so the bash
  wrapper goes. The parity, plan, comment, stale, put and publish steps call `ui-evidence parity …`,
  `ui-evidence parity-plan …` and so on; `scripts/doctor.sh` becomes `ui-evidence doctor` (unhealthy is exit 2); the
  provenance probe becomes `ui-evidence provenance`; `npx playwright install chromium` becomes `ui-evidence
  install-browser`.
- **bugfix-state** (`run-ui`): drops the `process.execPath, deps.uiEvidenceBin` argv (`deps.ts` `siblingUiEvidence`)
  and runs `ui-evidence run <script> <run-dir> --app-build <sha>` with a new `<run-dir>` per run; reads `summary.json`
  per the field changes below (`steps[].status`, `appBuild`, `scriptSha256`: its `UiSummarySchema` already accepts all
  three as written); treats exit 75 as "stack busy, try again" rather than a failed check; and its "wrote no summary"
  refusal now covers exit 4 and 75 as well as a crash. That refusal relies on the fresh directory: `run` deletes an
  earlier `summary.json` only once both locks are held, so a reused directory can still hold the previous run's file
  after exit 1, exit 75, or an exit 4 raised before the locks (the script unreadable, a lock directory that can't be made).
- **Scorer** (`scorer/src/ui-evidence-runs.ts`, `RunSummarySchema`): the C# summary is a new schema, and every changed
  field must move with the port, or `safeParse` fails and Scorer silently skips every C# run. The table below is the
  whole difference between what the readers' schemas expect today and what `ui-evidence run` writes.

Every key in `summary.json` is always written; a value that is unknown or wasn't given is `null`, never a missing key,
so a reader that accepts "missing" (`.optional()`) must also accept `null` (`.nullable()`) wherever the type says
`| null`. Pinned by `RunSummaryShapeTests` (a full and a minimal summary: the keys, their order, and which are null).

| Field | Today's readers expect | C# writes | Reader change |
|---|---|---|---|
| `ts` | string, required (Scorer) | gone: `startedAt` and `finishedAt`, UTC `O` with seven fractional digits (`2026-10-10T12:00:00.0000000Z`) | Scorer reads `finishedAt` (parse it, don't compare strings) |
| `steps[].name` | string, required (Scorer) | gone: a step is `viewport`, `index`, `action`, `target`, `status`, `screenshot` | Scorer drops `name` |
| `steps[].status`, `steps[].screenshot` | as written | unchanged | none |
| `visual` | `unchecked` \| `unchanged` \| `looks-right` \| `looks-off` \| `sloppy` (Scorer) | an object: `verdict` (those five), `reasons`, `diffScore` (number \| null), `diffImage` (string \| null) | Scorer reads `visual.verdict` |
| `visualReasons` | string[], required (Scorer) | gone: `visual.reasons` | Scorer reads `visual.reasons` |
| `diffScore` (top level) | not read | gone: `visual.diffScore` | none |
| `pr` | string, optional (Scorer: `z.string().optional()`) | string \| null, always written (`null` when the script has no `planning.pr`) | Scorer: `z.string().nullable()`; without it every run whose script has no `planning.pr` is skipped |
| `route` | string, optional (Scorer) | string, always written | none |
| `appBuild` | string \| null, optional (bugfix-state) | string \| null, always written | none |
| `scriptSha256` | string \| null, optional (bugfix-state) | string, always written (never null) | none; bugfix-state's "no check hash" refusal can no longer fire |
| `lintFindings[]` | `rule`, `selector`, `detail` (Scorer) | adds `viewport`; `rule` is one of the eight names in the table above | none (a non-strict object ignores `viewport`) |
| `invocations[]` | optional array; `phase`, `ok`, `elapsedMs`, `inputTokens`, `outputTokens` (number \| null), `cacheHit` optional (Scorer) | always written (`[]` when no model ran); adds `model` (string \| null); `cacheHit` always written | none required; Scorer may drop `.optional()` |
| `planning` | not read | object \| null; inside, `pr` and `model` are string \| null, `elapsedMs`, `inputTokens`, `outputTokens` number \| null | a reader that adds it must accept `null` |
| `fixtures` | not read | string \| null | as `planning` |
| `schema`, `runId`, `runInstance`, `host`, `viewports`, `evidence` | not read | new, always written | none (ignored by a non-strict object) |

- Exit 4 (the run errored) and 75 (a lock stayed busy) are new; a caller that only knew 0, 1 and 2 must treat both as
  "no evidence", never as a pass.
