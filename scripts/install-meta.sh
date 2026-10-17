#!/usr/bin/env bash
# Install the /meta harness (opt-in: ./setup.sh --with-meta; owner decision 2026-10-14): the aw-meta NativeAOT CLI,
# its hooks for the chosen provider, and the team launcher. aw-meta is a standalone pre-switch C# CLI like aw-readiness and
# aw-packets (docs/csharp/meta.md): its state lives under $AW_STATE_DIR/meta, and
# it appends to the team's running log; its gate and merge commands act on the team's checkout and PRs.
#
#   install-meta.sh [--provider claude|codex|cursor] [--allow-worktree]   default claude
#   --allow-worktree  install from this tree although it is not the canonical checkout: a linked worktree, a second clone, or any
#                     tree other than the team config's repo (the name is historical; it covers all three)
#   Exit codes: 0 installed; 1 failed (usage, an unsupported OS, a missing binary, a failed publish); 3 refused because this tree is not the
#   canonical checkout (setup.sh --with-meta warns on 3 and carries on, and stops on every other non-zero exit)
#   AW_DRY_RUN=1     print what would happen, write nothing
#   AW_SKIP_BUILD=1  don't publish; install the binary already in $AW_STATE_DIR/bin (tests)
#   CLAUDE_LOCAL_BIN where the aw-meta and aw-meta-launch links go (default ~/.local/bin)
#
# Supported OSes: Linux (x64, arm64) and macOS (arm64, x64: the host RID is whatever `dotnet --info` reports, osx-arm64 or osx-x64). Any
# other OS fails at once with exit 1 and says why; this script never skips quietly. On macOS aw-meta reads memory, swap and the process
# table through sysctl, vm_stat, ps and lsof instead of /proc (docs/csharp/meta.md "OS support"); NativeAOT needs Xcode's command line tools.
#
# 1. Publish src/Meta/Aw.Meta.Host with NativeAOT for the host RID, on a copy of the files it needs (a RID publish
#    adds the host's ILCompiler packs to the lock files, so it never runs on the checkout), under the heavy-job lock
#    when it exists. The binary and its bundled e_sqlite3 (libe_sqlite3.so, or libe_sqlite3.dylib on macOS) land in
#    $AW_STATE_DIR/bin. The publish is stamped with this tree's commit (-p:SourceRevisionId=$(git rev-parse HEAD)) and a UTC build
#    time (-p:AwBuildTime), and the installed version is printed from `aw-meta version`; scripts/meta-launch.sh warns when the
#    installed binary is older than the Meta sources it is launched from. A failed publish exits 1 and leaves the installed binary alone.
# 2. Hooks, per provider:
#    claude: Stop → meta-stop-gate.sh, PostToolUse with matcher SendMessage → meta-outbox.sh. Both pass straight through for
#      every session but the registered meta one.
#    codex, cursor (the lane hooks for non-Claude Meta lanes, docs/csharp/meta.md "Non-Claude lanes"; user-wide, through the
#      provider adapters in $AW_STATE_DIR/hooks): session-id-report.sh at SessionStart, meta-lane-gate.sh at UserPromptSubmit
#      and Stop (Codex) or sessionStart, postToolUse and stop (Cursor). Both pass straight through for every session that is
#      not a lane (AW_META_TEAM and AW_META_LANE, set by scripts/meta-lane.sh). Codex records trust against each hook's hash:
#      the install prints the /hooks step.
# 3. Links aw-meta and aw-meta-launch (scripts/meta-launch.sh) into CLAUDE_LOCAL_BIN. aw-meta-launch points into the canonical
#    checkout only. Before anything is built or written: when the team config (team.json, $AW_META_CONFIG or
#    $AW_STATE_DIR/meta/team.json) names a repo with scripts/meta-launch.sh, this script must run from that repo; with no team repo, a
#    linked git worktree (git-dir differs from git-common-dir) is refused. --allow-worktree (the owner's call) permits either and
#    links this tree.
# The /meta skill itself is linked by setup.sh with the other skills.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$SCRIPT_DIR/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}
allow_worktree=0
[ "${1:-}" = --allow-worktree ] && { allow_worktree=1; shift; }
[ $# -eq 0 ] || { echo "usage: install-meta.sh [--provider claude|codex|cursor] [--allow-worktree]" >&2; exit 1; }

STATE="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
BIN="$STATE/bin"
LOCAL_BIN="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
HEAVY="$STATE/bin-r1b/heavy"
dry() { [ "${AW_DRY_RUN:-0}" = 1 ]; }

case "$(uname)" in
  Linux | Darwin) ;;
  *)
    echo "install-meta: aw-meta is supported on Linux and macOS only (this is $(uname)); not installing" >&2
    exit 1
    ;;
esac
# shellcheck source=meta-version-lib.sh
. "$SCRIPT_DIR/scripts/meta-version-lib.sh"

# Never silently install an unmerged build. The team config's repo (when it has the launcher) is the canonical checkout: this
# script must run from it; from any other tree (a worktree, a second clone) it refuses. With no team repo, a linked git worktree
# is refused. --allow-worktree is the explicit override, and then aw-meta-launch is linked into this tree.
LAUNCH_DIR="$SCRIPT_DIR"
# shellcheck source=meta-team-lib.sh
. "$SCRIPT_DIR/scripts/meta-team-lib.sh"
meta_amb=0
meta_resolve "$STATE" "${AW_META_TEAM:-}" 2> /dev/null || meta_amb=1   # several teams and none selected: which team's repo to check is unknown
if [ "$meta_amb" = 1 ] && [ "$allow_worktree" != 1 ]; then
  echo "install-meta: several teams exist and none is selected, so the team's repo to install from is unknown." >&2
  echo "  Set AW_META_TEAM=<name> (aw-meta team-list shows them), or pass --allow-worktree to install from this tree on purpose." >&2
  exit 3
fi
team_json="${AW_META_CONFIG:-$META_CONFIG}"
team_repo="$(jq -r '.repo // empty' "$team_json" 2>/dev/null || true)"
if [ "$allow_worktree" != 1 ]; then
  if [ -n "$team_repo" ] && [ -f "$team_repo/scripts/meta-launch.sh" ]; then
    if [ "$(cd "$team_repo" && pwd -P)" != "$(cd "$SCRIPT_DIR" && pwd -P)" ]; then
      echo "install-meta: this tree ($SCRIPT_DIR) is not the team's repo ($team_repo); it would install an unmerged build." >&2
      echo "  Run scripts/install-meta.sh from $team_repo, or pass --allow-worktree to install from this tree on purpose." >&2
      exit 3
    fi
  else
    gd="$(git -C "$SCRIPT_DIR" rev-parse --absolute-git-dir 2>/dev/null || true)"
    gc="$(git -C "$SCRIPT_DIR" rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
    if [ -n "$gd" ] && [ -n "$gc" ] && [ "$gd" != "$gc" ]; then
      echo "install-meta: $SCRIPT_DIR is a linked git worktree, not the canonical checkout; aw-meta-launch would be linked into it." >&2
      echo "  Run it from the canonical checkout, set the team config's repo (aw-meta team-init --repo <dir>), or pass --allow-worktree." >&2
      exit 3
    fi
  fi
fi

# 1. Build.
if [ "${AW_SKIP_BUILD:-0}" = 1 ]; then
  [ -x "$BIN/aw-meta" ] || { echo "install-meta: AW_SKIP_BUILD=1 but $BIN/aw-meta is missing" >&2; exit 1; }
elif dry; then
  echo "  [dry-run] would publish src/Meta/Aw.Meta.Host (NativeAOT) to $BIN/aw-meta"
else
  export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  # The SDK global.json pins (rollForward disable): installed into ~/.dotnet with Microsoft's dotnet-install.sh when missing
  sdk="$(jq -r '.sdk.version' "$SCRIPT_DIR/global.json")"
  if ! dotnet --list-sdks 2>/dev/null | grep -q "^$sdk "; then
    echo "  meta: installing the .NET SDK $sdk into ~/.dotnet (global.json)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version "$sdk" --install-dir "$HOME/.dotnet" --no-path \
      || { echo "install-meta: installing the .NET SDK $sdk failed" >&2; exit 1; }
  fi
  work="$STATE/cache/meta-build"
  mkdir -p "$work"
  tree="$(mktemp -d "$work/tree.XXXXXX")"
  git -C "$SCRIPT_DIR" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' \
    'BannedSymbols*.txt' CodeMetricsConfig.txt src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Meta \
    | (cd "$SCRIPT_DIR" && tar --null -T - -cf -) | tar -xf - -C "$tree"
  rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
  stamp=(-p:AwBuildTime="$(date -u +%Y-%m-%dT%H:%M:%SZ)")
  commit="$(git -C "$SCRIPT_DIR" rev-parse HEAD 2> /dev/null || true)"
  [ -n "$commit" ] && stamp+=(-p:SourceRevisionId="$commit")
  run=(dotnet publish "$tree/src/Meta/Aw.Meta.Host/Aw.Meta.Host.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false "${stamp[@]}" -o "$tree/out")
  [ -x "$HEAVY" ] && run=("$HEAVY" "${run[@]}")
  echo "  meta: publishing aw-meta ($rid) — takes a box-wide build slot"
  "${run[@]}" < /dev/null > "$work/publish.log" 2>&1 || { tail -20 "$work/publish.log" >&2; exit 1; }
  mkdir -p "$BIN"
  for f in aw-meta libe_sqlite3.so libe_sqlite3.dylib; do
    [ -e "$tree/out/$f" ] || continue
    cp "$tree/out/$f" "$BIN/.$f.new" && mv "$BIN/.$f.new" "$BIN/$f"
  done
  find "$tree" -delete
  echo "  meta: installed $BIN/aw-meta"
fi
[ -x "$BIN/aw-meta" ] && echo "  meta: $(meta_version_line "$BIN/aw-meta")"

# 2. Hooks.
aw_hooks_init "$AW_PROVIDER"
if [ "$AW_PROVIDER" = claude ]; then
  if dry; then
    echo "  [dry-run] would install meta-stop-gate (Stop) and meta-outbox (PostToolUse SendMessage) in $AW_HOOKS_CONFIG"
  else
    mkdir -p "$AW_HOOKS_INSTALL_DIR/lib"
    for h in meta-stop-gate.sh meta-outbox.sh; do
      cp "$SCRIPT_DIR/config/hooks/$h" "$AW_HOOKS_INSTALL_DIR/$h"
      chmod +x "$AW_HOOKS_INSTALL_DIR/$h"
    done
    cp "$SCRIPT_DIR/config/lib/failopen.sh" "$AW_HOOKS_INSTALL_DIR/lib/failopen.sh"
    stop=$(jq -nc --arg c "$AW_HOOKS_INSTALL_DIR/meta-stop-gate.sh # aw:meta-stop-gate" '{hooks:[{type:"command",command:$c,timeout:20}]}')
    out=$(jq -nc --arg c "$AW_HOOKS_INSTALL_DIR/meta-outbox.sh # aw:meta-outbox" '{matcher:"SendMessage",hooks:[{type:"command",command:$c,timeout:10}]}')
    merge_hook "$AW_HOOKS_CONFIG" Stop aw:meta-stop-gate "$stop"
    merge_hook "$AW_HOOKS_CONFIG" PostToolUse aw:meta-outbox "$out"
    echo "  meta: hooks installed in $AW_HOOKS_CONFIG"
  fi
else
  if dry; then
    echo "  [dry-run] would install session-id-report and meta-lane-gate (lane hooks) for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  else
    mkdir -p "$(dirname "$AW_HOOKS_CONFIG")"
    aw_hooks_stage
    if [ "$AW_PROVIDER" = codex ]; then
      aw_hook_set SessionStart aw:session-id-report session-id-report.sh "" 10
      aw_hook_set UserPromptSubmit aw:meta-lane-gate meta-lane-gate.sh "" 10
      aw_hook_set Stop aw:meta-lane-gate meta-lane-gate.sh "" 20
    else
      aw_hook_set sessionStart aw:session-id-report session-id-report.sh "" 10
      aw_hook_set sessionStart aw:meta-lane-gate meta-lane-gate.sh "" 10
      aw_hook_set postToolUse aw:meta-lane-gate meta-lane-gate.sh "" 10
      aw_hook_set stop aw:meta-lane-gate meta-lane-gate.sh "" 20
    fi
    echo "  meta: lane hooks installed in $AW_HOOKS_CONFIG"
  fi
  # Codex records trust against each hook's hash and runs only trusted hooks: after every install (a changed command needs it again).
  if [ "$AW_PROVIDER" = codex ]; then
    echo "  meta: open \`codex\` and run \`/hooks\` to trust the \`aw:*\` hooks (until then a Codex lane shows \"hooks not running\" in aw-meta health)"
  fi
fi

# 3. Links.
if dry; then
  echo "  [dry-run] would link aw-meta and aw-meta-launch into $LOCAL_BIN"
else
  mkdir -p "$LOCAL_BIN"
  ln -sfn "$BIN/aw-meta" "$LOCAL_BIN/aw-meta"
  ln -sfn "$LAUNCH_DIR/scripts/meta-launch.sh" "$LOCAL_BIN/aw-meta-launch"
  echo "  meta: aw-meta and aw-meta-launch in $LOCAL_BIN"
  [ -f "$META_CONFIG" ] || [ "$meta_amb" = 1 ] || echo "  meta: next, write a team config: aw-meta team-init --team <t> --repo <dir> --lanes a,b,c (example: skills/meta/examples/agentic-workflow.team.json)"
fi
