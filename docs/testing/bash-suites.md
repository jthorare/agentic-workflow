# Bash suites

Every bash suite in the repo, with what it covers. `bash scripts/ci/bash-tests.sh` runs all of them, the way CI's "Bash tests" job does: the independent ones in parallel, then its SERIAL list (the timing-sensitive ones) one at a time. `--serial` prints that list with the reasons. A new suite goes in `scripts/ci/bash-tests.sh` and in this list (`planning/TESTING.md`).

Each suite runs with its own fresh temp HOME and AW_STATE_DIR and without the lane environment (AW_META_LANE*, AW_SINDRI_CHILD, TMUX, ...). Tools keep working through GIT_CONFIG_GLOBAL, DOTNET_ROOT, NUGET_PACKAGES and npm_config_cache.

```bash
bash scripts/ci/bash-tests.sh   # each suite runs with its own fresh temp HOME and AW_STATE_DIR and without the lane environment (AW_META_LANE*, AW_SINDRI_CHILD, TMUX, ...) (tools keep working through GIT_CONFIG_GLOBAL, DOTNET_ROOT, NUGET_PACKAGES, npm_config_cache)
bash providers/tests/install.test.sh
bash providers/tests/voice.test.sh       # the aw:voice block in ~/.claude/CLAUDE.md and ~/.codex/AGENTS.md: created, appended, replaced in place, an unclosed block refused, dry run, reply.md <= 4000 bytes, {{user}} filled with the account's first name (fallback "human")
bash providers/tests/install-hooks-target.test.sh   # --target-dir on providers/{claude,codex}/install-hooks.sh: files and hook command paths rooted at DIR, the real home untouched, usage exit 2, idempotent (SB-8)
bash providers/tests/install-skills-target.test.sh  # providers/install-skills.sh --target-dir DIR: DIR/skills/<name> links into the toolkit (the managed set from setup.sh, plus bootstrap), the real home untouched, usage exit 2, idempotent, dry run links nothing (SB-8)
bash scripts/tests/sync-rules.test.sh
bash scripts/tests/test-grep.test.sh
bash scripts/tests/check-deps.test.sh
bash scripts/tests/check-rules.test.sh
bash scripts/tests/check-launcher.test.sh   # launcher.json schema, fixtures, built-ins and argvHash goldens (node scripts/check-launcher.mjs; Ajv is a dev dependency of scripts/)
bash scripts/tests/check-handoff-schema.test.sh   # schemas/handoff/inbox.openapi.yaml (the handoff inbox contract, docs/sindri/handoff.md): it parses, its operations and status codes are all there, its examples and every file in tests/fixtures/handoff/fake-inbox/ get their verdicts (node scripts/check-handoff-schema.mjs; Ajv and yaml as the other checkers)
bash scripts/tests/check-fleet.test.sh      # fleet state schema, its valid and invalid fixtures, and the /proc-shaped sessions tree (node scripts/check-fleet.mjs; Ajv as above)
bash scripts/tests/scoping-bench.test.sh   # scripts/scoping-bench.mjs: this repo's answer keys pass --check, a missing or repeated key path is a finding, usage errors exit 2, --limit 0 refused and --limit passed to sindri, a stub sindri's answers scored (node, path steps, cite keys and refs; map and record keys skipped; touch recall apart from recall; a failed query exit 1) (yaml as the other checkers)
bash scripts/tests/check-library-schemas.test.sh # schemas/library/design-doc.schema.json (the parsed design doc, docs/conventions/design-docs.md): it compiles, its examples get their verdicts, each invalid one fails at its own path (node scripts/check-library-schemas.mjs; Ajv as above)
bash scripts/tests/check-library-fixtures.test.sh # tests/fixtures/library/ (made-up design docs) against its expected.json, the contract the C# parser's tests share: every .md listed, every SND-LIB-010..019 has a fixture, good front matter passes $defs/frontMatter, broken fails, generated shas match (node scripts/check-library-fixtures.mjs; Ajv as above)
bash scripts/tests/check-profile-doc.test.sh # docs/csharp/sindri-profile.md names every SindriProfile key (node scripts/check-profile-doc.mjs)
bash scripts/tests/scaffold.test.sh
bash scripts/tests/check-env-example.test.sh
bash scripts/tests/check-skills-toolkit.test.sh   # scripts/check-skills-toolkit.sh and scripts/skills-toolkit-rewrite.sh on fixture trees: a bare toolkit path or a hand-spelled per-repo path fails with file:line, the rewrite is idempotent
bash scripts/tests/check-skill-requires.test.sh   # scripts/check-skill-requires.sh on fixture skills: good, missing sidecar, empty file, unknown key, unknown agent, unknown mcp, unknown bin (and a stub), missing or misplaced gate line, two Result blocks, an opening that never runs the gate or runs it after the first heading, the unswept list (skipped, stale and unknown entries), usage errors, then the real tree
bash scripts/tests/check-skill-blocks.test.sh   # scripts/check-skill-blocks.mjs on fixture skills: a valid manifest, bad YAML, a schema break, an unquoted comma in a flow-map label, a repeated id, args slots and flags that name no input, a required input left out, an unmapped success, values on a path input or empty, an output reading a missing key or artifact, a fact key over 64 characters or derived twice, a missing SKILL.md, zero or two aw-result fences, bad JSON, another skill's name, then the real tree (Ajv and yaml as the other checkers)
bash scripts/tests/repo-slug.test.sh   # skills/_shared/repo-slug.sh: AW_DIR from the remote and AW_STATE_DIR as before, a preset AW_DIR kept (a Sindri Step's run folder), sourced twice
bash scripts/tests/binaries-lock.test.sh
bash scripts/tests/tlc-check.test.sh   # scripts/tlc-check.sh with a stub tlc.sh: the summary lines, MC<Name>.tla, several cfgs, the exits 1/2/3 and their order, a folder without .tla or .cfg, explicit dirs, no specs; one real TLC case on specs/tla/LedgerProjector when java and the jar are present
bash scripts/tests/images-lock.test.sh
bash scripts/tests/code-index-lock.test.sh
bash scripts/tests/install-deps.test.sh
bash scripts/tests/denylist-check.test.sh
bash scripts/tests/owner-check.test.sh
bash scripts/tests/context-first-run.test.sh   # dry-run golden, the refusal of a real-looking home, a whole run on a stub sindri with no approval stop
bash scripts/tests/model-limit-check.test.sh   # scripts/model-limit-check.sh: refusals, dry runs, the fake claude and codex (limit and answer), up, pause, resume --cli and down on a stub sindri
bash scripts/tests/repo-templates.test.sh
bash scripts/tests/probe.test.sh
bash scripts/tests/probe-linear-projects.test.sh   # scripts/probe-linear-projects.sh (the projects page Ph0 probe) with stub curl and gh: output, legacy state fallback, no key exit 2, a failed call exit 1, the key never printed
bash scripts/tests/linear-probe.test.sh   # scripts/linear-probe.sh (the Linear hub Ph0 probe) with a stub stateful curl: every read-only fact line and zero mutations, the write part (labelled issues only, cleanup archives all incl. strays, left 0), a mid-write failure exit 1 with cleanup, the guards exit 2, keys and Linear text never printed
bash scripts/tests/run-test-guard.test.sh   # scripts/run-test-guard.sh (the check wrapper for the TS bugfix-state, #1130): exit code passes through, a timeout exits 124 and the EXIT trap runs, a daemonized child is gone after a normal exit and after a timeout, usage errors exit 2 (Linux only)
bash scripts/tests/heavy-reclaim-race.test.sh   # scripts/heavy.sh's dead-holder reclaim (#2058, #2065) on scratch lock dirs: two reclaimers of one dead owner never both hold the slot (TLC WrapperReclaimRace), and a slot left with only an owner.reclaim.<dead pid> is freed
bash scripts/tests/heavy-mixed-reclaim-race.test.sh   # the real scripts/heavy.sh and config/lib/locks.sh acquire_any_lock racing for one dead holder's slot on scratch lock dirs: never two jobs on it (TLC MixedReclaimRace, #2058)
bash scripts/tests/heavy-reclaim-leftovers.test.sh   # which <owner|pid>.reclaim.<N>* leftovers scripts/heavy.sh and locks.sh acquire_any_lock free on scratch lock dirs: a dead N, any N once the slot is over 6 h old, never a young slot with a live N (#2065)
bash scripts/tests/heavy-dead-holder.test.sh   # a SIGKILLed holder's slot on scratch lock dirs: scripts/heavy.sh and locks.sh acquire_any_lock free a dead <slot>/pid or <slot>/owner and an empty folder over a minute old, never a young empty one or a live pid (#2059)
bash scripts/tests/heavy-cap-reserve.test.sh   # every heavy-slot taker reads heavy-slots-cap as it reads now and non-meta takers honour heavy-meta-reserve, on scratch lock dirs: config/lib/locks.sh acquire_any_lock and the scripts/heavy.sh wrapper (#2061)
bash scripts/tests/heavy-stale-cap.test.sh   # a cap lowered while the memory guard runs keeps a waiting job off the slots above it: an awk shim slows the guard on every OS (#2718)
bash scripts/tests/heavy-full-reserve.test.sh   # heavy-meta-reserve is capped at the slot count minus one, so a lane job always has a slot: config/lib/heavy-config.sh heavy_reserve and config/lib/locks.sh acquire_any_lock on scratch lock dirs (#2060; the wrapper rows are in heavy.test.sh)
bash scripts/tests/heavy-pgid-race.test.sh   # heavy.sh kills a dead session's job even when setsid or perl is slow to make its process group: a 1 s slow setsid, a 1 s slow perl with no setsid, and a control (#2799)
bash scripts/tests/heavy-wait-file.test.sh   # scripts/heavy.sh writes locks/heavy-wait/<pid> for a job that waits for a slot (JSON fields, exact argv) and removes it when the slot is taken, on TERM and on the lock timeout; a job that gets a slot at once writes none (#3142)
bash scripts/tests/skill-lock.test.sh   # skills/_shared/skill-lock.sh called the way the skills document it (`LOCK_NAME=browser source skill-lock.sh; acquire_lock`, then release_lock): exit 0, the lock named, the file gone after release, and the default name
bash scripts/tests/skill-lock-recovery.test.sh   # skill-lock.sh after a failed or abandoned call (#1989): an abandoned lock is taken over, a failed acquire leaves the holder's pid and none of its own, the next acquire gets the lock; the #2569 reclaim guard: a dead holder's guard is cleared, a live or young one is waited on
bash scripts/tests/skill-lock-sessions.test.sh   # skill-lock.sh called the way skills call it (#1991): acquire and release in separate Bash calls of one session, another session's acquire waits while the lock is held, a session that ends while holding it is taken over
bash scripts/tests/skill-lock-portable.test.sh   # skills/_shared/skill-lock.sh has no bash 4 or GNU-only forms (mapfile, BASHPID, declare -A, sed -i, find -printf, stat -c, date -d, BASH_VERSINFO ...), so it runs on the macOS runner (#2534); SKILL_LOCK_FILE points the scan at another file
bash scripts/tests/skill-lock-reclaim-race.test.sh   # two waiters reclaiming one dead holder of a skill-lock.sh lock never both hold it (#2569): a PATH rm stub holds one waiter at its remove until the other has taken the lock
bash scripts/tests/skill-lock-release-guard.test.sh   # a non-owner release_lock and a dead-guard cleanup never destroy another waiter's lock or guard (#2581): a PATH mv stub plays a waiter that takes the lock in a release gap, a PATH rm stub plays one that makes a fresh guard during a dead-guard cleanup
bash scripts/tests/ui-before-after-stack-parallel.test.sh   # #2596: two copies of skills/ui-before-after/scripts/stack.test.sh at once must both exit 0 (each copy claims its own free 7-port block below 32768)
bash scripts/tests/find-duplicate-skills.test.sh
bash scripts/tests/weeklyretro-shortlog.test.sh   # #1898: /weeklyRetro's `git shortlog` names a revision, so it lists authors with no terminal on stdin
bash scripts/tests/canary-baseline-keys.test.sh   # #1896: every `baseline.<key>` /canary reads is a key /landAndDeploy writes to baseline.json
bash scripts/tests/check-prose.test.sh   # scripts/check-prose.sh with the real pinned vale (fetched; the vale cases SKIP offline) on scripts/tests/fixtures/check-prose/: a clean file, one per rule family (vague verbs, hedges, slop/wordy/filler, dash runs, the 80% rules as warnings, unparseable front matter), --strict, --counts, sorted output, --base on a scratch repo (added lines and new files only), usage, vale missing exit 2
bash scripts/tests/check-cs-control-bytes.test.sh   # scripts/check-cs-control-bytes.sh: the real tree passes, a planted 0x08, 0x00, 0x0B or 0x1F in a tracked .cs fails naming file:line and the byte (never printed raw), tab/CR/LF pass, untracked and non-.cs files are skipped, usage exit 2
bash scripts/tests/format-touched.test.sh   # scripts/format-touched.sh (#3564): a fake dotnet that checks a file only from the repo root with a repo-relative --include; a broken file in the absolute, cwd-relative and repo-relative forms exits 1, a clean one exits 0 with that call shape, one call per csproj, Compile Remove / no project / two projects, missing and non-.cs files exit 2, an error line with exit 0 exits 1, --fix, AW_HEAVY
bash scripts/tests/check-parity.test.sh   # scripts/check-parity.sh (Messages spec W6, #3266): the real docs/parity/slack.md passes; a bare row fails naming file:line and the row (done without evidence, partial or differs without a limit, planned without a phase, missing without #N, dropped without a reason, blocked without its P0 line, an empty or unknown status, a bare part of a comma list); a gap or repeat in the row numbers, no rows, or a wrong cell count fails; --release fails planned, differs and blocked; usage exit 2
bash scripts/tests/check-skills-git-mode.test.sh   # scripts/check-skills-git-mode.sh: every skill with a git/gh write command has a "## Git mode" section with ${AW_GIT_MODE:-full}; a planted skill without it fails naming file:line
bash scripts/tests/check-skills-worktree-path.test.sh   # scripts/check-skills-worktree-path.sh: the real tree passes; a planted fenced, inline or .sh `git worktree add` without worktree-path.sh fails naming file:line, the helper in the same file fixes it, prose "no `git worktree add`" and .sh comments pass, bootstrap/ is scanned, usage exit 2
bash scripts/tests/worktree-path.test.sh   # skills/_shared/worktree-path.sh on a temp git repo and HOME: default root, worktrees.json, ~ and relative roots, AW_WORKTREE_ROOT over the file, a root inside the repo refused, malformed JSON falls back with a warning, no jq gives the same answers, usage exit 2
bash scripts/tests/eval-git-wrapper.test.sh     # evals/stubs/bin/git: pass-through; under AW_GIT_MODE=readonly refuses commit, push, merge, rebase, cherry-pick, revert, am, tag <name>, branch <name>, worktree add, checkout -b, switch -c with exit 1 and an `aw-eval: git <sub> refused` line
bash scripts/tests/eval-av-fakes.test.sh        # evals/stubs/bin/{bun,ffmpeg,ffprobe}: the planted probe answers travel with the mp4 (render outside the workspace still probes has_b_frames=2), frame grabs show the beat at -ss, a missing input exits 1, *720p* is 1280x720, review.sh and deliver.sh report the B-frames, no fake names itself
bash scripts/tests/system-design-tokens.test.sh   # skills/system-design/scripts/tokens_to_css.py: known tokens and aliases map, unmapped ones are printed and keep the neutral fallback, a missing tokens file exits 1 without output
bash scripts/tests/system-design-packet.test.sh   # build_prs.py (cards, numeric order, idempotent, refusals), write_packet.py (packet.json, *.src.html skipped), packet-to-storyboard.ts parser/layout imported under node 22+ (skips without it; no bun)
bash scripts/tests/formal-verify-tlc.test.sh   # skills/formal-verify/scripts/tlc.sh: usage, fetch from a file:// lock (install, cache hit, sha256 refusal, bad lock), java 8 and a missing jar refused, a fake java (-Xmx from TLC_HEAP and bad values refused, the OutOfMemoryError line, TERM/INT/HUP to tlc.sh stop its java and remove the temp dir), then the pinned jar from tla2tools.lock and real TLC runs (a check-then-set mutex broken, its fix passing, a witness, starvation, deadlock, a parse error, a PlusCal lost update); SKIP without java 11+ or the network
bash scripts/tests/system-design-share.test.sh    # build_share.py: tdd-share.html + deck.html from the example story (two chapters, every block, 20 slides, images inlined, byte-identical rebuild), one chapter without part chrome, project tokens layered last, escaping, refusals (history markers, limits, unknown type, lifecycle states, untitled chapter, missing image), --check, packet.json share/deck keys
bash scripts/tests/system-design-diagrams.test.sh    # skills/system-design/scripts/draw_diagrams.mjs against a stub tldraw server: dark by default (darkMode export, the reported palette swapped for Sindri's by role), --theme light untouched, a bad theme exits 2
bash scripts/tests/system-design-video.test.sh   # skills/system-design-video/scripts with stub ffmpeg/ffprobe/bun: doctor.sh reports what is missing and honors PLAYWRIGHT_CORE / AW_STATE_DIR, playback-check.sh passes level 4.1 without B-frames and fails level 5.0, B-frames, non-h264 and decode errors, bad arguments exit 1
bash scripts/tests/dotnet-verify-packages.test.sh
bash scripts/tests/sindri-prove.test.sh
bash scripts/tests/evidence-one-path.test.sh   # cutover §3.4: Playwright only in src/Evidence, SKILL.md runs checks only through ui-evidence verbs
bash scripts/tests/bugfix-worktree-sweep.test.sh   # #1992: skills/bugFixOrchestrator removes worktrees only outside a meta lane; a lane asks meta for aw-meta worktree-sweep
bash scripts/tests/executable-script.test.sh   # tests that write a shell script and run it go through ExecutableScript.Write (ETXTBSY, #1251): a test .cs file with a `#!` line and a hand-set exec bit fails
bash scripts/tests/sindri-ui-fixture.test.sh
bash scripts/tests/sindri-ui-fixture-prs.test.sh   # the Projects fixture seeds the GitHub repos and three open PRs with their relates links, once (#1908)
bash scripts/tests/sindri-ui-fixture-reviews.test.sh   # the reviews fixture: review.login and workflow.forge in the profile, the PR built in a forge world inside the home (stub aw-forge-fake), its Context record, serve restarted on the forge state, idempotent, down
bash scripts/tests/forge-fixture.test.sh    # scripts/forge-fixture.sh over the real aw-forge-fake (dotnet build once): the PR babysitter's fixture PR set, every variant, the guard, --github refusals and dry run
bash scripts/tests/cutover-clean-ts.test.sh   # scripts/cutover-clean-ts.sh on a scratch box: what the TS install left, removed and nothing else; its last case runs the real bugfix-state (needs dotnet, or BUGFIX_STATE_BIN)
bash scripts/tests/cutover-prove.test.sh      # the switch proof: setup.sh on a scratch box, the C# binaries linked, the TS wrappers gone
bash scripts/tests/install-cs.test.sh         # scripts/install-cs.sh: publish, link, the wrappers, the stamps, the running-serve guard
bash scripts/tests/serve-build-guard.test.sh    # #1032: a running serve keeps its build dir (cutover-clean-ts refuses or keeps it), install-cs --link names the still-running build
bash scripts/tests/sindri-open-runs.test.sh     # the open agent runs guard install-cs.sh runs before a publish under a running serve (#1000): stub sindri, tty go/wait under script
bash scripts/tests/sindri-pod.test.sh      # scripts/sindri-pod.sh (connect, profile edit, repo add, doctor) with a fake kubectl, sindri-ui and EDITOR: --pod/--release, the dry run, the mirror home and its modes, the container wait and its state, restart and forward retry, refusals, the per-verb exit codes, --onyx (the pod's port, override warning, Onyx off, --adopt), profile edit (init, unchanged, valid, invalid then fixed or aborted, conflict), the thin verbs' exit-code passthrough
bash scripts/tests/readiness-baseline.test.sh
bash scripts/tests/agent-token.test.sh
bash scripts/tests/signal-suites-ignored-hup.test.sh   # agent-token.test.sh, formal-verify-tlc.test.sh and meta-launch.test.sh started with SIGHUP ignored (a `nohup` gate) all pass: each wrapper resets every signal it sends (#2377, #2566)
bash scripts/tests/ci-changed-areas.test.sh
bash scripts/tests/ci-bash-tests.test.sh
bash scripts/tests/ci-lane-budgets.test.sh
bash scripts/tests/tmux-isolation.test.sh   # scripts/ci/tmux-isolation.sh: no test runs a tmux outside scripts/test-tmux.sh, -L, -S or AW_TMUX
bash scripts/tests/test-ports.test.sh   # scripts/ci/test-ports.sh: no *.test.sh binds a literal listener port or assigns digits to a *PORT variable without a "test-ports: ok <reason>" waiver (#2597)
bash scripts/tests/ui-checks-xvfb-lib.test.sh   # tests/ui-checks/xvfb-lib.sh with a fake Xvfb: the display is stopped by pid on exit and an existing EXIT trap still runs
bash scripts/tests/test-tmux.test.sh
bash scripts/tests/sindri-smoke-normalize.test.sh
bash scripts/tests/aot-smoke-helpers.test.sh   # src/Sindri/aot-smoke/helpers.sh with a fake sindri: a session's manual service run gets past a scheduled run's SND-JOB-409 (#1949)
bash scripts/tests/aot-smoke-cleanup.test.sh   # src/Sindri/aot-smoke/run.sh's cleanup and traps with fake serve and temporal processes: both are gone after a normal exit, a missing control.json, TERM and INT, temporal alone, two starts and a hung stop, and an unrelated pid is left alone (#2109)
bash scripts/tests/skill-evals.test.sh      # scripts/skill-evals.sh with a stub claude: flags, exit codes 0-4, the staged rewrite (source untouched), the env allowlist and token scrub, the flock'd spend tally under two concurrent runs, budget refusal, --changed, bootstrap/ as a skill, family sets (selftest, description-change pull-in, the separate $12 cap), the fixture lint's comment lines, --selftest, --lint-descriptions, --status; the stubs gh and curl
bash scripts/tests/skill-evals-node-failure.test.sh   # scripts/skill-evals.sh --selftest with a stub node that fails on any RegExp check (#2027): a node failure is reported as SELFTEST-REGEX "node failed (exit N)" for a regex grader and a tool_used input_match, never read as "compiles fine"
bash scripts/tests/eval-gh-wrapper.test.sh   # evals/stubs/bin/gh and curl: the readonly refusals (every form), canned answers from .aw-eval/*.json (match rules, first match, no match, no file, subdirectory), pass-through to a fake real gh
bash scripts/tests/code-index-fixture.test.sh  # the code-index fixtures (tests/fixtures/code-index): verify.mjs, the sample check, the hostile generator
bash scripts/tests/meta-skill-card.test.sh   # #2498: skills/meta/SKILL.md stays under its byte cap, opens with a card per harness (Claude Code, Codex, Cursor), says to trust the CLI FAIL lines and not open gate logs, and points at docs/csharp/meta-skill-reference.md, which exists
bash scripts/tests/meta-launch.test.sh      # Linux and macOS (golden transcript; needs cc on macOS)
bash scripts/tests/meta-launch-model-stub.test.sh   # the routed-lane stub of meta-launch.test.sh: its `--no-daemon` probe (`<bin> --help`) records no launch, a real launch records argv and pid (#2637)
bash scripts/tests/meta-launch-run-model-lane.test.sh   # run_model_lane of meta-launch.test.sh: the helper returns and leaves no lane script running when the lane script hangs on its own and never runs the stub (#2643)
bash scripts/tests/meta-start-lock-guard.test.sh   # scripts/meta-start-lock.sh's takeover guard (#2598): a taker never clears a guard a live taker holds, neither one between its mkdir and its pid file nor a fresh one made after a dead holder's guard was cleared (PATH rm/rmdir stubs start a real taker in the gap)
bash scripts/tests/launcher-probe.test.sh   # launcher_cli_has_flag: finds a flag, answers no without it, ends a hanging CLI, leaves none of its children running (#2329)
bash scripts/tests/meta-version.test.sh     # the installed aw-meta's version and the launcher's staleness warning (Linux and macOS)
bash scripts/tests/install-meta.test.sh
bash scripts/tests/oom-policy.test.sh   # install-meta.sh writes the DefaultOOMPolicy=continue drop-in only when the user manager says stop (idempotent, dry run, other drop-ins untouched, never runs daemon-reload), aw-meta-launch warns once when it isn't continue; no line without systemctl or on macOS (#2931)
bash scripts/tests/meta-gate-reserve.test.sh   # scripts/meta-gate-reserve.sh (the meta gate's heavy-slot reserve, linked to $AW_STATE_DIR/bin-r1b/meta-gate-reserve by install-cs.sh) on a temp state dir: depth tiers, the heavy-slots-cap clamp, --show, --clear, usage exit 2, the file holds just the number
bash scripts/tests/heavy.test.sh              # scripts/heavy.sh (the heavy-job wrapper, linked to $AW_STATE_DIR/bin-r1b/heavy by install-cs.sh) on a temp state dir and a stub dotnet: usage, exit codes (75, 124, 130), lock files and owner line, heavy-slots-cap, the meta reserve, the memory guard, nested runs, --light/--expensive classification, UseSharedCompilation=false unless the caller sets it (#3178) (~25 s)
bash scripts/tests/meta-teams.test.sh
bash config/hooks/tests/codex-adapter.test.sh
bash config/hooks/tests/cursor-adapter.test.sh
bash config/hooks/tests/provider-install-hooks.test.sh
bash config/hooks/tests/probe-log.test.sh
bash config/hooks/tests/judge-health.test.sh
bash config/hooks/tests/sindri-nudge.test.sh
bash config/hooks/tests/sindri-tasks.test.sh   # the aw:sindri-tasks UserPromptSubmit hook on a temp AW_SINDRI_HOME and git repo: once per task per session, ids and numbers only, ssh/https origins, child sessions, bad session ids, broken files, 500 ms, seen pruning
bash config/hooks/tests/fleet-state.test.sh   # the aw:fleet hook: every Claude event's state file against the schema, PostToolUse before jq, refusals, modes, under 100 ms (real payloads in tests/fixtures/fleet-input/)
bash scripts/tests/install-fleet.test.sh      # install-fleet.sh install and --uninstall (only # aw:fleet entries), setup.sh --with-fleet --dry-run
bash scripts/tests/install-headroom.test.sh   # aw-headroom.sh and install-headroom.sh: upstream detection order, no loop, fake headroom/claude/codex, dry run writes nothing, no MCP, token never printed
bash scripts/tests/install-tldraw.test.sh     # install-tldraw.sh: brew cask on macOS, the pinned AppImage on Linux (sha256 refusal), the agent-skills marker check, fail-open, --no-tldraw
bash scripts/tests/install-tmux.test.sh       # install-tmux.sh on a temp HOME and private tmux sockets: dry run writes nothing, the config parses (status-style, ~ path, prefix chip, no clock, no 256-color numbers), idempotent re-run, backups never overwritten, no tmux = skip, --restore (plugin block last, stand-in TPM, install_plugins on the server that ran tpm), setup.sh --with-tmux[-restore] --dry-run
bash scripts/tests/external-packs.test.sh    # scripts/lib/external-packs.sh: fetch_external_pack on local git repos, the unpinned vercel-labs/ui pack follows upstream, a pinned pack stays, the subdir arg links only the user skill, setup.sh and the pin files (#3458)
bash scripts/tests/result-gate.test.sh   # every skill's Result-block gate prints AW_RESULT and a non-empty AW_DIR, run alone in a fresh shell (#2036)
bash config/hooks/tests/failopen.test.sh
bash config/hooks/tests/block-destructive.test.sh   # config/hooks/block-destructive.sh (#3621): each destructive rule blocks with the reason on stderr and none on stdout; quoted text, a heredoc body or a grep pattern that names one is not a command; a shell -c string is still judged
bash config/hooks/tests/block-push-main.test.sh   # config/hooks/block-push-main.sh (#3621) in a temp git repo: a real push to main or master is blocked with the reason on stderr and none on stdout; a heredoc body, quoted text, an echo or a later checkout of main is not a push
bash config/hooks/tests/meta-hooks.test.sh
for t in config/lib/tests/*.test.sh; do bash "$t" || echo "FAILED: $t"; done  # merge-hook, levers, installers, judge-cs-hook-surface (the hook contract: real hooks vs the C# judge; needs dotnet) and sindri-cs-hook-surface (the sindri-nudge SessionStart hook vs the C# `sindri repo status --nudge`: one line, silence, exit 0, read-only, bounded; builds the Sindri host, needs dotnet)
```
