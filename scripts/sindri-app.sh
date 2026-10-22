#!/usr/bin/env bash
# scripts/sindri-app.sh — the C# Sindri app in one command, `sindri-app`: publish it for this platform when the build is
# stale, then run it. Bare `sindri-app` is bare `sindri`: it starts the control plane (serve and its pinned Temporal dev
# server) or finds the running one, and opens the desktop app. Any other arguments go to the C# `sindri` unchanged
# (`sindri-app status`, `sindri-app stop`, `sindri-app item add …`).
#
#   sindri-app [--rebuild] [<sindri arguments>...]
#   sindri-app --publish-only [--rebuild]
#   --rebuild       publish again although the build matches the source
#   --publish-only  the publish steps only (the PATH link, SDK, temporal, publish): sindri is not run and no MCP hint is
#                   printed. A leading flag, optionally followed by --rebuild; any other argument is exit 2. Exit 0 when
#                   published or current, 1 when the publish failed. This is what setup.sh calls (setup-once spec §9).
#
# A side name: the TypeScript `sindri` (~/.local/bin/sindri, its launchd jobs and the nudge hook) stays as it is until the
# switch (AGENTS.md "C# build"), and nothing in the providers or launchd runs this. The one caller in setup.sh is the Onyx step
# (`./setup.sh --with-sindri`, scripts/install-onyx.sh), which runs `sindri-app onyx up`.
#
# The build is one directory, $AW_STATE_DIR/bin/sindri-app/<stamp>/: `sindri` framework-dependent (the Temporal .NET SDK
# can't be trimmed; .agents/rules/csharp.md "AOT scope") and `sindri-ui` NativeAOT for this RID with its native
# libraries (src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>), and the C# `judge` NativeAOT for this RID with its e_sqlite3
# (published as src/Judge/aot-smoke/run.sh does), side by side. Bare `sindri` looks for `sindri-ui` there
# (docs/csharp/sindri-ui.md "Launching"), and sindri-app runs sindri with AW_SINDRI_JUDGE=<build>/judge unless the caller
# set it: step.judge.choose needs `judge choose`, and the `judge` on the PATH is the TypeScript one, which has none. <stamp> hashes the content of every tracked and untracked file
# under the paths the host build reads, the pinned SDK and the platform, so a source change, committed or not, rebuilds
# and nothing else does. The newest two builds are kept, and any build a running control plane executes (its pid in control.json, alive, with the
# build dir in its command line) is neither pruned nor rebuilt: --rebuild of it is declined until `sindri-app stop`. A build
# that exists for the stamp is never published again, only made current.
# The file list is scripts/lib/sindri-build-inputs.sh, shared with src/Sindri/aot-smoke/run.sh. The host publish runs on a copy of those files (as the smoke scripts do, so the checkout's lock files never change),
# under the box-wide heavy-job slot (`heavy --expensive`) when it exists ($AW_STATE_DIR/bin-r1b/heavy), one publish at a time per state dir.
# The shipped recipes: the publish stages `git archive HEAD recipes/sindri` into the build copy (deleting the copy's
# directory first) and the host build embeds that directory (AwRecipesDir, Aw.Sindri.Host.csproj), so an edited or untracked
# working-tree recipe is never embedded. The stamp carries the commit and the git tree hash of recipes/sindri at HEAD, and
# the build dir holds them in `build-info`. A dirty tree publishes and says so; a tree that is not a git checkout does not.
# The recipe evals: before the build becomes current, the published binary runs `recipe manifest --json` over the staged
# copy and, when its digest differs from the last successful publish's ($base/recipes-digest), `recipe eval --shipped --json`;
# a failing eval, or an embedded recipe count that differs from the staged recipes/sindri/*.yaml count, fails the publish (exit 1,
# no build for the stamp, the digest not recorded). An unchanged digest skips the evals and the count check (the set was verified).
# Progress goes to stderr, so stdout is only sindri's (`sindri-app status --json` stays parseable).
#
# Every run also: installs the .NET SDK global.json pins into $DOTNET_ROOT (default: where the pinned SDK is found, else ~/.dotnet) when missing, with
# Microsoft's dotnet-install.sh; fetches the pinned temporal CLI when missing (scripts/binaries-lock.sh fetch temporal);
# and links $CLAUDE_LOCAL_BIN/sindri-app (default ~/.local/bin) to this script, unless AW_SINDRI_APP_NO_LINK=1, this tree
# is a linked git worktree, or something else already has that name (a dangling link is replaced).
#
# MCP: once the control plane is up, a bare run (or `serve`) prints the command that registers Sindri's /sindri/mcp with every
# installed provider (Claude Code's local scope, the checkout's .cursor/mcp.json and .codex/config.toml, plus Codex's project trust
# row in its user config): `sindri-app mcp register --apply --providers all`, idempotent; run it again when the port changes. It
# only prints: `--apply` is the human's verb, and sindri refuses it from anything without a terminal on stdin and stdout
# (SND-AUTH-002), which the background watcher that waits for the plane never has. The repo is the one the directory is in, or
# AW_SINDRI_MCP_REPO (printed as --repo); a linked git worktree names its main checkout's folder, which works when the profile
# names the repo after its folder (else set AW_SINDRI_MCP_REPO). AW_SINDRI_APP_NO_MCP=1 skips it.
#
# Exit: sindri's exit code; 1 when setup or a publish failed (the publish log's tail on stderr).
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

src="${BASH_SOURCE[0]}"
while [ -L "$src" ]; do
  t="$(readlink "$src")"
  case "$t" in /*) src="$t" ;; *) src="$(dirname "$src")/$t" ;; esac
done
here="$(cd "$(dirname "$src")" && pwd -P)"
root="$(cd "$here/.." && pwd -P)"
self="$here/$(basename "$src")"

say() { echo "sindri-app: $*" >&2; }
die() { say "$*"; exit 1; }

rebuild=0
publish_only=0
if [ "${1:-}" = --publish-only ]; then
  publish_only=1; shift
  if [ "${1:-}" = --rebuild ]; then rebuild=1; shift; fi
  if [ $# -gt 0 ]; then say "--publish-only takes only --rebuild after it, not '$1'"; exit 2; fi
elif [ "${1:-}" = --rebuild ]; then rebuild=1; shift; fi

state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
base="$state/bin/sindri-app"
cache="$state/cache/sindri-app-build"
heavy="$state/bin-r1b/heavy"

# 1. The PATH link (default on). Only from the canonical checkout: a worktree's link would dangle once it is removed.
link_dir="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
link="$link_dir/sindri-app"
if [ "${AW_SINDRI_APP_NO_LINK:-0}" != 1 ]; then
  gd="$(git -C "$root" rev-parse --absolute-git-dir 2>/dev/null || true)"
  gc="$(git -C "$root" rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
  if [ -n "$gd" ] && [ "$gd" != "$gc" ]; then
    :
  elif [ -L "$link" ] && [ "$(readlink "$link")" = "$self" ]; then
    :
  elif [ -e "$link" ]; then
    say "$link is already something else; left as it is (run $self directly, or remove it to link this one)"
  else
    mkdir -p "$link_dir"
    ln -sfn "$self" "$link"
    say "linked $link -> $self"
    case ":$PATH:" in *":$link_dir:"*) ;; *) say "$link_dir is not on your PATH; add it to run sindri-app by name" ;; esac
  fi
fi

# 2. The pinned SDK, on PATH for this run and the apphost (which finds the runtime through DOTNET_ROOT).
# DOTNET_ROOT, when unset, is where the pinned SDK was found (the parent of its sdk dir in `dotnet --list-sdks`), else ~/.dotnet.
dotnet_root_given="${DOTNET_ROOT:-}"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
sdk="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$root/global.json" | head -1)"
[ -n "$sdk" ] || die "no sdk version in $root/global.json"
sdk_line="$(dotnet --list-sdks 2>/dev/null | grep "^$sdk " | head -1 || true)"
if [ -z "$sdk_line" ]; then
  say "installing the .NET SDK $sdk into $DOTNET_ROOT (global.json)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version "$sdk" --install-dir "$DOTNET_ROOT" --no-path >&2 \
    || die "installing the .NET SDK $sdk failed"
elif [ -z "$dotnet_root_given" ]; then
  sdk_dir="${sdk_line#*\[}"; sdk_dir="${sdk_dir%\]*}"
  DOTNET_ROOT="$(dirname "$sdk_dir")"
  export DOTNET_ROOT
  PATH="$DOTNET_ROOT:$PATH"
fi

# 3. The pinned temporal CLI that serve starts (kept when already installed).
"$root/scripts/binaries-lock.sh" fetch temporal >/dev/null || die "fetching the pinned temporal CLI failed (scripts/binaries-lock.sh fetch temporal)"

# 4. The stamp: what the build is made of.
case "$(uname -s)/$(uname -m)" in
  Darwin/arm64) platform=osx-arm64 ;; Darwin/x86_64) platform=osx-x64 ;;
  Linux/aarch64|Linux/arm64) platform=linux-arm64 ;; Linux/x86_64) platform=linux-x64 ;;
  *) die "unsupported platform $(uname -s)/$(uname -m)" ;;
esac
# The commit and the recipes tree at HEAD: part of the stamp, and what the staged recipes are (the embedded set is HEAD's).
commit="$(git -C "$root" rev-parse --verify -q HEAD 2>/dev/null || true)"
[ -n "$commit" ] || die "Sindri can only be published from a git checkout"
recipes_tree="$(git -C "$root" rev-parse --verify -q HEAD:recipes/sindri 2>/dev/null || true)"
sha256() { shasum -a 256 2>/dev/null || sha256sum; }
mkdir -p "$base" "$cache"
base="$(cd "$base" && pwd -P)"   # the resolved path: a running plane's command line shows it (in_use)
inputs="$cache/inputs.$$"
trap 'rm -f "$inputs"' EXIT
# A tracked file deleted in the working tree is left out: it isn't part of the build, and tar would fail on it.
# shellcheck source=lib/sindri-build-inputs.sh
source "$here/lib/sindri-build-inputs.sh"
# `context` is read by the build once #287 lands; it is not in the smoke's list yet.
git -C "$root" ls-files -z --cached --others --exclude-standard -- "${SINDRI_BUILD_PATHSPECS[@]}" context \
  | while IFS= read -r -d '' f; do if [ -f "$root/$f" ]; then printf '%s\n' "$f"; fi; done | LC_ALL=C sort -u > "$inputs"
[ -s "$inputs" ] || die "no build inputs found under $root"
stamp="$( { printf '%s\n' "$sdk" "$platform" "$commit" "$recipes_tree"; cat "$inputs"; (cd "$root" && git hash-object --stdin-paths < "$inputs"); } | sha256 | cut -c1-16)"
current="$(cat "$base/current" 2>/dev/null || true)"
built() { [ -x "$base/$1/sindri" ] && [ -x "$base/$1/sindri-ui" ] && [ -x "$base/$1/judge" ]; }
# A build a running control plane executes is never replaced or pruned: the pid in control.json, alive, whose command line
# (ps) names the build dir. (A plain pid check, no lock file the plane would have to keep.)
control="${AW_SINDRI_HOME:-$state/sindri-app}/control.json"
running_pid() {
  local pid
  pid="$(jq -r '.pid // empty' "$control" 2>/dev/null || true)"
  if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then printf '%s' "$pid"; fi
}
in_use() { # $1 = a build's stamp
  local pid cmd
  pid="$(running_pid)"
  [ -n "$pid" ] || return 1
  cmd="$(ps -p "$pid" -o command= 2>/dev/null || true)"
  case "$cmd" in *"$base/$1/"*) return 0 ;; *) return 1 ;; esac
}
if [ "$rebuild" = 1 ] && in_use "$stamp"; then
  say "the running control plane uses build $stamp; not rebuilding it (\`sindri-app stop\`, then --rebuild)"
  rebuild=0
fi

# 5. Publish when there is no build for this stamp (or --rebuild), one publish at a time (a lock dir holding the owner's
# pid; a dead owner's lock is taken over). An existing build for the stamp is only made current.
make_current() { # $1 = stamp; the earlier current becomes previous
  local previous d n
  previous="$(cat "$base/previous" 2>/dev/null || true)"
  if [ -n "$current" ] && [ "$current" != "$1" ]; then previous="$current"; fi
  printf '%s\n' "$previous" > "$base/previous"
  printf '%s\n' "$1" > "$base/current.new"
  mv -f "$base/current.new" "$base/current"
  # Keep this build and the one before it, and any a running control plane uses.
  for d in "$base"/*; do
    n="$(basename "$d")"
    if [ -d "$d" ] && [ "$n" != "$1" ] && [ "$n" != "$previous" ] && ! in_use "$n"; then rm -rf "$d"; fi
  done
}
if [ "$rebuild" = 0 ] && built "$stamp"; then
  if [ "$current" != "$stamp" ]; then make_current "$stamp"; fi
else
  lock="$base/.lock"
  until mkdir "$lock" 2>/dev/null; do
    owner="$(cat "$lock/pid" 2>/dev/null || true)"
    if [ -n "$owner" ] && ! kill -0 "$owner" 2>/dev/null; then rm -rf "$lock"; continue; fi
    say "waiting for another sindri-app publish (pid ${owner:-?})"
    sleep 2
  done
  echo $$ > "$lock/pid"
  tree=""
  stage="$base/.stage.$$"
  trap 'rm -f "$inputs"; rm -rf "$lock" "$stage" "$stage.judge"; [ -z "$tree" ] || rm -rf "$tree"' EXIT
  current="$(cat "$base/current" 2>/dev/null || true)"
  # Another run may have published this stamp while this one waited.
  if [ "$rebuild" = 1 ] || ! built "$stamp"; then
    log="$cache/publish.log"
    : > "$log"
    pre=()
    if [ -x "$heavy" ]; then pre=("$heavy" --expensive); fi
    tree="$(mktemp -d "$cache/tree.XXXXXX")"
    (cd "$root" && tar -cf - -T "$inputs") | tar -xf - -C "$tree"
    rm -rf "$stage"
    # The shipped recipes are HEAD's: the copy's directory is replaced by the archive, so an edited or untracked
    # working-tree recipe is never embedded.
    rm -rf "$tree/recipes/sindri"
    if [ -n "$recipes_tree" ]; then
      # pipefail is on: a failure of either side of the pipe stops here, never leaving an empty set to be embedded.
      if ! git -C "$root" archive HEAD recipes/sindri 2>>"$log" | tar -xf - -C "$tree" 2>>"$log"; then
        rm -rf "$tree/recipes/sindri"
        tail -5 "$log" >&2
        die "could not stage recipes/sindri from HEAD (git archive failed)"
      fi
    fi
    if [ -n "$(git -C "$root" status --porcelain 2>/dev/null)" ]; then
      say "the working tree has uncommitted changes; the embedded recipes are HEAD's, and the rest of the build is the working tree's"
    fi
    say "publishing sindri (framework-dependent) and sindri-ui (NativeAOT, $platform), build $stamp; log: $log"
    # From inside the copy (host) and the checkout (UI), so global.json's SDK pin applies whatever the caller's cwd.
    (cd "$tree" && ${pre[@]+"${pre[@]}"} dotnet publish "src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release --no-self-contained \
      -p:AwRecipesDir="$tree/recipes/sindri" -o "$stage") </dev/null >>"$log" 2>&1 || { tail -20 "$log" >&2; die "publishing sindri failed"; }
    (cd "$root" && ${pre[@]+"${pre[@]}"} bash "$root/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh" "$stage") </dev/null >>"$log" 2>&1 \
      || { tail -20 "$log" >&2; die "publishing sindri-ui failed"; }
    # The C# judge, NativeAOT, on the build copy (restore adds the RID's packs to the copy's lock files, never the checkout's).
    # It goes to its own directory first; only the binary and its bundled e_sqlite3 are copied into the stage.
    say "publishing judge (NativeAOT, $platform)"
    jstage="$stage.judge"
    rm -rf "$jstage"
    (cd "$tree" && ${pre[@]+"${pre[@]}"} dotnet publish "src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj" -c Release -r "$platform" \
      -p:RestoreLockedMode=false -o "$jstage") </dev/null >>"$log" 2>&1 || { tail -20 "$log" >&2; die "publishing judge failed"; }
    [ -x "$jstage/judge" ] || die "the publish left no judge in $jstage"
    cp "$jstage/judge" "$stage/judge"
    for f in "$jstage"/libe_sqlite3.*; do if [ -f "$f" ]; then cp "$f" "$stage/"; fi; done
    rm -rf "$jstage"
    [ -x "$stage/sindri" ] || die "the publish left no sindri in $stage"
    [ -x "$stage/sindri-ui" ] || die "the publish left no sindri-ui in $stage"
    # The recipe evals (setup-once spec §5.3), with the published binary over the staged set, before the build becomes current:
    # run when the manifest digest differs from the last successful publish's, skipped when it matches. The embedded count must
    # equal the staged recipes/sindri/*.yaml count. A failure leaves no build for the stamp and no digest recorded.
    digest=""
    if [ -n "$recipes_tree" ]; then
      manifest="$("$stage/sindri" recipe manifest "$tree/recipes/sindri" --json 2>>"$log" </dev/null)" \
        || { tail -20 "$log" >&2; die "reading the shipped recipes' manifest failed"; }
      digest="$(printf '%s' "$manifest" | jq -r '.digest // empty' 2>/dev/null || true)"
      [ -n "$digest" ] || die "the shipped recipes' manifest has no digest (a recipe does not read)"
      if [ "$digest" = "$(cat "$base/recipes-digest" 2>/dev/null || true)" ]; then
        say "the shipped recipes are unchanged since the last publish; evals skipped"
      else
        say "running the shipped recipes' evals"
        evals="$("$stage/sindri" recipe eval --shipped --json 2>>"$log" </dev/null)" \
          || { printf '%s\n' "$evals" >&2; tail -20 "$log" >&2; die "recipe evals failed; the build was not made current (sindri recipe eval --shipped)"; }
        staged_count="$(find "$tree/recipes/sindri" -maxdepth 1 -name '*.yaml' | wc -l | tr -d ' ')"
        embedded_count="$(printf '%s' "$evals" | jq -r '.recipes | length' 2>/dev/null || true)"
        [ "$embedded_count" = "$staged_count" ] \
          || die "the build embeds ${embedded_count:-?} recipes but recipes/sindri has $staged_count (AwRecipesDir)"
      fi
    fi
    # The copy is done with: the final exec skips EXIT traps, so it goes here.
    rm -rf "$tree"; tree=""
    printf 'commit=%s\nrecipes-tree=%s\n' "$commit" "$recipes_tree" > "$stage/build-info"
    rm -rf "$base/$stamp"
    mv "$stage" "$base/$stamp"
    if [ -n "$digest" ]; then printf '%s\n' "$digest" > "$base/recipes-digest"; fi
    make_current "$stamp"
    say "built $base/$stamp"
    # A control plane already running keeps its build: bare sindri finds it rather than starting this one.
    pid="$(running_pid)"
    if [ -n "$pid" ] && ! in_use "$stamp"; then
      say "a control plane from an earlier build is running (pid $pid); \`sindri-app stop\`, then \`sindri-app\`, runs this one"
    fi
  fi
  rm -rf "$lock"
fi

rm -f "$inputs"
if [ "$publish_only" = 1 ]; then exit 0; fi

# 6. The MCP registration command, printed once the plane is up (a background watcher: the exec below replaces this shell).
mcp_repo="${AW_SINDRI_MCP_REPO:-}"
if [ -z "$mcp_repo" ]; then
  gitdir="$(git rev-parse --absolute-git-dir 2>/dev/null || true)"
  common="$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
  if [ -n "$gitdir" ] && [ -n "$common" ] && [ "$gitdir" != "$common" ]; then mcp_repo="$(basename "$(dirname "$common")")"; fi
fi
register_hint() { # waits up to 2 minutes for a live control plane, then prints the registration command (it can't run it: no terminal)
  local i pid url cmd="sindri-app mcp register --apply --providers all"
  if [ -n "$mcp_repo" ]; then cmd="$cmd --repo $(printf '%q' "$mcp_repo")"; fi   # pasteable even when the folder name has a space
  for i in $(seq 1 240); do
    pid="$(jq -r '.pid // empty' "$control" 2>/dev/null || true)"
    url="$(jq -r '.url // empty' "$control" 2>/dev/null || true)"
    if [ -n "$pid" ] && [ -n "$url" ] && kill -0 "$pid" 2>/dev/null; then
      say "mcp: to register Sindri's MCP server with your providers (once, and again when the port changes), run: $cmd"
      return 0
    fi
    sleep 0.5
  done
  say "mcp: the control plane didn't come up in 2 minutes; once it is up, run: $cmd"
}
if [ "${AW_SINDRI_APP_NO_MCP:-0}" != 1 ] && { [ $# -eq 0 ] || [ "${1:-}" = serve ]; }; then
  register_hint >&2 </dev/null &
fi
# step.judge.choose runs the judge published with this build, unless the caller named one.
export AW_SINDRI_JUDGE="${AW_SINDRI_JUDGE:-$base/$stamp/judge}"
exec "$base/$stamp/sindri" "$@"
