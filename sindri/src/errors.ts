// Stable error codes: SND-<AREA>-<NNN>. Every code used in src/ must be
// registered here, and every registered code must be used (tests/errors.test.ts).
// docs/sindri/errors.md is generated from this table (npm run gen).
export interface ErrorDef {
  readonly summary: string;
  readonly fix: string;
}

// `fix` is a command whenever one exists (spec §10.3); a SindriError may carry a
// more specific fix for one occurrence.
export const ERRORS = {
  "SND-CLI-001": { summary: "Unknown command.", fix: "sindri help" },
  "SND-CLI-002": { summary: "Invalid arguments for this command.", fix: "Run `sindri help` and check the command's flags." },
  "SND-ITEM-404": { summary: "No such item in the ledger.", fix: "Run `sindri observe` to list items." },
  "SND-PROFILE-001": { summary: "The profile is invalid.", fix: "Run `sindri profile validate` and fix each listed key." },
  "SND-PROFILE-002": { summary: "No profile was found.", fix: "Run `sindri profile init`, pass --profile <dir>, or set AW_PROFILE_DIR." },
  "SND-PROFILE-003": { summary: "No such profile key.", fix: "See docs/sindri/profile.md for the keys." },
  "SND-PROFILE-004": { summary: "No such repo in the profile.", fix: "Use a name listed under repos in profile.yaml." },
  "SND-PROFILE-005": { summary: "The profile was written for a newer sindri.", fix: "Upgrade sindri (`scripts/install-sindri.sh` from the latest main)." },
  "SND-PROFILE-006": { summary: "The hash doesn't match the current profile.", fix: "Run `sindri profile approve` to see the current hash and diff, then approve that hash." },
  "SND-PROFILE-007": { summary: "A profile already exists there.", fix: "Edit it, or pass --force to overwrite it." },
  "SND-PROFILE-008": { summary: "This repo has no plan files for --ring0.", fix: "Run --ring0 from a repo with docs/superpowers/plans, or run `sindri profile init` without it." },
  "SND-PROFILE-009": { summary: "Not inside a git repo.", fix: "cd into the repo first." },
  "SND-PROFILE-010": { summary: "Approving a profile needs an interactive terminal.", fix: "run `sindri profile approve <hash>` yourself, in a terminal" },
  "SND-PROFILE-011": { summary: "The approval was not confirmed.", fix: "rerun and type the first 6 characters of the hash" },
  "SND-PROFILE-012": { summary: "No profile has been approved yet.", fix: "sindri profile approve" },
  "SND-PROFILE-013": { summary: "That repo name is already used for another path or file.", fix: "pass --name <another name>" },
  "SND-PROFILE-014": { summary: "That repo name is not valid.", fix: "use lowercase letters, digits and dashes (max 39)" },
  "SND-PROFILE-015": { summary: "That repo is in the live profile but not approved yet.", fix: "run `sindri profile approve`, review the diff, then approve it at a terminal (or `sindri repo onboard`, which prints both commands)" },
  "SND-PROFILE-016": { summary: "This worktree has no main checkout to add (a bare repo, a separate git dir or a submodule).", fix: "pass the main checkout's path; a bare repo has no checkout to index" },
  "SND-LOCK-001": { summary: "Another sindri run holds the lock.", fix: "wait a moment and rerun; `sindri doctor` shows the holder" },
  "SND-SCRUB-001": { summary: "A profile scrub pattern does not compile.", fix: "Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`." },
  "SND-SCRUB-002": { summary: "Staged changes contain likely secrets.", fix: "remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why" },
  "SND-SCRUB-003": { summary: "A different pre-commit hook is already installed, or core.hooksPath points the hook at a directory sindri didn't create (it never writes there).", fix: "Add both lines to that hook by hand, in this order: `sindri scrub --staged || exit 1` then `sindri shape --record --staged || true`." },
  "SND-SCRUB-004": { summary: "`scrub --staged` or `--install-pre-commit` ran outside a git repo.", fix: "cd into the repo first, or pass --repo PATH to --install-pre-commit." },
  "SND-SCRUB-005": { summary: "`git diff --cached` failed, so `scrub --staged` could not scan the staged changes.", fix: "Fix the git error shown in the details, then retry the commit." },
  "SND-SCRUB-006": { summary: "git's init.templateDir is set to a directory sindri does not own, or could not be read or set.", fix: "copy the named hook into that template dir's hooks/ yourself, or `git config --global --unset init.templateDir` and rerun `sindri repo onboard --template`" },
  "SND-SCOPE-001": { summary: "No allowed provider can run scoping.", fix: "add anthropic to providers.allowed, then sindri profile approve" },
  "SND-SCOPE-002": { summary: "A model job failed, timed out or returned junk.", fix: "rerun; the message carries the CLI's own error. If it repeats, run `claude -p hello` to check the CLI and its login" },
  "SND-SCOPE-004": { summary: "The model's answer didn't match the required shape.", fix: "rerun; the next round gets the reasons. Persistent: try another models.scoping" },
  "SND-SCOPE-005": { summary: "The run's token budget is used up.", fix: "raise scope.maxTokensPerRun (or maxTokensPerBacktest) in the profile, then sindri profile approve" },
  "SND-SCOPE-010": { summary: "Linear rejected the token.", fix: "check sources.linear.token points at a valid read-only Linear API key" },
  "SND-SCOPE-011": { summary: "Linear could not be read.", fix: "check the project URL and your network, then rerun; the message carries Linear's own error text" },
  "SND-SCOPE-020": { summary: "The brief file can't be read.", fix: "check the path you passed to sindri scope" },
  "SND-SCOPE-021": { summary: "No output directory for the scope map.", fix: "pass --out DIR or set sources.notesDir" },
  "SND-SCOPE-022": { summary: "That section isn't in the document.", fix: "check the heading number (## 13. …)" },
  "SND-SCOPE-023": { summary: "The project has no issues filed after its brief.", fix: "pick a project with later issues, or a shorter --window" },
  "SND-SCOPE-024": { summary: "Linear isn't configured as a source.", fix: "add sources.linear.token (a secret pointer), then sindri profile approve" },
  "SND-SCOPE-025": { summary: "Refusing to write a scope map into a git worktree.", fix: "use --sources file,code, or write outside the repo" },
  "SND-LEDGER-001": { summary: "The ledger was written by a newer sindri.", fix: "Upgrade sindri (`scripts/install-sindri.sh` from the latest main), then rerun." },
  "SND-SECRET-001": { summary: "A secret pointer could not be resolved.", fix: "check the pointer in the profile and that the secret exists (env var, file, keychain item or 1Password item)" },
  "SND-SECRET-002": { summary: "A secret file is readable by other users.", fix: "chmod 600 <file>" },
  "SND-LOCK-003": { summary: "This run's fencing epoch is stale; another run took over.", fix: "Nothing to do; the newer run continues. Check `sindri doctor` if this repeats." },
  "SND-TRACKER-001": { summary: "The tracker's source is missing or unreadable (for plan-file: the repo path or plan dir).", fix: "Restore that directory, or fix `path` in repos/<name>.yaml and run `sindri profile approve`." },
  "SND-TRACKER-002": { summary: "The tracker's source holds no items to read (for plan-file: no plan file matches tracker.include).", fix: "Fix tracker.include in profile.yaml (or restore the plan files), then run `sindri profile approve`." },
  "SND-TRACKER-003": { summary: "A plan file is too big to read (over 2 MiB).", fix: "Split the plan file into smaller plans." },
  "SND-TRACKER-404": { summary: "The tracker has no such item.", fix: "Check the id with `sindri observe`." },
  "SND-TRACKER-405": { summary: "This tracker can't write.", fix: "The plan-file tracker is read-only; edit the plan file." },
  "SND-INDEX-001": { summary: "All heavy-job slots are busy.", fix: "wait for the holder to finish; `sindri doctor` shows it" },
  "SND-INDEX-002": { summary: "The repo path is not a git repo.", fix: "check repos/<name>.yaml path, then sindri profile approve" },
  "SND-INDEX-003": { summary: "The index input is larger than index.maxTotalMB.", fix: "add generated or vendored paths to index.denyPaths, or raise index.maxTotalMB" },
  "SND-INDEX-404": { summary: "No index has been built for this repo.", fix: "sindri index build --repo <name>" },
  "SND-INDEX-005": { summary: "The embedding URL is not loopback.", fix: "set index.embeddings.url to http://127.0.0.1:11434 (or disable embeddings)" },
  "SND-INDEX-006": { summary: "The local embedding server failed.", fix: "sindri index setup (starts Ollama checks and pulls the model)" },
  "SND-INDEX-007": { summary: "No network sandbox is available for graphify.", fix: "macOS: sandbox-exec ships with the OS; Linux: install bubblewrap (bwrap), or set index.graph: none" },
  "SND-INDEX-008": { summary: "graphify is missing, failed or wrote no usable graph.", fix: "if graphify or its sandbox is missing: sindri index setup; if graph.json is over the cap: raise index.graphMaxMB (max 512); then sindri index build" },
  "SND-EVOLVE-001": { summary: "The toolkit repo couldn't be read as a git repository.", fix: "check the ring-0 repo's path in repos/<name>.yaml, then sindri profile approve" },
  "SND-EVOLVE-002": { summary: "No offline comparison exists for that artifact yet.", fix: "only the scope.draft prompt can be compared in this release (spec amendment 4)" },
  "SND-EVOLVE-003": { summary: "A pull request couldn't be read with gh.", fix: "check the PR number and `gh auth status`" },
  "SND-EVOLVE-004": { summary: "That proposal can't be adopted.", fix: "sindri evolve show <id> says what is missing: a won comparison, the safety clause, or a clean leak check" },
  "SND-EVOLVE-005": { summary: "That channel change isn't allowed yet.", fix: "sindri channel status shows why and what to run" },
  "SND-EVOLVE-006": { summary: "That action needs an interactive terminal.", fix: "run it yourself in a terminal" },
  "SND-EVOLVE-007": { summary: "The confirmation didn't match.", fix: "rerun and type the characters shown" },
  "SND-EVOLVE-008": { summary: "No artifact in the registry has that id.", fix: "sindri evolve status lists the registered artifacts; sindri evolve init refreshes the registry" },
  "SND-EVOLVE-009": { summary: "The judge model must differ from the model that wrote the outputs.", fix: "set models.adjudicator to a different model than models.scoping, then sindri profile approve" },
  "SND-EVOLVE-010": { summary: "The artifact registry is empty.", fix: "sindri evolve init" },
  "SND-EVOLVE-011": { summary: "The toolkit repo has no GitHub remote called origin.", fix: "git remote add origin <github url> in the toolkit repo" },
  "SND-EVOLVE-012": { summary: "That pull request can't be reflected on.", fix: "reflect only reads merged PRs by an allowed author (evolve.prAuthors)" },
  "SND-EVOLVE-013": { summary: "gh returned a reply in an unexpected shape.", fix: "update gh, then rerun" },
  "SND-EVOLVE-014": { summary: "Publishing writes into the working tree, which is on the default branch.", fix: "git switch -c docs/sindri-proposals-<week>, then rerun sindri evolve publish" },
  "SND-EVOLVE-015": { summary: "The privacy gate has no private terms to check against.", fix: "add your workplace's names to privacy.denyTerms in the private profile, then sindri profile approve; or pass --no-privacy-terms to publish with the private-terms layer skipped (the other five layers still run)" },
  "SND-EVOLVE-016": { summary: "That proposal's status doesn't allow a comparison.", fix: "sindri evolve show <id> prints its status; a rejected, adopted, merged or staged proposal is never compared again" },
  "SND-EVOLVE-017": { summary: "A private repo has no usable built code index (none, unbuilt or damaged), so publish can't check proposals for its identifiers.", fix: "sindri index build --repo <name> (remove a damaged index file first), then rerun sindri evolve publish" },
  "SND-EVOLVE-018": { summary: "The English word list publish uses to tell private identifiers from ordinary words is missing.", fix: "install a word list at /usr/share/dict/words (or point SINDRI_WORDS at one), then rerun sindri evolve publish" },
  "SND-EVOLVE-019": { summary: "Publish couldn't list a private repo's tracked files, so it can't check proposals for their names.", fix: "check that the repo file's path is that repo's git checkout on this machine, then rerun sindri evolve publish" },
  "SND-CLI-900": { summary: "Unexpected internal error (a bug).", fix: "rerun with SINDRI_DEBUG=1 and report the output" },
} as const satisfies Record<string, ErrorDef>;

export type ErrorCode = keyof typeof ERRORS;

export class SindriError extends Error {
  readonly fix?: string;
  readonly details: string[];
  // 1 when the human can fix it and rerun (spec §10.3 "attention"); default 2.
  readonly exitCode?: 1 | 2;
  constructor(
    readonly code: ErrorCode,
    message: string,
    more: { fix?: string; details?: string[]; exitCode?: 1 | 2 } = {},
  ) {
    super(message);
    this.name = "SindriError";
    this.fix = more.fix;
    this.details = more.details ?? [];
    this.exitCode = more.exitCode;
  }
}
