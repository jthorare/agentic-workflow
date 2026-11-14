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
# untracked source file the publishes read), `bin/cs/current` is the symlink --link switches (atomically).
# Only the binaries whose inputs changed are published (#3041). Each binary has its own input set: the project dirs of its project and
# of every project it references (the ProjectReference closure), the root build files, and the few files outside src/ that the
# projects embed (scripts/lib/sindri-build-inputs.sh). A build keeps the hash of each binary's inputs (inputs/<name>.sha256), the
# top-level files its publish made (inputs/<name>.files) and the commit it was made at (inputs/<name>.rev). A publish compares each
# hash with the one in the build `current` names; a binary that matches is copied from that build (`install-cs: reusing judge from
# <stamp>`) in the same order as a publish, so the first file of a name still wins (sindri-ui's libSkiaSharp.so). The stamp still
# covers everything, so a change to scripts/heavy.sh alone makes a new build dir whose binaries are all copies. --rebuild, a first
# build, and a `current` build made before this (no inputs/ folder) publish all eight. Each publish still takes its own heavy slot. The newest two
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
#   sindri-code-runner  src/Sindri/Aw.Sindri.CodeRunner  framework-dependent (it loads an assembly the control plane compiled); no wrapper: sindri starts it from beside itself
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
#
# Wrappers (~/.local/bin/{sindri,judge,bugfix-state,ui-evidence,aw-packets,aw-readiness}, each written to a temp file and
# moved over any TS wrapper of the same name): export DOTNET_ROOT and exec bin/cs/current/<name> by its resolved path. The
# `sindri` wrapper also exports AW_SINDRI_JUDGE, AW_PACKETS_BIN and AW_UI_EVIDENCE_BIN (unless the caller set them), so bare
# `sindri` finds sindri-ui next to it. A wrapper never publishes.
#
# The heavy-job wrapper: scripts/heavy.sh is copied into each build as <build>/heavy (mode 755; part of the stamp, checked by verify), and
# --link points $AW_STATE_DIR/bin-r1b/heavy at bin/cs/current/heavy, so the link never resolves into a git working tree (a checkout of an
# older commit or a swept worktree cannot take it away) and changes only on an install. config/lib/heavy-config.sh rides along as <build>/heavy-config.sh (heavy sources it from its own folder). --link refuses a build without a heavy or its heavy-config.sh. A real
# file already at bin-r1b/heavy is moved to heavy.pre-repo (never deleted; a later one gets a timestamp suffix). --publish alone does
# not touch the link.
#
# The meta gate's slot reserve works the same way: scripts/meta-gate-reserve.sh is copied into each build as <build>/meta-gate-reserve
# (mode 755, part of the stamp, checked by verify), --link points $AW_STATE_DIR/bin-r1b/meta-gate-reserve at
# bin/cs/current/meta-gate-reserve, refuses a build without it, and moves a real file there to meta-gate-reserve.pre-repo.
#
# The toolkit SDK (src/Toolkit/Aw.Sindri.Toolkit, package id Aw.Sindri.Toolkit): after the publish step (also when the build is
# current) it is `dotnet pack`ed from a copy of the sources into the folder feed $AW_SINDRI_SDK_FEED (default
# $AW_SINDRI_HOME/sdk/packages) as version 0.1.0-local.<UTC yyyyMMddHHmmss>, but only when its own inputs (src/Toolkit and the
# root build files; the SDK has no Aw.Kernel dependency) changed since the stamp in <feed>/.sdk-stamp. The two
# newest packages stay in the feed. Toolkits made by /new-toolkit restore it from there (floating 0.1.0-local.*). A checkout
# without src/Toolkit/Aw.Sindri.Toolkit skips the step. `--dry-run` prints it. --link does not pack.
#
# Environment: AW_STATE_DIR (default ~/.agentic-workflow), CLAUDE_LOCAL_BIN (default ~/.local/bin), DOTNET_ROOT,
#   AW_SINDRI_HOME (control.json for the running-serve guard; default $AW_STATE_DIR/sindri-app),
#   AW_SINDRI_SDK_FEED (the toolkit SDK folder feed; default $AW_SINDRI_HOME/sdk/packages),
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
reserve="$state/bin-r1b/meta-gate-reserve"
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
# shellcheck source=lib/sindri-build-inputs.sh
source "$here/lib/sindri-build-inputs.sh"
PATHSPECS=("${SINDRI_BUILD_PATHSPECS[@]}" src/Evidence src/BugFix src/Readiness scripts/heavy.sh scripts/meta-gate-reserve.sh config/lib/heavy-config.sh)
sha256() { shasum -a 256 2> /dev/null || sha256sum; }
# hash_list <list> <header line>...: sha256 (16 hex) of the header lines, the file list and git's hash of each listed file. A git that
# dies (killed under memory pressure) stops the script with a message; piped into sha256 it ended the script silently (#3218).
hash_list() {
  local list="$1" hashes
  shift
  hashes="$(mktemp "${TMPDIR:-/tmp}/install-cs-hash.XXXXXX")"
  (cd "$root" && git hash-object --stdin-paths < "$list") > "$hashes" \
    || { rm -f "$hashes"; die "git hash-object failed while hashing the build inputs (git ended with an error; run it again)"; }
  { printf '%s\n' "$@"; cat "$list" "$hashes"; } | sha256 | cut -c1-16
  rm -f "$hashes"
}
inputs="$(mktemp "${TMPDIR:-/tmp}/install-cs-inputs.XXXXXX")"
# a tracked file deleted in the working tree is left out: it isn't part of the build, and tar would fail on it
git -C "$root" ls-files -z --cached --others --exclude-standard -- "${PATHSPECS[@]}" \
  | while IFS= read -r -d '' f; do if [ -f "$root/$f" ]; then printf '%s\n' "$f"; fi; done | LC_ALL=C sort -u > "$inputs"
[ -s "$inputs" ] || die "no build inputs found under $root"
stamp="$(hash_list "$inputs" "$sdk" "$platform" "$commit")"
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

# --- per-binary inputs (#3041) ---------------------------------------------------------------------------------------
normpath() { # normpath a/b/../c -> a/c (a path made of / segments; no symlinks looked at)
  local out="" part oldifs="$IFS"
  IFS=/; set -f
  for part in $1; do
    case "$part" in
      '' | .) ;;
      ..) case "$out" in */*) out="${out%/*}" ;; *) out="" ;; esac ;;
      *) out="${out:+$out/}$part" ;;
    esac
  done
  set +f; IFS="$oldifs"
  printf '%s' "$out"
}
project_dirs() { # project_dirs <csproj, relative to the checkout>: the project dir of it and of every project it references, transitively
  local todo="$1" seen="" p d r dirs=""
  while [ -n "$todo" ]; do
    # shellcheck disable=SC2086  # paths here hold no space or glob character
    set -- $todo; p="$1"; shift; todo="$*"
    case " $seen " in *" $p "*) continue ;; esac
    seen="$seen $p"
    d="$(dirname "$p")"
    case " $dirs " in *" $d "*) ;; *) dirs="$dirs $d" ;; esac
    [ -f "$root/$p" ] || continue
    # Include paths are relative to the csproj and may use backslashes
    for r in $(sed -n 's/.*<ProjectReference[^>]*Include="\([^"]*\)".*/\1/p' "$root/$p" | tr '\\' '/' | tr -d '\r'); do
      todo="$todo $(normpath "$d/$r")"
    done
  done
  printf '%s\n' $dirs
}
input_hash() { # input_hash <csproj>: the hash of one binary's inputs, made as the stamp is: SDK, platform, the files and their git hashes
  local list d specs=("${ROOT_BUILD_INPUTS[@]}")
  list="$(mktemp "${TMPDIR:-/tmp}/install-cs-bin.XXXXXX")"
  for d in $(project_dirs "$1"); do
    specs+=("$d")
    case "$d" in
      src/Sindri | src/Sindri/*) specs+=("${SINDRI_EXTRA_INPUTS[@]}") ;;
      src/Judge | src/Judge/*) specs+=("${JUDGE_EXTRA_INPUTS[@]}") ;;
    esac
  done
  # A git that dies here (killed under memory pressure) stops the publish with a message; it never ends the script silently (#3218).
  git -C "$root" ls-files -z --cached --others --exclude-standard -- "${specs[@]}" > "$list.z" \
    || { rm -f "$list" "$list.z"; die "git ls-files failed while hashing the inputs of $1 (git ended with an error; run it again)"; }
  while IFS= read -r -d '' f; do if [ -f "$root/$f" ]; then printf '%s\n' "$f"; fi; done < "$list.z" | LC_ALL=C sort -u > "$list"
  rm -f "$list.z"
  hash_list "$list" "$sdk" "$platform"
  rm -f "$list"
}

# --- publish ---------------------------------------------------------------------------------------------------------
publish() {
  # 1. The pinned SDK and temporal.
  if [ "$dry" = 1 ]; then
    say "[dry-run] would ensure the .NET SDK $sdk, fetch the pinned temporal CLI, and publish ${BINARIES[*]} sindri-ui sindri-code-runner into $base/$stamp"
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
  if [ -n "$dirty" ]; then say "the working tree has uncommitted changes under the build paths; the build is stamped $revision"; fi

  local stampargs=(-p:SourceRevisionId="$revision" -p:AwBuildTime="$when")
  # The build `current` names: a binary whose inputs hash equals the one recorded there is copied from it, not published (#3041).
  # No inputs/ folder (a build made before this) or --rebuild: everything is published.
  local from="" cur
  cur="$(current_stamp)"
  if [ "$rebuild" = 0 ] && [ -n "$cur" ] && [ "$cur" != "$stamp" ] && built "$cur" && [ -d "$base/$cur/inputs" ]; then from="$base/$cur"; fi
  mkdir -p "$stage/inputs"
  # pub <name> <project> <extra dotnet args>...: publish into $stage.<name>, then copy what the stage lacks into $stage;
  # or, when the hash of its inputs is the one in the current build, copy its files from there (the same order, the same rule)
  pub() {
    local name="$1" proj="$2" out f want have; shift 2
    want="$(input_hash "$proj")"
    if [ -n "$from" ]; then
      have="$(cat "$from/inputs/$name.sha256" 2> /dev/null || true)"
      if [ "$have" = "$want" ] && [ -f "$from/inputs/$name.files" ] && reuse "$name" "$from"; then
        say "reusing $name from $cur"
        return 0
      fi
    fi
    out="$stage.$name"; rm -rf "$out"
    say "publishing $name"
    (cd "$tree" && ${pre[@]+"${pre[@]}"} dotnet publish "$proj" -c Release "${stampargs[@]}" "$@" -o "$out") < /dev/null >> "$log" 2>&1 \
      || { tail -20 "$log" >&2; die "publishing $name failed (log: $log)"; }
    [ -e "$out/$name" ] || die "the publish left no $name in $out (log: $log)"
    # Dot entries too: ui-evidence's Playwright driver is .playwright/, and without it every browser call fails.
    # The file list holds every top-level entry of the output (also those the stage already had), so a later copy can walk it.
    : > "$stage/inputs/$name.files"
    for f in "$out"/* "$out"/.[!.]*; do
      [ -e "$f" ] || continue
      basename "$f" >> "$stage/inputs/$name.files"
      [ -e "$stage/$(basename "$f")" ] || cp -R "$f" "$stage/"
    done
    printf '%s\n' "$want" > "$stage/inputs/$name.sha256"
    printf '%s\n' "$revision" > "$stage/inputs/$name.rev"
    rm -rf "$out"
  }
  # reuse <name> <build dir>: pub()'s merge, from the files the earlier publish of <name> left in <build dir> (never moved: the
  # build stays whole). False, with nothing staged for <name>, when one of them is gone.
  reuse() {
    local name="$1" src="$2" f
    while IFS= read -r f; do [ -e "$src/$f" ] || return 1; done < "$src/inputs/$name.files"
    while IFS= read -r f; do
      [ -e "$stage/$f" ] || cp -R "$src/$f" "$stage/" || return 1
    done < "$src/inputs/$name.files"
    cp "$src/inputs/$name.sha256" "$src/inputs/$name.files" "$stage/inputs/"
    if [ -f "$src/inputs/$name.rev" ]; then cp "$src/inputs/$name.rev" "$stage/inputs/"; fi
  }
  local aot=(-r "$platform" -p:RestoreLockedMode=false)
  pub sindri src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj --no-self-contained
  pub sindri-code-runner src/Sindri/Aw.Sindri.CodeRunner/Aw.Sindri.CodeRunner.csproj --no-self-contained
  pub ui-evidence src/Evidence/Aw.Evidence.Host/Aw.Evidence.Host.csproj --no-self-contained
  # Keep sindri-ui BEFORE judge: pub() keeps the first file of a name, so the staged libSkiaSharp.so is sindri-ui's (SkiaSharp.NativeAssets.Linux,
  # which links fontconfig, so Avalonia finds system fonts), not judge's NoDependencies build. check_skia_fontconfig fails the install if that flips.
  pub sindri-ui src/Sindri/Aw.Sindri.Ui.Desktop/Aw.Sindri.Ui.Desktop.csproj "${aot[@]}"
  pub judge src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj "${aot[@]}"
  pub aw-packets src/Packets/Aw.Packets.App/Aw.Packets.App.csproj "${aot[@]}"
  pub bugfix-state src/BugFix/Aw.BugFix.Host/Aw.BugFix.Host.csproj "${aot[@]}"
  pub aw-readiness src/Readiness/Aw.Readiness.App/Aw.Readiness.App.csproj "${aot[@]}"

  check_skia_fontconfig "$stage"

  # The heavy-job wrapper rides in the build, so bin-r1b/heavy can point at bin/cs/current/heavy and never into a git tree.
  install -m 755 "$tree/scripts/heavy.sh" "$stage/heavy" || die "staging scripts/heavy.sh failed"
  # (a tree without it, such as a test fixture with a stub heavy, ships none; the real heavy exits 2 when it needs one)
  if [ -f "$tree/config/lib/heavy-config.sh" ]; then
    install -m 644 "$tree/config/lib/heavy-config.sh" "$stage/heavy-config.sh" || die "staging config/lib/heavy-config.sh failed"
  fi

  install -m 755 "$tree/scripts/meta-gate-reserve.sh" "$stage/meta-gate-reserve" || die "staging scripts/meta-gate-reserve.sh failed"

  # 3. "Install didn't take": each binary answers its state-free call, in a scratch state dir and with no inherited environment.
  verify "$stage" "$revision" "$tree/scripts/heavy.sh" "$tree/scripts/meta-gate-reserve.sh"

  printf 'commit=%s\nbuilt=%s\n' "$revision" "$when" > "$stage/build-info"
  rm -rf "$tree"; tree=""
  rm -rf "$base/$stamp"
  mv "$stage" "$base/$stamp"; stage=""
  say "built $base/$stamp ($revision)"
  prune
}

# On Linux the staged libSkiaSharp.so must be the build that needs libfontconfig (sindri-ui's); macOS has one SkiaSharp build for both apps.
check_skia_fontconfig() { # check_skia_fontconfig <dir>
  [ "$(uname -s)" = Linux ] || return 0
  if ! command -v readelf > /dev/null 2>&1; then
    say "readelf is missing; not checking that libSkiaSharp.so needs libfontconfig"; return 0
  fi
  local lib="$1/libSkiaSharp.so" needed
  [ -e "$lib" ] || die "install didn't take: $lib is missing (sindri-ui stages it)"
  needed="$(readelf -d "$lib" 2> /dev/null || true)"
  grep -q 'NEEDED.*libfontconfig' <<< "$needed" \
    || die "install didn't take: the staged libSkiaSharp.so does not need libfontconfig, so it is not sindri-ui's build (judge's NoDependencies one won the stage: keep sindri-ui before judge in the pub lines)"
}

verify() { # verify <dir> <revision> <heavy source> <meta-gate-reserve source>
  local d="$1" rev="$2" src="$3" rsrc="$4" scratch b out rc
  scratch="$(mktemp -d "$cache/verify.XXXXXX")"
  for b in "${BINARIES[@]}" sindri-ui sindri-code-runner; do
    [ -x "$d/$b" ] || { rm -rf "$scratch"; die "install didn't take: $d/$b is missing or not executable"; }
  done
  run() { env -i HOME="$scratch" PATH="/usr/bin:/bin" DOTNET_ROOT="$DOTNET_ROOT" AW_STATE_DIR="$scratch/state" AW_JUDGE_PROVIDERS=rules \
      AW_SESSION_ID=install-cs "$@" < /dev/null 2>&1; }
  { [ -x "$d/heavy" ] && cmp -s "$d/heavy" "$src"; } || { rm -rf "$scratch"; die "install didn't take: $d/heavy is missing, not executable or not scripts/heavy.sh"; }
  if grep -q heavy-config "$d/heavy" && [ ! -f "$d/heavy-config.sh" ]; then rm -rf "$scratch"; die "install didn't take: $d/heavy-config.sh is missing"; fi
  { [ -x "$d/meta-gate-reserve" ] && cmp -s "$d/meta-gate-reserve" "$rsrc"; } || { rm -rf "$scratch"; die "install didn't take: $d/meta-gate-reserve is missing, not executable or not scripts/meta-gate-reserve.sh"; }
  mkdir -p "$scratch/state"
  rc=0; out="$(run "$d/sindri" help)" || rc=$?
  [ "$rc" = 0 ] || { rm -rf "$scratch"; die "install didn't take: sindri help exited $rc"; }
  rc=0; out="$(run "$d/judge" health)" || rc=$?
  [ "$rc" = 0 ] || { rm -rf "$scratch"; die "install didn't take: judge health exited $rc: $out"; }
  # a ui-evidence copied from an earlier build names the commit that build was made at (inputs/ui-evidence.rev)
  if [ -s "$d/inputs/ui-evidence.rev" ]; then rev="$(cat "$d/inputs/ui-evidence.rev")"; fi
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
    # The default is its own assignment: inside "${DOTNET_ROOT:-...}" shq's single quotes would be kept as part of the value.
    echo "[ -n \"\${DOTNET_ROOT:-}\" ] || DOTNET_ROOT=$(shq "$DOTNET_ROOT")"
    echo 'export DOTNET_ROOT'
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

# A build swap can leave the live profile unreadable by the new build: say the one command, never fail the install over it.
profile_hint() {
  local out rc=0
  [ -x "$link_dir/sindri" ] || return 0
  out="$(cd / && "$link_dir/sindri" profile validate < /dev/null 2>&1)" || rc=$?
  case "$rc:$out" in
    0:*) ;;
    *) say "the profile doesn't validate with this build; run \`sindri profile validate\` to see why (edits apply on save, there is nothing to approve)" ;;
  esac
  return 0
}

link() {
  local b sdk_dir
  if [ "$dry" = 1 ]; then
    say "[dry-run] would link $heavy -> $base/current/heavy"
    say "[dry-run] would link $reserve -> $base/current/meta-gate-reserve"
    say "[dry-run] would point $base/current at $stamp, write wrappers for ${BINARIES[*]} into $link_dir, and run ui-evidence install-browser"
    return 0
  fi
  built "$stamp" || die "no build for this source (stamp $stamp); run scripts/install-cs.sh --publish first"
  [ -x "$base/$stamp/heavy" ] || die "build $stamp has no heavy; run scripts/install-cs.sh --publish --rebuild"
  if grep -q heavy-config "$base/$stamp/heavy" && [ ! -f "$base/$stamp/heavy-config.sh" ]; then
    die "build $stamp has no heavy-config.sh; run scripts/install-cs.sh --publish --rebuild"
  fi
  [ -x "$base/$stamp/meta-gate-reserve" ] || die "build $stamp has no meta-gate-reserve; run scripts/install-cs.sh --publish --rebuild"
  if [ -z "${DOTNET_ROOT:-}" ]; then   # where the pinned SDK is found (the parent of its sdk dir), else ~/.dotnet
    sdk_dir="$(dotnet --list-sdks 2> /dev/null | grep "^$sdk " | head -1 | sed 's/^[^[]*\[\(.*\)\]$/\1/' || true)"
    DOTNET_ROOT="${sdk_dir:+$(dirname "$sdk_dir")}"
    DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  fi
  mkdir -p "$base" "$link_dir"
  ln -s "$stamp" "$base/.current.$$"
  rename_over "$base/.current.$$" "$base/current"
  say "current -> $stamp"
  link_through_current heavy "$heavy"
  link_through_current meta-gate-reserve "$reserve"
  for b in "${BINARIES[@]}"; do write_wrapper "$b"; done
  say "wrappers in $link_dir: ${BINARIES[*]}"
  case ":$PATH:" in *":$link_dir:"*) ;; *) warn "$link_dir is not on your PATH" ;; esac
  if [ "$skip_dl" != 1 ]; then
    "$link_dir/ui-evidence" install-browser < /dev/null >&2 \
      || warn "ui-evidence install-browser failed (offline?); re-run: ui-evidence install-browser"
  fi
  prune
  profile_hint
  local running
  running="$(running_build)"
  if [ -n "$running" ] && [ "$running" != "$stamp" ]; then
    say "Sindri is still running build $running; run \`sindri stop && sindri\` to switch to $stamp"
  fi
}

link_through_current() { # link_through_current <name> <target path>; through current, so it changes only on an install
  local name="$1" target="$2" want="$base/current/$1"
  if [ -L "$target" ] && [ "$(readlink "$target")" = "$want" ]; then return 0; fi
  mkdir -p "$(dirname "$target")"
  if [ -e "$target" ] && [ ! -L "$target" ]; then
    if [ -e "$target.pre-repo" ]; then mv "$target" "$target.pre-repo.$(date +%s)"; else mv "$target" "$target.pre-repo"; fi
  fi
  ln -s "$want" "$target.$$"
  rename_over "$target.$$" "$target"
  say "$name -> $want"
}

# --- toolkit SDK package ---------------------------------------------------------------------------------------------
sdk_feed="${AW_SINDRI_SDK_FEED:-${AW_SINDRI_HOME:-$state/sindri-app}/sdk/packages}"
SDK_PROJECT=src/Toolkit/Aw.Sindri.Toolkit/Aw.Sindri.Toolkit.csproj
# why: Directory.Build.targets gives every production project an analyzer reference to src/Aw.Analyzers, so the copy needs it
SDK_PATHSPECS=(global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt src/Aw.Analyzers src/Toolkit)
pack_sdk() {
  [ -f "$root/$SDK_PROJECT" ] || return 0   # a checkout without the SDK has nothing to pack
  if [ "$dry" = 1 ]; then
    say "[dry-run] would pack $SDK_PROJECT into $sdk_feed (only when src/Toolkit or its build inputs changed since the last pack)"
    return 0
  fi
  local list want have
  list="$(mktemp "${TMPDIR:-/tmp}/install-cs-sdk.XXXXXX")"
  git -C "$root" ls-files -z --cached --others --exclude-standard -- "${SDK_PATHSPECS[@]}" \
    | while IFS= read -r -d '' f; do if [ -f "$root/$f" ]; then printf '%s\n' "$f"; fi; done | LC_ALL=C sort -u > "$list"
  want="$(hash_list "$list" "$sdk")"
  have="$(cat "$sdk_feed/.sdk-stamp" 2> /dev/null || true)"
  if [ "$rebuild" = 0 ] && [ "$want" = "$have" ] && ls "$sdk_feed"/Aw.Sindri.Toolkit.*.nupkg > /dev/null 2>&1; then
    say "toolkit SDK package is current ($sdk_feed)"
    rm -f "$list"; return 0
  fi
  export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  export PATH="$DOTNET_ROOT:$PATH"
  mkdir -p "$cache" "$sdk_feed"
  local ptree version out="$cache/pack-sdk.log" pre=()
  ptree="$(mktemp -d "$cache/sdk-tree.XXXXXX")"
  version="0.1.0-local.$(date -u +%Y%m%d%H%M%S)"
  (cd "$root" && tar -cf - -T "$list") | tar -xf - -C "$ptree"
  rm -f "$list"
  if [ -x "$heavy" ]; then pre=("$heavy" --expensive); fi
  say "packing the toolkit SDK $version"
  if ! (cd "$ptree" && ${pre[@]+"${pre[@]}"} dotnet pack "$SDK_PROJECT" -c Release -p:Version="$version" -p:RestoreLockedMode=false -o "$ptree/out") < /dev/null > "$out" 2>&1; then
    tail -20 "$out" >&2; rm -rf "$ptree"; warn "packing the toolkit SDK failed; the next install retries (log: $out)"; return 0
  fi
  ls "$ptree"/out/Aw.Sindri.Toolkit."$version".nupkg > /dev/null 2>&1 || { rm -rf "$ptree"; warn "the pack left no Aw.Sindri.Toolkit.$version.nupkg; the next install retries (log: $out)"; return 0; }
  cp "$ptree"/out/*.nupkg "$sdk_feed/"
  printf '%s\n' "$want" > "$sdk_feed/.sdk-stamp"
  rm -rf "$ptree"
  # keep the two newest Aw.Sindri.Toolkit packages (the version sorts by its timestamp)
  local old
  old="$(ls "$sdk_feed"/Aw.Sindri.Toolkit.*.nupkg | LC_ALL=C sort | sed '$d' | sed '$d')"
  if [ -n "$old" ]; then printf '%s\n' "$old" | while IFS= read -r f; do rm -f "$f"; done; fi
  say "packed Aw.Sindri.Toolkit $version into $sdk_feed"
}

if [ "$do_publish" = 1 ]; then publish; fi
if [ "$do_link" = 1 ]; then link; fi
# why: the SDK package is optional and packs after the link, so a failed pack (a warning; the stamp stays, so the next
# install retries) never leaves a box without its wrappers
if [ "$do_publish" = 1 ]; then pack_sdk; fi
exit 0
