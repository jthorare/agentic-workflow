#!/usr/bin/env bash
# scripts/install-cs.sh — the one install of the toolkit's C# binaries (cutover spec §7.1): publish them for this platform
# into a stamped build directory, then link small wrappers for them into CLAUDE_LOCAL_BIN. Until the switch PR, nothing
# calls it (setup.sh, the providers, launchd): it is run by its test and by hand.
#
#   install-cs.sh [--publish | --link] [--rebuild] [--dry-run]
#   --publish   the publish steps only (SDK, temporal, publish, verify, prune); `current` and the wrappers are not touched
#   --link      switch bin/cs/current to this source's build (which must exist), write the wrappers, install the browser
#   neither     both
#   --rebuild   publish again although a build matches the source
#   --dry-run   print what would happen, write nothing
#   Exit 0 done or current; 1 failed (a failed --publish changes nothing on PATH or in `current`); 2 usage.
#   Output lines start `install-cs:`, no colour. Bash 3.2 compatible (macOS /bin/bash).
#
# Builds live in $AW_STATE_DIR/bin/cs/<stamp>/ (the stamp hashes the SDK pin, the platform, the commit, and every tracked and
# untracked source file the publishes read), `bin/cs/current` is the symlink --link switches (atomically). The newest two
# builds are kept, plus the one `current` names, and any build a running control plane executes (the pid in control.json,
# alive, its command line naming the build dir): that one is neither pruned nor rebuilt (it says to run `sindri stop`).
# A publish while a control plane runs first checks for open agent runs (scripts/lib/sindri-open-runs.sh, #1000): it lists
# them and asks go/wait/stop on a terminal, and stops without one unless AW_SINDRI_STOP_OK=1.
# One publish at a time per state dir (a lock dir holding the owner's pid; a dead owner's lock is taken over).
#
# Binaries (all published from a copy of the sources, under the box-wide heavy slot when it exists, so the checkout's lock
# files never change):
#   sindri       src/Sindri/Aw.Sindri.Host        framework-dependent (the Temporal worker cannot be trimmed)
#   sindri-ui    src/Sindri/Aw.Sindri.Ui.Desktop  NativeAOT, with its native libraries
#   judge        src/Judge/Aw.Judge.Host          NativeAOT, with libe_sqlite3.* beside it
#   aw-packets   src/Packets/Aw.Packets.App       NativeAOT
#   bugfix-state src/BugFix/Aw.BugFix.Host        NativeAOT
#   ui-evidence  src/Evidence/Aw.Evidence.Host    framework-dependent
#   aw-readiness src/Readiness/Aw.Readiness.App   NativeAOT
# aw-meta is not here: it stays opt-in through scripts/install-meta.sh.
#
# Every published binary answers one state-free call before the build is made a candidate ("install didn't take" otherwise):
#   sindri `help`; judge `health` (scratch AW_STATE_DIR, rules only); ui-evidence `version` (names the commit); aw-packets,
#   bugfix-state and aw-readiness have no help or version route, so a no-argument call must exit below 126 with a usage line
#   (scratch AW_STATE_DIR); sindri-ui is checked by presence only (any argument opens a window).
# The shipped recipes are staged from HEAD (git archive) and embedded; their eval gate runs when the manifest digest differs
# from the last successful publish's.
#
# Wrappers (~/.local/bin/{sindri,judge,bugfix-state,ui-evidence,aw-packets,aw-readiness}, each written to a temp file and
# moved over any TS wrapper of the same name): export DOTNET_ROOT and exec bin/cs/current/<name> by its resolved path. The
# `sindri` wrapper also exports AW_SINDRI_JUDGE, AW_PACKETS_BIN and AW_UI_EVIDENCE_BIN (unless the caller set them), so bare
# `sindri` finds sindri-ui next to it. A wrapper never publishes.
#
# Environment: AW_STATE_DIR (default ~/.agentic-workflow), CLAUDE_LOCAL_BIN (default ~/.local/bin), DOTNET_ROOT,
#   AW_SINDRI_HOME (control.json for the running-serve guard; default $AW_STATE_DIR/sindri-app),
#   AW_CS_SKIP_DOWNLOADS=1 skip the network steps (SDK install, temporal fetch, Chromium); the test's seam.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
root="$(cd "$here/.." && pwd -P)"

say() { echo "install-cs: $*"; }
warn() { echo "install-cs: warning: $*" >&2; }
die() { echo "install-cs: $*" >&2; exit 1; }
usage() { echo "usage: install-cs.sh [--publish | --link] [--rebuild] [--dry-run]" >&2; exit 2; }

do_publish=1 do_link=1 rebuild=0 dry=0 mode=0
for a in "$@"; do
  case "$a" in
    --publish) do_link=0; mode=$((mode + 1)) ;;
    --link) do_publish=0; mode=$((mode + 1)) ;;
    --rebuild) rebuild=1 ;;
    --dry-run) dry=1 ;;
    *) usage ;;
  esac
done
[ "$mode" -le 1 ] || usage
if [ "$do_publish" = 0 ] && [ "$rebuild" = 1 ]; then usage; fi

state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
base="$state/bin/cs"
cache="$state/cache/install-cs"
heavy="$state/bin-r1b/heavy"
link_dir="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
skip_dl="${AW_CS_SKIP_DOWNLOADS:-0}"
BINARIES=(sindri judge bugfix-state ui-evidence aw-packets aw-readiness)

case "$(uname -s)/$(uname -m)" in
  Darwin/arm64) platform=osx-arm64 ;; Darwin/x86_64) platform=osx-x64 ;;
  Linux/aarch64 | Linux/arm64) platform=linux-arm64 ;; Linux/x86_64) platform=linux-x64 ;;
  *) die "unsupported platform $(uname -s)/$(uname -m)" ;;
esac
sdk="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$root/global.json" | head -1)"
[ -n "$sdk" ] || die "no sdk version in $root/global.json"

# The stamp: what the build is made of.
commit="$(git -C "$root" rev-parse --verify -q HEAD 2> /dev/null || true)"
[ -n "$commit" ] || die "the C# binaries can only be published from a git checkout"
recipes_tree="$(git -C "$root" rev-parse --verify -q HEAD:recipes/sindri 2> /dev/null || true)"
# shellcheck source=lib/sindri-build-inputs.sh
source "$here/lib/sindri-build-inputs.sh"
PATHSPECS=("${SINDRI_BUILD_PATHSPECS[@]}" src/Evidence src/BugFix src/Readiness)
sha256() { shasum -a 256 2> /dev/null || sha256sum; }
inputs="$(mktemp "${TMPDIR:-/tmp}/install-cs-inputs.XXXXXX")"
# a tracked file deleted in the working tree is left out: it isn't part of the build, and tar would fail on it
git -C "$root" ls-files -z --cached --others --exclude-standard -- "${PATHSPECS[@]}" \
  | while IFS= read -r -d '' f; do if [ -f "$root/$f" ]; then printf '%s\n' "$f"; fi; done | LC_ALL=C sort -u > "$inputs"
[ -s "$inputs" ] || die "no build inputs found under $root"
stamp="$( { printf '%s\n' "$sdk" "$platform" "$commit" "$recipes_tree"; cat "$inputs"; (cd "$root" && git hash-object --stdin-paths < "$inputs"); } | sha256 | cut -c1-16)"
dirty=""
if [ -n "$(git -C "$root" status --porcelain -- "${PATHSPECS[@]}" 2> /dev/null)" ]; then dirty="-dirty"; fi
revision="$commit$dirty"

lock="" stage="" tree=""
cleanup() {
  rm -f "$inputs"
  if [ -n "$stage" ]; then rm -rf "$stage" "$stage".*; fi
  if [ -n "$tree" ]; then rm -rf "$tree"; fi
  if [ -n "$lock" ]; then rm -rf "$lock"; fi
}
trap cleanup EXIT

built() { # $1 = a stamp: a complete build is a build-info file written last
  [ -f "$base/$1/build-info" ]
}
# A build a running control plane executes: the pid in control.json, alive, whose command line (ps) names the build dir.
# The running control plane (#1032). serve writes control.json as one compact line, {"url":..,"pid":N,"epoch":..,
# "temporal":{"pid":M,..}}: the serve's pid is the TOP-LEVEL one, never the Temporal server's. (Same helper as
# cutover-clean-ts.sh; the two scripts are copied alone in their tests, so it is repeated, not sourced.)
control="${AW_SINDRI_HOME:-$state/sindri-app}/control.json"
serve_pid() { # the live serve pid, or nothing
  local pid
  [ -f "$control" ] || return 0
  pid="$(jq -r '.pid // empty' "$control" 2> /dev/null || true)"
  if [ -z "$pid" ]; then
    pid="$(tr -d '\n' < "$control" | sed -E 's/"temporal"[ ]*:.*//' | sed -nE 's/.*"pid"[ ]*:[ ]*([0-9]+).*/\1/p' | head -n 1)"
  fi
  case "$pid" in ''|*[!0-9]*) return 0 ;; esac
  if kill -0 "$pid" 2> /dev/null; then echo "$pid"; fi
}
serve_cmd() { # $1 = pid: its executable (Linux, " (deleted)" stripped) and its command line, one per line
  local exe=""
  if [ -d /proc/self ]; then exe="$(readlink "/proc/$1/exe" 2> /dev/null || true)"; exe="${exe% (deleted)}"; fi
  printf '%s\n%s\n' "$exe" "$(ps -o args= -p "$1" 2> /dev/null || true)"
}
in_use() { # $1 = a stamp: does the running control plane execute from it?
  local pid
  pid="$(serve_pid)"
  [ -n "$pid" ] || return 1
  case "$(serve_cmd "$pid")" in *"$base/$1/"*) return 0 ;; *) return 1 ;; esac
}
running_build() { # the build dir name the running control plane executes from (bin/cs or the pre-cutover bin/sindri-app), or nothing
  local pid cmd rest root_dir
  pid="$(serve_pid)"
  [ -n "$pid" ] || return 0
  cmd="$(serve_cmd "$pid")"
  for root_dir in "$base" "$state/bin/sindri-app"; do
    case "$cmd" in
      *"$root_dir/"*)
        rest="${cmd#*"$root_dir/"}"
        echo "${rest%%/*}"
        return 0 ;;
    esac
  done
}
current_stamp() { # the stamp `current` names
  if [ -L "$base/current" ]; then basename "$(readlink "$base/current")"; fi
}
rename_over() { # rename_over <tmp> <final>: an atomic replace (rename(2)), also over a symlink to a directory
  perl -e 'rename($ARGV[0], $ARGV[1]) or die "rename: $!\n"' "$1" "$2"
}

# --- publish ---------------------------------------------------------------------------------------------------------
publish() {
  # 1. The pinned SDK and temporal.
  if [ "$dry" = 1 ]; then
    say "[dry-run] would ensure the .NET SDK $sdk, fetch the pinned temporal CLI, and publish ${BINARIES[*]} sindri-ui into $base/$stamp"
    return 0
  fi
  local dotnet_root_given="${DOTNET_ROOT:-}" sdk_line sdk_dir
  export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  export PATH="$DOTNET_ROOT:$PATH"
  sdk_line="$(dotnet --list-sdks 2> /dev/null | grep "^$sdk " | head -1 || true)"
  if [ -z "$sdk_line" ]; then
    [ "$skip_dl" != 1 ] || die "the .NET SDK $sdk is not installed (AW_CS_SKIP_DOWNLOADS=1)"
    say "installing the .NET SDK $sdk into $DOTNET_ROOT (global.json)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version "$sdk" --install-dir "$DOTNET_ROOT" --no-path >&2 \
      || die "installing the .NET SDK $sdk failed"
  elif [ -z "$dotnet_root_given" ]; then
    sdk_dir="${sdk_line#*\[}"; sdk_dir="${sdk_dir%\]*}"
    DOTNET_ROOT="$(dirname "$sdk_dir")"
    export DOTNET_ROOT
    PATH="$DOTNET_ROOT:$PATH"
  fi
  if [ "$skip_dl" != 1 ]; then
    "$root/scripts/binaries-lock.sh" fetch temporal > /dev/null \
      || die "fetching the pinned temporal CLI failed (scripts/binaries-lock.sh fetch temporal)"
  fi

  # 2. A build that exists for the stamp is only reported; a build a running serve executes is never replaced.
  if [ "$rebuild" = 1 ] && in_use "$stamp"; then
    say "the running control plane uses build $stamp; not rebuilding it (run sindri stop, then re-run)"
    return 0
  fi
  if [ "$rebuild" = 0 ] && built "$stamp"; then
    say "build $stamp is current"
    return 0
  fi

  # Open agent runs (#1000): a publish under a running plane can stop it mid-Step. Checked with the running build's own
  # sindri (the build its command line names, else the current one).
  local pid guard_bin="" d
  pid="$(serve_pid)"
  if [ -n "$pid" ]; then
    for d in "$base"/*; do
      if [ -d "$d" ] && [ ! -L "$d" ] && [ -x "$d/sindri" ] && in_use "$(basename "$d")"; then guard_bin="$d/sindri"; break; fi
    done
    if [ -z "$guard_bin" ] && [ -x "$base/current/sindri" ]; then guard_bin="$base/current/sindri"; fi
    # shellcheck source=lib/sindri-open-runs.sh
    source "$here/lib/sindri-open-runs.sh"
    if [ -n "$guard_bin" ] && ! sindri_open_runs_guard "$guard_bin"; then exit 1; fi
  fi

  mkdir -p "$base"
  lock="$base/.lock"
  until mkdir "$lock" 2> /dev/null; do
    local owner
    owner="$(cat "$lock/pid" 2> /dev/null || true)"
    if [ -n "$owner" ] && ! kill -0 "$owner" 2> /dev/null; then rm -rf "$lock"; continue; fi
    say "waiting for another install-cs publish (pid ${owner:-?})"
    sleep 2
  done
  echo $$ > "$lock/pid"
  if [ "$rebuild" = 0 ] && built "$stamp"; then say "build $stamp is current"; return 0; fi   # published while this one waited

  mkdir -p "$cache"
  local log="$cache/publish.log" pre=() when
  : > "$log"
  if [ -x "$heavy" ]; then pre=("$heavy" --expensive); fi
  when="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  tree="$(mktemp -d "$cache/tree.XXXXXX")"
  stage="$base/.stage.$$"
  rm -rf "$stage"; mkdir -p "$stage"
  (cd "$root" && tar -cf - -T "$inputs") | tar -xf - -C "$tree"
  # The shipped recipes are HEAD's: an edited or untracked working-tree recipe is never embedded.
  rm -rf "$tree/recipes/sindri"
  if [ -n "$recipes_tree" ]; then
    if ! git -C "$root" archive HEAD recipes/sindri 2>> "$log" | tar -xf - -C "$tree" 2>> "$log"; then
      rm -rf "$tree/recipes/sindri"; tail -5 "$log" >&2
      die "could not stage recipes/sindri from HEAD (git archive failed)"
    fi
  fi
  if [ -n "$dirty" ]; then say "the working tree has uncommitted changes under the build paths; the build is stamped $revision"; fi

  local stampargs=(-p:SourceRevisionId="$revision" -p:AwBuildTime="$when")
  # pub <name> <project> <extra dotnet args>...: publish into $stage.<name>, then copy what the stage lacks into $stage
  pub() {
    local name="$1" proj="$2" out f; shift 2
    out="$stage.$name"; rm -rf "$out"
    say "publishing $name"
    (cd "$tree" && ${pre[@]+"${pre[@]}"} dotnet publish "$proj" -c Release "${stampargs[@]}" "$@" -o "$out") < /dev/null >> "$log" 2>&1 \
      || { tail -20 "$log" >&2; die "publishing $name failed (log: $log)"; }
    [ -e "$out/$name" ] || die "the publish left no $name in $out (log: $log)"
    for f in "$out"/*; do [ -e "$stage/$(basename "$f")" ] || cp -R "$f" "$stage/"; done
    rm -rf "$out"
  }
  local aot=(-r "$platform" -p:RestoreLockedMode=false)
  pub sindri src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj --no-self-contained -p:AwRecipesDir="$tree/recipes/sindri"
  pub ui-evidence src/Evidence/Aw.Evidence.Host/Aw.Evidence.Host.csproj --no-self-contained
  pub sindri-ui src/Sindri/Aw.Sindri.Ui.Desktop/Aw.Sindri.Ui.Desktop.csproj "${aot[@]}"
  pub judge src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj "${aot[@]}"
  pub aw-packets src/Packets/Aw.Packets.App/Aw.Packets.App.csproj "${aot[@]}"
  pub bugfix-state src/BugFix/Aw.BugFix.Host/Aw.BugFix.Host.csproj "${aot[@]}"
  pub aw-readiness src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj "${aot[@]}"

  # 3. "Install didn't take": each binary answers its state-free call, in a scratch state dir and with no inherited environment.
  verify "$stage" "$revision"

  # 4. The recipe evals, with the published binary over the staged set, before the build becomes a candidate.
  local digest="" manifest evals staged_count embedded_count
  if [ -n "$recipes_tree" ]; then
    manifest="$(AW_STATE_DIR="$tree/.state" "$stage/sindri" recipe manifest "$tree/recipes/sindri" --json 2>> "$log" < /dev/null)" \
      || { tail -20 "$log" >&2; die "reading the shipped recipes' manifest failed"; }
    digest="$(printf '%s' "$manifest" | jq -r '.digest // empty' 2> /dev/null || true)"
    [ -n "$digest" ] || die "the shipped recipes' manifest has no digest (a recipe does not read)"
    if [ "$digest" = "$(cat "$base/recipes-digest" 2> /dev/null || true)" ]; then
      say "the shipped recipes are unchanged since the last publish; evals skipped"
    else
      say "running the shipped recipes' evals"
      evals="$(AW_STATE_DIR="$tree/.state" "$stage/sindri" recipe eval --shipped --json 2>> "$log" < /dev/null)" \
        || { printf '%s\n' "$evals" >&2; tail -20 "$log" >&2; die "recipe evals failed; no build was made (sindri recipe eval --shipped)"; }
      staged_count="$(find "$tree/recipes/sindri" -maxdepth 1 -name '*.yaml' | wc -l | tr -d ' ')"
      embedded_count="$(printf '%s' "$evals" | jq -r '.recipes | length' 2> /dev/null || true)"
      [ "$embedded_count" = "$staged_count" ] \
        || die "the build embeds ${embedded_count:-?} recipes but recipes/sindri has $staged_count (AwRecipesDir)"
    fi
  fi

  printf 'commit=%s\nrecipes-tree=%s\nbuilt=%s\n' "$revision" "$recipes_tree" "$when" > "$stage/build-info"
  rm -rf "$tree"; tree=""
  rm -rf "$base/$stamp"
  mv "$stage" "$base/$stamp"; stage=""
  if [ -n "$digest" ]; then printf '%s\n' "$digest" > "$base/recipes-digest"; fi
  say "built $base/$stamp ($revision)"
  prune
}

verify() { # verify <dir> <revision>
  local d="$1" rev="$2" scratch b out rc
  scratch="$(mktemp -d "$cache/verify.XXXXXX")"
  for b in "${BINARIES[@]}" sindri-ui; do
    [ -x "$d/$b" ] || { rm -rf "$scratch"; die "install didn't take: $d/$b is missing or not executable"; }
  done
  run() { env -i HOME="$scratch" PATH="/usr/bin:/bin" DOTNET_ROOT="$DOTNET_ROOT" AW_STATE_DIR="$scratch/state" AW_JUDGE_PROVIDERS=rules \
      AW_SESSION_ID=install-cs "$@" < /dev/null 2>&1; }
  mkdir -p "$scratch/state"
  rc=0; out="$(run "$d/sindri" help)" || rc=$?
  [ "$rc" = 0 ] || { rm -rf "$scratch"; die "install didn't take: sindri help exited $rc"; }
  rc=0; out="$(run "$d/judge" health)" || rc=$?
  [ "$rc" = 0 ] || { rm -rf "$scratch"; die "install didn't take: judge health exited $rc: $out"; }
  rc=0; out="$(run "$d/ui-evidence" version)" || rc=$?
  { [ "$rc" = 0 ] && printf '%s' "$out" | grep -qF "${rev:0:7}"; } \
    || { rm -rf "$scratch"; die "install didn't take: ui-evidence version (exit $rc) does not name commit ${rev:0:7}: $out"; }
  for b in aw-packets bugfix-state aw-readiness; do
    rc=0; out="$(run "$d/$b")" || rc=$?
    { [ "$rc" -lt 126 ] && [ -n "$out" ]; } || { rm -rf "$scratch"; die "install didn't take: $b answered nothing usable (exit $rc)"; }
  done
  rm -rf "$scratch"
}

prune() { # keep the newest two builds, the one `current` names, and any a running control plane uses
  local keep=0 d n cur
  cur="$(current_stamp)"
  # shellcheck disable=SC2012  # directory names here are 16 hex digits
  for d in $(ls -1t "$base" 2> /dev/null); do
    n="$d"
    [ ! -L "$base/$n" ] && [ -d "$base/$n" ] && [ -f "$base/$n/build-info" ] || continue
    keep=$((keep + 1))
    if [ "$keep" -le 2 ] || [ "$n" = "$cur" ] || in_use "$n"; then continue; fi
    rm -rf "${base:?}/$n"
  done
}

# --- link ------------------------------------------------------------------------------------------------------------
shq() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"; }
write_wrapper() { # write_wrapper <name>
  local name="$1" target="$link_dir/$1" tmp
  if [ -d "$target" ]; then die "refusing: $target is a directory"; fi
  tmp="$(mktemp "$link_dir/.$name.XXXXXX")"
  {
    echo '#!/usr/bin/env bash'
    echo "# install-cs: C# wrapper for $name; scripts/install-cs.sh writes it, and publishing is its job, not this wrapper's"
    echo "export DOTNET_ROOT=\"\${DOTNET_ROOT:-$(shq "$DOTNET_ROOT")}\""
    echo "cur=\"\$(cd $(shq "$base/current") 2> /dev/null && pwd -P)\" || { echo \"$name: no C# build is linked (run scripts/install-cs.sh)\" >&2; exit 1; }"
    if [ "$name" = sindri ]; then
      echo 'export AW_SINDRI_JUDGE="${AW_SINDRI_JUDGE:-$cur/judge}"'
      echo 'export AW_PACKETS_BIN="${AW_PACKETS_BIN:-$cur/aw-packets}"'
      echo 'export AW_UI_EVIDENCE_BIN="${AW_UI_EVIDENCE_BIN:-$cur/ui-evidence}"'
    fi
    echo "exec \"\$cur/$name\" \"\$@\""
  } > "$tmp"
  chmod +x "$tmp"
  mv -f "$tmp" "$target"
}

link() {
  local b sdk_dir
  if [ "$dry" = 1 ]; then
    say "[dry-run] would point $base/current at $stamp, write wrappers for ${BINARIES[*]} into $link_dir, and run ui-evidence install-browser"
    return 0
  fi
  built "$stamp" || die "no build for this source (stamp $stamp); run scripts/install-cs.sh --publish first"
  if [ -z "${DOTNET_ROOT:-}" ]; then   # where the pinned SDK is found (the parent of its sdk dir), else ~/.dotnet
    sdk_dir="$(dotnet --list-sdks 2> /dev/null | grep "^$sdk " | head -1 | sed 's/^[^[]*\[\(.*\)\]$/\1/' || true)"
    DOTNET_ROOT="${sdk_dir:+$(dirname "$sdk_dir")}"
    DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  fi
  mkdir -p "$base" "$link_dir"
  ln -s "$stamp" "$base/.current.$$"
  rename_over "$base/.current.$$" "$base/current"
  say "current -> $stamp"
  for b in "${BINARIES[@]}"; do write_wrapper "$b"; done
  say "wrappers in $link_dir: ${BINARIES[*]}"
  case ":$PATH:" in *":$link_dir:"*) ;; *) warn "$link_dir is not on your PATH" ;; esac
  if [ "$skip_dl" != 1 ]; then
    "$link_dir/ui-evidence" install-browser < /dev/null >&2 \
      || warn "ui-evidence install-browser failed (offline?); re-run: ui-evidence install-browser"
  fi
  prune
  local running
  running="$(running_build)"
  if [ -n "$running" ] && [ "$running" != "$stamp" ]; then
    say "Sindri is still running build $running; run \`sindri stop && sindri\` to switch to $stamp"
  fi
}

if [ "$do_publish" = 1 ]; then publish; fi
if [ "$do_link" = 1 ]; then link; fi
exit 0
