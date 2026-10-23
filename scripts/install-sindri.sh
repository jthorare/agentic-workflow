#!/usr/bin/env bash
# Build sindri and install the ~/.local/bin/sindri CLI wrapper.
#
#   install-sindri.sh                 npm ci + build, then write the wrapper
#   AW_DRY_RUN=1 install-sindri.sh    print what would happen, write nothing
#   AW_SKIP_BUILD=1 install-sindri.sh write the wrapper only (tests; dist/ already built)
#   AW_SKIP_LAUNCHD=1 install-sindri.sh skip the observe, index and evolve launchd jobs (macOS; tests)
#   CLAUDE_LOCAL_BIN=DIR             where the wrapper goes (default ~/.local/bin)
#   install-sindri.sh --hook-only [--provider claude|codex|cursor]
#                                     install only the SessionStart nudge (aw:sindri-nudge) for
#                                     that host (default claude); AW_DRY_RUN=1 prints it. A plain
#                                     install never installs the nudge: it prints the hint.
#   install-sindri.sh --channel stable|next [--ref <sha>]
#                                     build a merged ref (default HEAD; must be an ancestor of
#                                     origin/<default branch>) into $AW_STATE_DIR/sindri/channels/
#                                     <channel>/<sha>/ and point sindri (stable) or sindri-next at it.
#                                     AW_SINDRI_SRC=DIR picks the source repo (tests).
#                                     --channel stable only bootstraps: once a stable exists, a
#                                     stable change goes through `sindri channel promote <sha>`.
#
# The git template hook is a separate, explicit opt-in (it changes global git config):
# `sindri repo onboard --template`. This script only prints that as a hint.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SINDRI_DIR="$SCRIPT_DIR/sindri"
BIN_DIR="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$SCRIPT_DIR/config/hooks/adapters/install-lib.sh"
PROVIDER_GIVEN=0
for a in "$@"; do
  case "$a" in --provider|--provider=*) PROVIDER_GIVEN=1 ;; esac
done
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

SRC_REPO="${AW_SINDRI_SRC:-$SCRIPT_DIR}"
USAGE="usage: install-sindri.sh [--hook-only [--provider claude|codex|cursor]] | [--channel stable|next [--ref <sha>]]"
HOOK_ONLY=0
CHANNEL=""
REF=""
while [ $# -gt 0 ]; do
  case "$1" in
    --hook-only) HOOK_ONLY=1; shift ;;
    --channel) [ $# -ge 2 ] || { echo "$USAGE" >&2; exit 1; }; CHANNEL="$2"; shift 2 ;;
    --ref) [ $# -ge 2 ] || { echo "$USAGE" >&2; exit 1; }; REF="$2"; shift 2 ;;
    *) echo "$USAGE" >&2; exit 1 ;;
  esac
done
case "$CHANNEL" in
  ""|stable|next) ;;
  *) echo "--channel must be stable or next" >&2; exit 1 ;;
esac
if [ -z "$CHANNEL" ] && [ -n "$REF" ]; then
  echo "--ref only makes sense with --channel" >&2
  exit 1
fi
if [ "$HOOK_ONLY" = "1" ] && [ -n "$CHANNEL" ]; then
  echo "$USAGE" >&2
  exit 1
fi
if [ "$PROVIDER_GIVEN" = "1" ] && [ -n "$CHANNEL" ]; then
  echo "--provider has no meaning with --channel (a channel build is not per provider)" >&2
  exit 1
fi

# The nudge is silent until sindri is installed, so it may land before or after the build.
if [ "$HOOK_ONLY" = "1" ]; then
  aw_hooks_init "$AW_PROVIDER"
  case "$AW_PROVIDER" in
    cursor) EVENT=sessionStart ;;
    *) EVENT=SessionStart ;;
  esac
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    echo "  [dry-run] would install sindri-nudge ($EVENT) for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  elif [ "$AW_PROVIDER" = "claude" ]; then
    mkdir -p "$AW_HOOKS_INSTALL_DIR"
    cp "$SCRIPT_DIR/config/hooks/sindri-nudge.sh" "$AW_HOOKS_INSTALL_DIR/sindri-nudge.sh"
    chmod +x "$AW_HOOKS_INSTALL_DIR/sindri-nudge.sh"
    ENTRY=$(jq -nc --arg c "$AW_HOOKS_INSTALL_DIR/sindri-nudge.sh # aw:sindri-nudge" '{hooks:[{type:"command",command:$c}]}')
    merge_hook "$AW_HOOKS_CONFIG" SessionStart aw:sindri-nudge "$ENTRY"
    echo "  sindri: SessionStart nudge installed for claude in $AW_HOOKS_CONFIG"
  else
    aw_hooks_stage
    aw_hook_set "$EVENT" aw:sindri-nudge sindri-nudge.sh
    echo "  sindri: $EVENT nudge installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  fi
  exit 0
fi

# Single-quote a string for the shell, escaping embedded quotes.
shq() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"; }

# A wrapper is written to a temp file in the same directory and renamed into place: a symlink at
# the destination is replaced, never written through. A directory (or a symlink to one) at the
# destination is refused: mv would move the wrapper into it.
check_wrapper_target() { # name
  if [ -d "$BIN_DIR/$1" ]; then
    echo "refusing: $BIN_DIR/$1 is a directory" >&2
    exit 1
  fi
}
write_wrapper() { # name cli
  local target="$BIN_DIR/$1" tmp
  check_wrapper_target "$1"
  mkdir -p "$BIN_DIR"
  tmp="$(mktemp "$BIN_DIR/.$1.XXXXXX")"
  {
    echo '#!/usr/bin/env bash'
    echo "export SINDRI_BIN=$(shq "$target")"
    echo "exec $(shq "$(command -v node)") $(shq "$2") \"\$@\""
  } > "$tmp"
  chmod 755 "$tmp"
  mv -f "$tmp" "$target"
}

# channels.json is a plain object with "stable" and "next", each null or an entry with a sha and a dir.
# A missing file is a first install; anything else that doesn't fit is refused, never overwritten.
CHANNELS_JS='
const fs = require("node:fs");
const isEntry = (e) => e === null || (typeof e === "object" && !Array.isArray(e) && typeof e.sha === "string" && typeof e.dir === "string");
function readChannels(file) {
  let c;
  try {
    c = JSON.parse(fs.readFileSync(file, "utf8"));
  } catch (e) {
    if (e.code === "ENOENT") return { stable: null, next: null };
    fail("channels.json is unreadable: " + e.message);
  }
  if (c === null || typeof c !== "object" || Array.isArray(c) || !("stable" in c) || !("next" in c) || !isEntry(c.stable) || !isEntry(c.next)) {
    fail("channels.json is unreadable: it is not an object with stable and next entries");
  }
  return c;
}
function fail(why) {
  console.error(why + "; repair or restore channels.json (a copy of the last good one, or fix the entry by hand) and run the install again");
  process.exit(1);
}
'

# schema is the ledger schema version the build supports (its `--schema-version`); empty when the build
# can't say, and then `sindri channel rollback` refuses to roll back to it.
record_channel() { # state channel sha dest schema
  node -e "$CHANNELS_JS"'
const [file, channel, sha, dir, schema] = process.argv.slice(1);
const cur = readChannels(file);
const entry = { sha, dir, installedAt: new Date().toISOString(), ...(/^[1-9][0-9]*$/.test(schema) ? { schema: Number(schema) } : {}) };
const plain = (e) => ({ sha: e.sha, dir: e.dir, installedAt: e.installedAt, ...(e.schema === undefined ? {} : { schema: e.schema }) });
if (channel === "stable") {
  cur.stable = { ...entry, previous: cur.stable ? plain(cur.stable) : null };
} else {
  cur.next = entry;
}
const tmp = file + ".tmp-" + process.pid;
fs.writeFileSync(tmp, JSON.stringify(cur, null, 2), { mode: 0o600 });
fs.renameSync(tmp, file);' "$1/channels.json" "$2" "$3" "$4" "$5"
}

# Prints "yes" or "no": does channels.json already name a stable build? A bad file exits 1.
has_stable() { # state
  node -e "$CHANNELS_JS"'process.stdout.write(readChannels(process.argv[1]).stable ? "yes" : "no");' "$1/channels.json"
}

# Only merged code runs on a channel: the ref must be an ancestor of refs/remotes/origin/<default
# branch>, named in full so a local tag or branch called origin/<branch> can't stand in for it. This
# guards against mistakes and agents, not against someone who can rewrite refs in the source repo.
DEST_CLEANUP=""
cleanup_dest() { if [ -n "$DEST_CLEANUP" ]; then rm -rf "$DEST_CLEANUP"; fi; }

install_channel() {
  local state="${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri" default_branch sha dest wrapper remote_ref remote_tip stable_state
  default_branch="$(git -C "$SRC_REPO" symbolic-ref --short refs/remotes/origin/HEAD 2>/dev/null | sed 's|^origin/||' || true)"
  default_branch="${default_branch:-main}"
  case "$default_branch" in
    *[!A-Za-z0-9._/-]*|-*) echo "refusing: the default branch name '$default_branch' has unexpected characters" >&2; exit 1 ;;
  esac
  remote_ref="refs/remotes/origin/$default_branch"
  # show-ref --verify matches the exact refname only (rev-parse would fall back to a tag or branch of that name).
  remote_tip="$(git -C "$SRC_REPO" show-ref --verify --hash "$remote_ref" 2>/dev/null || true)"
  [ -n "$remote_tip" ] || { echo "refusing: no $remote_ref in $SRC_REPO (run git fetch origin first)" >&2; exit 1; }
  sha="$(git -C "$SRC_REPO" rev-parse --verify --end-of-options "${REF:-HEAD}^{commit}")" || { echo "refusing: ${REF:-HEAD} is not a commit in $SRC_REPO" >&2; exit 1; }
  if ! git -C "$SRC_REPO" merge-base --is-ancestor "$sha" "$remote_tip" 2>/dev/null; then
    echo "refusing: $sha is not an ancestor of $remote_ref (only merged code runs on a channel)" >&2
    exit 1
  fi
  dest="$state/channels/$CHANNEL/$sha"
  wrapper="sindri"
  [ "$CHANNEL" = "next" ] && wrapper="sindri-next"
  # Check the state file before anything is built (a bad one refuses here, not after a full build).
  stable_state="$(has_stable "$state")" || exit 1
  if [ "$CHANNEL" = "stable" ] && [ "$stable_state" = "yes" ]; then
    echo "refusing: a stable build already exists; change stable with: sindri channel promote <sha> (it checks the soak and the suite, and asks you to confirm)" >&2
    exit 1
  fi
  if [ -e "$dest" ]; then
    echo "refusing: $dest already exists (channel builds are immutable)" >&2
    exit 1
  fi
  check_wrapper_target "$wrapper"
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    echo "  [dry-run] would build sindri at $sha into $dest"
    echo "  [dry-run] would write $BIN_DIR/$wrapper"
    return
  fi
  mkdir -p -m 700 "$state"
  chmod 700 "$state"
  # A failed build removes its partial directory, so the same sha can be retried.
  DEST_CLEANUP="$dest"
  trap cleanup_dest EXIT
  mkdir -p "$dest"
  git -C "$SRC_REPO" archive "$sha" sindri | tar -x -C "$dest" --strip-components=1 --no-same-owner
  if [ -n "$(find "$dest" -type l)" ]; then
    echo "refusing: the archive at $sha contains symlinks" >&2
    exit 1
  fi
  if [ "${AW_SKIP_BUILD:-0}" != "1" ]; then
    # Install scripts from the ref don't run; only better-sqlite3's native build does.
    (cd "$dest" && npm ci --ignore-scripts && npm rebuild better-sqlite3 && npm run build)
  fi
  # Record first: if the state can't be written, the old wrapper stays and the new build is removed.
  record_channel "$state" "$CHANNEL" "$sha" "$dest" "$(node "$dest/dist/cli.js" --schema-version 2>/dev/null || true)"
  DEST_CLEANUP=""
  write_wrapper "$wrapper" "$dest/dist/cli.js"
  echo "  sindri: $CHANNEL channel at $sha ($BIN_DIR/$wrapper)"
}

if [ -n "$CHANNEL" ]; then
  install_channel
  exit 0
fi

echo ""
echo "Installing sindri..."

if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would run npm ci && npm run build in $SINDRI_DIR"
  echo "  [dry-run] would write $BIN_DIR/sindri"
  for PLIST_FILE in com.agentic-workflow.sindri-observe.plist com.agentic-workflow.sindri-index-quick.plist com.agentic-workflow.sindri-index.plist com.agentic-workflow.sindri-evolve.plist; do
    echo "  [dry-run] would install launchd job $PLIST_FILE (macOS)"
  done
  exit 0
fi

USE_LAUNCHD=0
if [ "$(uname -s)" = "Darwin" ] && [ "${AW_SKIP_LAUNCHD:-0}" != "1" ]; then USE_LAUNCHD=1; fi
if [ "$USE_LAUNCHD" = "1" ]; then
  # The substituted values land in a sed script and an XML plist; refuse characters that break either.
  case "$HOME$BIN_DIR" in
    *[\|\&\\\<\>]*) echo "ERROR: \$HOME and the bin dir must not contain | & \\ < > (got HOME=$HOME, bin=$BIN_DIR)" >&2; exit 2 ;;
  esac
fi

if [ "${AW_SKIP_BUILD:-0}" != "1" ]; then
  (cd "$SINDRI_DIR" && npm ci && npm run build)
fi
# Absolute node path: launchd and GUI git clients don't load shell profiles (nvm, Homebrew).
NODE_BIN="$(command -v node)"
mkdir -p "$BIN_DIR"
cat > "$BIN_DIR/sindri" <<EOF
#!/usr/bin/env bash
# SINDRI_BIN lets \`sindri scrub --install-pre-commit\` write this absolute path into the hook.
export SINDRI_BIN="$BIN_DIR/sindri"
exec "$NODE_BIN" "$SINDRI_DIR/dist/cli.js" "\$@"
EOF
chmod +x "$BIN_DIR/sindri"
echo "  sindri: CLI at $BIN_DIR/sindri"
# launchd jobs (macOS only; AW_SKIP_LAUNCHD=1 skips them, which the tests use): the hourly observe
# keeps the ledger's plan-task state current, the hourly quick index build keeps structure, clones and
# deps fresh, and the nightly full build refreshes everything. Each plist sets PATH, because launchd's
# default has neither ~/.local/bin (uv tools) nor Homebrew.
if [ "$USE_LAUNCHD" = "1" ]; then
  LAUNCH_AGENTS_DIR="$HOME/Library/LaunchAgents"
  # launchd does not pass AW_STATE_DIR, so the jobs and their logs use the default state dir.
  SINDRI_STATE="$HOME/.agentic-workflow/sindri"
  mkdir -p "$LAUNCH_AGENTS_DIR"
  mkdir -p -m 700 "$SINDRI_STATE"
  chmod 700 "$SINDRI_STATE"
  for JOB in "com.agentic-workflow.sindri-observe|hourly observe" \
             "com.agentic-workflow.sindri-index-quick|hourly quick index build" \
             "com.agentic-workflow.sindri-index|nightly full index build at 03:15" \
             "com.agentic-workflow.sindri-evolve|weekly evolve (Mondays 07:30)"; do
    NAME="${JOB%%|*}"
    WHAT="${JOB#*|}"
    PLIST="$LAUNCH_AGENTS_DIR/$NAME.plist"
    sed -e "s|__HOME__|$HOME|g" -e "s|__BIN__|$BIN_DIR|g" "$SCRIPT_DIR/config/launchd/$NAME.plist" > "$PLIST"
    launchctl bootout "gui/$(id -u)/$NAME" 2>/dev/null || true
    if launchctl bootstrap "gui/$(id -u)" "$PLIST" 2>/dev/null; then
      LOADED=1
    else
      sleep 1
      if launchctl bootstrap "gui/$(id -u)" "$PLIST"; then
        LOADED=1
      else
        LOADED=0
        echo "  WARN: could not load $NAME; run: launchctl bootstrap gui/$(id -u) $PLIST"
      fi
    fi
    if [ "$LOADED" = "1" ]; then echo "  sindri: $WHAT (launchd $NAME)"; fi
  done
fi
case ":$PATH:" in *":$BIN_DIR:"*) ;; *) echo "  WARN: $BIN_DIR is not on PATH" ;; esac
echo "  sindri: onboard a repo with \`sindri repo onboard <path>\`; to give new clones the (inactive until onboarded) pre-commit hook: sindri repo onboard --template"
echo "  sindri: the SessionStart nudge is per provider: scripts/install-sindri.sh --hook-only --provider claude|codex|cursor (setup.sh --with-sindri does this)"
