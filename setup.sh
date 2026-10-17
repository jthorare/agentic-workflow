#!/usr/bin/env bash
# Agentic Workflow — one-command setup for Claude Code, Codex, and Cursor.
#
# Usage:
#   ./setup.sh [--providers claude,codex,cursor] [--dry-run]
#   ./setup.sh --install-agents [--providers ...] [--dry-run]
#   ./setup.sh [--providers ...] --with-sindri   # also build and install the sindri CLI (opt-in)
#   ./setup.sh [--providers ...] --with-meta     # also install the /meta harness: aw-meta CLI, hooks, launcher (opt-in)
#   ./setup.sh --profile <web-app|ios|personal> --target <dir> [--dry-run]
#
# --providers defaults to every provider whose CLI is installed
# (`claude`, `codex`, `cursor-agent`/`cursor`). Shared steps (scorer/judge,
# Serena image, external pack clone, rtk, headroom) run once;
# per-provider steps live in providers/<name>/install.sh.
#
# State stays under ~/.agentic-workflow (kept for compatibility):
#   ~/.agentic-workflow/toolkit    → this repo (skills read _shared via it)
#   ~/.agentic-workflow/providers  → "<name> <skills-dir>" per installed provider
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TOOLKIT_DIR="$SCRIPT_DIR"
CLAUDE_DIR="$HOME/.claude"

ALL_PROVIDERS="claude codex cursor"

usage() {
  sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'
}

# System packages are installed when missing (config/lib/sys-pkg.sh: Homebrew on macOS, sudo + the distro's package
# manager on Linux). AW_NO_SYSTEM_INSTALL=1 prints the commands instead; --dry-run runs nothing.
# shellcheck source=config/lib/sys-pkg.sh
source "$SCRIPT_DIR/config/lib/sys-pkg.sh"
case " $* " in *" --dry-run "*) _aw_pre_dry=1 ;; *) _aw_pre_dry=0 ;; esac
AW_DRY_RUN="$_aw_pre_dry" aw_ensure_brew || { echo "FATAL: Homebrew is required on macOS (https://brew.sh)."; exit 1; }

# jq: required by the installer (settings/MCP config merges) and by the statusline
if ! command -v jq &>/dev/null; then
  echo "Installing jq (required by the installer and the statusline)..."
  AW_DRY_RUN="$_aw_pre_dry" aw_sys_install jq || { echo "FATAL: jq could not be installed (see above)."; exit 1; }
  if [ "$_aw_pre_dry" = "0" ] && ! command -v jq &>/dev/null; then echo "FATAL: jq is still missing after the install."; exit 1; fi
  if [ "$_aw_pre_dry" = "1" ] && ! command -v jq &>/dev/null; then echo "  (dry run: jq is needed to go further)"; exit 0; fi
fi

# --- --profile <name> [--target <dir>] [--dry-run] ---
# Applies a repo profile's skillOverrides via the native settings.local.json
# mechanism (see config/lib/apply-profile.sh). Claude Code only — skillOverrides
# is a Claude settings key. Gated on config/lib/diff-skill-pairs.sh for the 14
# camelCase/kebab-case skill pairs — refuses to write if any of the 11
# assumed-safe case-fold pairs unexpectedly DIFFERS.
if [ "${1:-}" = "--profile" ]; then
  PROFILE_NAME="${2:-}"
  TARGET_DIR=""
  DRY_RUN=0
  shift 2 || true
  while [ $# -gt 0 ]; do
    case "$1" in
      --target) TARGET_DIR="$2"; shift 2 ;;
      --dry-run) DRY_RUN=1; shift ;;
      *) shift ;;
    esac
  done

  if [ -z "$PROFILE_NAME" ] || [ -z "$TARGET_DIR" ]; then
    echo "usage: setup.sh --profile <web-app|ios|personal> --target <dir> [--dry-run]"
    exit 1
  fi

  PROFILE_FILE="$SCRIPT_DIR/config/profiles/$PROFILE_NAME.json"
  if [ ! -f "$PROFILE_FILE" ]; then
    echo "no such profile: $PROFILE_NAME (looked for $PROFILE_FILE)"
    exit 1
  fi

  source "$SCRIPT_DIR/scripts/find-duplicate-skills.sh"
  source "$SCRIPT_DIR/config/lib/diff-skill-pairs.sh"
  source "$SCRIPT_DIR/config/lib/apply-profile.sh"

  AW_SKILLS_DIR="$HOME/.claude/skills"
  WA_SKILLS_DIR="$TARGET_DIR/.claude/skills"

  echo "=== diff-skill-pairs: checking the 14 known camelCase/kebab-case skill pairs ==="
  if [ -d "$AW_SKILLS_DIR" ] && [ -d "$WA_SKILLS_DIR" ]; then
    PAIRS_FILE="$(mktemp)"
    AW_LIST="$(mktemp)"; WA_LIST="$(mktemp)"
    ls "$AW_SKILLS_DIR" > "$AW_LIST" 2>/dev/null || true
    ls "$WA_SKILLS_DIR" > "$WA_LIST" 2>/dev/null || true
    find_duplicate_skills "$AW_LIST" "$WA_LIST" > "$PAIRS_FILE" || true
    EXACT_NAME_PAIRS="autoplan cso verify-web"
    diff_skill_pairs "$AW_SKILLS_DIR" "$WA_SKILLS_DIR" "$PAIRS_FILE" | while IFS=$'\t' read -r verdict a b; do
      echo "  $verdict	$a	$b"
      if skill_pair_refuse "$verdict" "$a" "$EXACT_NAME_PAIRS"; then
        echo "REFUSE	$verdict	$a" >> "$PAIRS_FILE.refuse"
      fi
    done
    if [ -f "$PAIRS_FILE.refuse" ]; then
      echo "REFUSING: a case-fold pair's kebab-case copy is MISSING — turning off the camelCase copy would remove the skill entirely. Resolve by hand before re-running."
      cat "$PAIRS_FILE.refuse"
      rm -f "$PAIRS_FILE" "$AW_LIST" "$WA_LIST" "$PAIRS_FILE.refuse"
      exit 1
    fi
    rm -f "$PAIRS_FILE" "$AW_LIST" "$WA_LIST"
  else
    echo "  (skipping: skills dirs not found at $AW_SKILLS_DIR or $WA_SKILLS_DIR)"
  fi

  REPO_SLUG="$(basename "$(dirname "$TARGET_DIR")")-$(basename "$TARGET_DIR")"
  MANIFEST_DIR="$HOME/.agentic-workflow/managed/profiles"
  MANIFEST_FILE="$MANIFEST_DIR/$REPO_SLUG.json"
  SETTINGS_FILE="$TARGET_DIR/.claude/settings.local.json"

  if [ "$DRY_RUN" -eq 1 ]; then
    echo "=== --dry-run: would apply $PROFILE_FILE to $SETTINGS_FILE (manifest: $MANIFEST_FILE) ==="
    TMP_SETTINGS="$(mktemp)"
    if [ -f "$SETTINGS_FILE" ]; then cp "$SETTINGS_FILE" "$TMP_SETTINGS"; else echo '{}' > "$TMP_SETTINGS"; fi
    TMP_MANIFEST="$(mktemp)"
    if [ -f "$MANIFEST_FILE" ]; then cp "$MANIFEST_FILE" "$TMP_MANIFEST"; else echo '{}' > "$TMP_MANIFEST"; fi
    apply_profile "$TMP_SETTINGS" "$PROFILE_FILE" "$TMP_MANIFEST"
    diff <(cat "$SETTINGS_FILE" 2>/dev/null || echo '{}') "$TMP_SETTINGS" || true
    rm -f "$TMP_SETTINGS" "$TMP_MANIFEST"
    exit 0
  fi

  mkdir -p "$MANIFEST_DIR" "$(dirname "$SETTINGS_FILE")"
  apply_profile "$SETTINGS_FILE" "$PROFILE_FILE" "$MANIFEST_FILE"
  echo "applied $PROFILE_NAME profile to $SETTINGS_FILE"
  echo "=== resolved skillOverrides (user > shared-project > local) ==="
  jq -s '.[0] * .[1] * .[2]' "$HOME/.claude/settings.json" "$TARGET_DIR/.claude/settings.json" "$SETTINGS_FILE" 2>/dev/null | jq '.skillOverrides' || true
  exit 0
fi

# --- General flags ---
MODE="install"
PROVIDERS_ARG=""
AW_DRY_RUN=0
WITH_SINDRI=0
WITH_META=0
while [ $# -gt 0 ]; do
  case "$1" in
    --providers) PROVIDERS_ARG="${2:-}"; shift 2 || { echo "--providers needs a value"; exit 1; } ;;
    --providers=*) PROVIDERS_ARG="${1#--providers=}"; shift ;;
    --install-agents) MODE="agents"; shift ;;
    --dry-run) AW_DRY_RUN=1; shift ;;
    --with-sindri) WITH_SINDRI=1; shift ;;
    --with-meta) WITH_META=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown option: $1"; echo ""; usage; exit 1 ;;
  esac
done
export AW_DRY_RUN TOOLKIT_DIR

# shellcheck source=providers/lib.sh
source "$SCRIPT_DIR/providers/lib.sh"
# shellcheck source=config/lib/install-agents.sh
source "$SCRIPT_DIR/config/lib/install-agents.sh"
for _p in $ALL_PROVIDERS; do
  # shellcheck disable=SC1090
  source "$SCRIPT_DIR/providers/$_p/install.sh"
done

# Resolve the provider list: explicit --providers, else detect installed CLIs.
PROVIDERS=""
if [ -n "$PROVIDERS_ARG" ]; then
  for _p in $(printf '%s' "$PROVIDERS_ARG" | tr ',' ' '); do
    case " $ALL_PROVIDERS " in
      *" $_p "*) PROVIDERS="$PROVIDERS $_p" ;;
      *) echo "unknown provider: $_p (expected: $(echo $ALL_PROVIDERS | tr ' ' ','))"; exit 1 ;;
    esac
  done
else
  for _p in $ALL_PROVIDERS; do
    if "${_p}_detect"; then PROVIDERS="$PROVIDERS $_p"; fi
  done
fi
PROVIDERS="$(echo $PROVIDERS)"
if [ -z "$PROVIDERS" ]; then
  echo "No supported agent CLI found (claude, codex, cursor-agent). Install one, or pass --providers."
  exit 1
fi
for _p in $PROVIDERS; do
  "${_p}_detect" || echo "WARN: --providers includes $_p but its CLI is not on PATH; installing files anyway"
done
export AW_PROVIDERS="$PROVIDERS"

# --- --install-agents ---
# Installs config/agents/*.md (lean pinned-model agent types) for each provider:
#   claude → ~/.claude/agents/*.md (verbatim)
#   codex  → ~/.codex/agents/*.toml (translated)
#   cursor → ~/.cursor/agents/*.md (translated frontmatter)
# Ownership tracked in ~/.agentic-workflow/managed/agents[-<provider>].json.
if [ "$MODE" = "agents" ]; then
  echo "=== Agentic Workflow: installing lean agent types ($PROVIDERS) ==="
  for _p in $PROVIDERS; do
    "${_p}_install_agents"
  done
  exit 0
fi

# Native build tools (required by better-sqlite3 when no prebuilt binary exists). On macOS Homebrew brought the
# Command Line Tools; on Linux build-essential (or the distro's equivalent) is installed.
if ! command -v make &>/dev/null || ! command -v g++ &>/dev/null; then
  if [ "$(uname)" = "Darwin" ]; then
    if aw_dry; then echo "  [dry-run] would run: xcode-select --install"; else
      echo "FATAL: make/g++ are missing: the Xcode Command Line Tools are not installed. Run: xcode-select --install"; exit 1
    fi
  else
    echo "Installing build tools (make, g++) for native Node addons..."
    aw_sys_install build-tools || { echo "FATAL: build tools could not be installed (see above)."; exit 1; }
    if ! aw_dry && { ! command -v make &>/dev/null || ! command -v g++ &>/dev/null; }; then
      echo "FATAL: make/g++ still missing after the install."; exit 1
    fi
  fi
fi

# Canonical list of skills managed by this toolkit.
# Note: skills/_shared/ is intentionally excluded from MANAGED_SKILLS. It is not
# linked into any provider's skills dir — skills read it through the stable path
# $HOME/.agentic-workflow/toolkit/skills/_shared (toolkit symlink created below).
MANAGED_SKILLS=(review postReview addressReview enhancePrompt rootCause bugHunt bugReport shipRelease syncDocs weeklyRetro officeHours productReview archReview withInterview design-analyze design-analyze-web design-analyze-ios design-language design-evolve design-evolve-web design-evolve-ios design-mockup design-mockup-web design-mockup-ios design-implement design-implement-web design-implement-ios design-refine design-verify design-verify-web design-verify-ios verify-app verify-web verify-ios autoplan planDesignReview planDevexReview cso design-shotgun landAndDeploy canary prismStatus specToProvenPR testAudit judge ui-evidence bugFixOrchestrator reflect correct new-package new-slice meta)

# Stale standalone copies of impeccable skills from an older setup.sh that
# copied (cp -r) individual skills. Canonical pbakaus/impeccable v3.1.1+ is a
# single umbrella skill, fetched via the External Skill Packs section.
# Removed from every provider skills dir on each run (idempotent).
DEPRECATED_SKILLS=(bolder critique audit polish animate distill colorize typeset arrange quieter harden onboard delight clarify normalize extract adapt optimize overdrive teach-impeccable)

echo "=== Agentic Workflow Setup ==="
echo "  providers: $PROVIDERS"
aw_dry && echo "  mode:      DRY RUN — nothing will be written"
echo ""

# Also clean up the old impeccable-cache directory from the previous setup.sh era
if [ -d "$HOME/.claude/impeccable-cache" ]; then
  aw_run rm -rf "$HOME/.claude/impeccable-cache"
  echo "  removed legacy impeccable-cache directory"
fi

# --- Stable paths ---
echo "Creating stable paths..."
aw_run mkdir -p "$AW_STATE_ROOT"
echo "  $AW_STATE_ROOT/: output + state directory"
aw_link_toolkit

# --- Scorer + judge (shared CLIs) ---
if aw_dry; then
  echo "  [dry-run] would run scripts/install-scorer.sh and scripts/install-judge.sh --build-only"
else
  "$SCRIPT_DIR/scripts/install-scorer.sh"
  bash "$SCRIPT_DIR/scripts/install-judge.sh" --build-only
fi

# --- Sindri (opt-in: ./setup.sh --with-sindri) ---
if [ "$WITH_SINDRI" = "1" ]; then
  if aw_dry; then
    echo "  [dry-run] would run scripts/install-sindri.sh"
  else
    bash "$SCRIPT_DIR/scripts/install-sindri.sh"
  fi
  # uv installs graphify (the code index graph layer) at its pin: sindri index setup
  if aw_dry; then
    echo "  [dry-run] would install uv (brew) if missing"
  elif command -v uv &>/dev/null; then
    echo "  uv: already installed ($(uv --version 2>/dev/null || echo 'unknown version'))"
  elif command -v brew &>/dev/null; then
    echo "Installing uv (required for the sindri index graph layer)..."
    brew install uv || echo "  WARN: uv install failed; the sindri index graph layer stays unavailable. Install uv (https://docs.astral.sh/uv/getting-started/installation/), then run: sindri index setup"
  else
    echo "WARN: 'uv' not found and Homebrew is not available; the sindri index graph layer stays unavailable."
    echo "      Install uv, then run: sindri index setup"
    echo "        https://docs.astral.sh/uv/getting-started/installation/"
  fi
fi

# --- External tools: uv, graphify (pinned), Temporal binaries, system packages ---
# User-level installs happen here; missing system packages are installed through config/lib/sys-pkg.sh (sudo on Linux).
echo ""
bash "$SCRIPT_DIR/scripts/install-deps.sh" || { echo "FATAL: external tools missing (see above). Fix, then re-run setup.sh."; exit 1; }


# --- Skill packages (TypeScript helpers run by SKILL.md) ---
echo ""
echo "Building skill packages..."
aw_build_skill_packages "$SCRIPT_DIR"

# --- Serena MCP (Docker image + wrapper) ---
echo ""
echo "=== Serena prerequisites ==="
if ! command -v docker &>/dev/null; then
  echo "Installing Docker (Serena runs in a container)..."
  aw_sys_install docker || { echo "FATAL: Docker could not be installed (see above)."; exit 1; }
  if ! aw_dry; then
    # Homebrew links the CLI from the app bundle; make sure this shell sees it
    [ -x /Applications/Docker.app/Contents/Resources/bin/docker ] && export PATH="$PATH:/Applications/Docker.app/Contents/Resources/bin"
    command -v docker &>/dev/null || { echo "FATAL: docker is still missing after the install."; exit 1; }
  fi
fi
# The daemon must be up: start it when it isn't (Docker Desktop on macOS, the systemd service on Linux).
if ! aw_dry && command -v docker &>/dev/null && ! docker info &>/dev/null; then
  if [ "$(uname)" = "Darwin" ]; then
    echo "Starting Docker Desktop (the first start asks you to accept its terms)..."
    open -a Docker 2>/dev/null || open -a "Docker Desktop" 2>/dev/null || true
  else
    echo "Starting the Docker service and adding $USER to the docker group..."
    aw_sys_root systemctl enable --now docker || true
    id -nG "$USER" 2>/dev/null | tr ' ' '\n' | grep -qx docker || aw_sys_root usermod -aG docker "$USER" || true
  fi
  for _i in $(seq 1 60); do docker info &>/dev/null && break; sleep 2; done
  if ! docker info &>/dev/null; then
    if [ "$(uname)" != "Darwin" ] && aw_sys_root docker info &>/dev/null; then
      echo "FATAL: Docker is running, but this shell is not in the docker group yet. Log out and back in (or run: newgrp docker), then re-run setup.sh."
    else
      echo "FATAL: the Docker daemon did not come up within 2 minutes. Start Docker, then re-run setup.sh."
    fi
    exit 1
  fi
fi

# Derive version from committed wrapper — single source of truth, no dual-maintenance
SERENA_VERSION=$(grep '^BASE_VERSION=' "$SCRIPT_DIR/scripts/serena-docker" \
  | sed 's/BASE_VERSION="//;s/".*//')
if [ -z "$SERENA_VERSION" ]; then
  echo "FATAL: Could not parse BASE_VERSION from scripts/serena-docker"; exit 1
fi

if aw_dry; then
  echo "  [dry-run] would build serena-local:${SERENA_VERSION} if missing (+ -csharp / -swift variants when detected)"
  echo "  [dry-run] would install scripts/serena-docker → $HOME/.local/bin/serena-docker"
else
  echo "=== Building Serena base image (TS + Python) ==="
  if ! docker image inspect "serena-local:${SERENA_VERSION}" &>/dev/null; then
    echo "Building serena-local:${SERENA_VERSION} (~5 min)..."
    docker build \
      --pull \
      --progress plain \
      --build-arg BASE_TAG="${SERENA_VERSION}" \
      -t "serena-local:${SERENA_VERSION}" \
      -f "$SCRIPT_DIR/Dockerfile.serena" \
      "$SCRIPT_DIR" \
      || { echo "FATAL: Base image build failed."; exit 1; }
    echo "Built serena-local:${SERENA_VERSION}"
  else
    echo "serena-local:${SERENA_VERSION} already exists, skipping"
  fi

  echo "=== Building Serena C# extension image (opt-in) ==="
  # Auto-detect C# projects or honour BUILD_CSHARP=1 env var override
  _build_csharp=0
  if [ "${BUILD_CSHARP:-0}" = "1" ]; then
    _build_csharp=1
  elif find "$SCRIPT_DIR" -maxdepth 3 \( -name "*.csproj" -o -name "*.cs" \) -print -quit 2>/dev/null | grep -q .; then
    _build_csharp=1
  fi

  if [ "$_build_csharp" = "1" ]; then
    if ! docker image inspect "serena-local:${SERENA_VERSION}-csharp" &>/dev/null; then
      echo "Building serena-local:${SERENA_VERSION}-csharp (~15 min — .NET SDK download)..."
      # No --pull here: this image's FROM is the local serena-local:${SERENA_VERSION}
      # (not a registry image), so --pull would force Docker to resolve it against
      # docker.io and fail with "pull access denied". The base build above uses --pull
      # because its FROM is the remote ghcr.io/oraios/serena. (Matches the Swift build.)
      docker build \
        --progress plain \
        --build-arg LOCAL_TAG="${SERENA_VERSION}" \
        -t "serena-local:${SERENA_VERSION}-csharp" \
        -f "$SCRIPT_DIR/Dockerfile.serena-csharp" \
        "$SCRIPT_DIR" \
        || { echo "FATAL: C# image build failed."; exit 1; }
      echo "Built serena-local:${SERENA_VERSION}-csharp"
    else
      echo "serena-local:${SERENA_VERSION}-csharp already exists, skipping"
    fi
  else
    echo "=== Skipping C# Serena image (no .csproj/.cs found) ==="
    echo "To build later, run: BUILD_CSHARP=1 ./setup.sh"
  fi

  echo "=== Building Serena Swift extension image (opt-in) ==="
  # Auto-detect Swift projects or honour BUILD_SWIFT=1 env var override
  # The Swift image adds socat + a sourcekit-lsp shim; sourcekit-lsp itself runs on the host.
  _build_swift=0
  if [ "${BUILD_SWIFT:-0}" = "1" ]; then
    _build_swift=1
  elif find "$SCRIPT_DIR" -maxdepth 4 -name "*.swift" -print -quit 2>/dev/null | grep -q .; then
    _build_swift=1
  fi

  if [ "$_build_swift" = "1" ]; then
    # Ensure socat is available on the host — required for the host-side LSP bridge process
    if ! command -v socat &>/dev/null; then
      echo "Installing socat (required for Swift LSP bridge)..."
      if ! aw_sys_install socat; then
        echo "WARN: socat could not be installed (see above); skipping Swift image build."
        _build_swift=0
      fi
    fi
    if [ "$_build_swift" = "1" ]; then
      if ! docker image inspect "serena-local:${SERENA_VERSION}-swift" &>/dev/null; then
        echo "Building serena-local:${SERENA_VERSION}-swift (socat + sourcekit-lsp shim)..."
        docker build \
          --progress plain \
          --build-arg LOCAL_TAG="${SERENA_VERSION}" \
          -t "serena-local:${SERENA_VERSION}-swift" \
          -f "$SCRIPT_DIR/Dockerfile.serena-swift" \
          "$SCRIPT_DIR" \
          || { echo "FATAL: Swift image build failed."; exit 1; }
        echo "Built serena-local:${SERENA_VERSION}-swift"
      else
        echo "serena-local:${SERENA_VERSION}-swift already exists, skipping"
      fi
    fi
  else
    echo "=== Skipping Swift Serena image (no *.swift found) ==="
    echo "To build later, run: BUILD_SWIFT=1 ./setup.sh"
  fi

  echo "=== Installing serena-docker wrapper ==="
  mkdir -p "$HOME/.local/bin"
  cp "$SCRIPT_DIR/scripts/serena-docker" "$HOME/.local/bin/serena-docker"
  chmod +x "$HOME/.local/bin/serena-docker"
fi

# Ensure ~/.local/bin is in PATH for the rest of this script and future shells
if ! echo "$PATH" | tr ':' '\n' | grep -qx "$HOME/.local/bin"; then
  export PATH="$HOME/.local/bin:$PATH"
  if aw_dry; then
    echo "  [dry-run] would add ~/.local/bin to PATH in ~/.bashrc / ~/.zshrc"
  else
    LOCAL_BIN_LINE='export PATH="$HOME/.local/bin:$PATH"'
    _added_local_bin=false
    for _rc in "$HOME/.bashrc" "$HOME/.zshrc"; do
      if [ -f "$_rc" ] && ! grep -qF '.local/bin' "$_rc" 2>/dev/null; then
        printf '\n# Added by Agentic Workflow setup.sh\n%s\n' "$LOCAL_BIN_LINE" >> "$_rc"
        _added_local_bin=true
      fi
    done
    if [ "$_added_local_bin" = true ]; then
      echo "  ~/.local/bin added to PATH (updated shell profile)"
    else
      echo "  ~/.local/bin added to PATH (this session only)"
    fi
  fi
fi

# --- Dembrandt CLI ---
echo ""
echo "Installing Dembrandt CLI..."
DEMBRANDT_VERSION="0.7.0"
if command -v dembrandt &>/dev/null; then
  echo "  dembrandt: already installed ($(dembrandt --version 2>/dev/null || echo 'unknown version'))"
elif aw_dry; then
  echo "  [dry-run] would run: npm install -g dembrandt@$DEMBRANDT_VERSION"
else
  npm install -g "dembrandt@$DEMBRANDT_VERSION" 2>&1 && \
    echo "  dembrandt: installed globally ($DEMBRANDT_VERSION)" || \
    echo "  dembrandt: failed to install (non-fatal, install manually: npm install -g dembrandt)"
fi

# --- External Skill Packs (clone once; linked per provider below) ---
echo ""
echo "=== Fetching external skill packs ==="

EXTERNAL_DIR="$AW_STATE_ROOT/external-skills"
aw_run mkdir -p "$EXTERNAL_DIR"
AW_EXTERNAL_SKILL_DIRS=""

fetch_external_pack() {
  local repo="$1"   # e.g. pbakaus/impeccable
  local pin="$2"    # commit SHA
  local target
  target="$EXTERNAL_DIR/$(basename "$repo")"

  if aw_dry; then
    echo "  [dry-run] would clone/fetch $repo into $target and check out ${pin}"
  else
    if [ ! -d "$target/.git" ]; then
      echo "  $repo: cloning..."
      git clone "https://github.com/$repo.git" "$target" 2>&1 \
        || { echo "  WARN: failed to clone $repo (non-fatal)"; return 0; }
    fi
    (cd "$target" && git fetch origin --quiet) || true
    if [ -n "$pin" ] && [ "$pin" != "HEAD" ]; then
      (cd "$target" && git checkout "$pin" --quiet) 2>/dev/null \
        || echo "  WARN: pin $pin not found in $repo, using current HEAD"
    fi
  fi

  # Find the skill source directory — tries both layouts
  local skills_dir=""
  if [ -d "$target/skills" ]; then
    skills_dir="$target/skills"
  elif [ -d "$target/.claude/skills" ]; then
    skills_dir="$target/.claude/skills"
  fi
  if [ -z "$skills_dir" ]; then
    aw_dry || echo "  WARN: no skills directory found in $(basename "$repo")"
    return 0
  fi

  local skill_dir
  for skill_dir in "$skills_dir"/*/; do
    [ -f "$skill_dir/SKILL.md" ] || continue
    AW_EXTERNAL_SKILL_DIRS="$AW_EXTERNAL_SKILL_DIRS${skill_dir%/}
"
  done
  echo "  $repo: ready ($(basename "$skills_dir") in $target)"
}

if [ -f "$SCRIPT_DIR/EXTERNAL_PINS.env" ]; then
  # Safe parser: read only well-formed KEY=value lines and validate SHA format.
  # Avoids `source`'ing the file as bash (which would evaluate arbitrary commands
  # smuggled in by a malicious commit to EXTERNAL_PINS.env).
  read_pin() {
    local key="$1"
    local value
    # grep -m1 stops after first match (avoids concatenation on duplicate keys);
    # cut -d= -f2- preserves everything after the first '=' so values containing '=' survive.
    value=$(grep -m1 "^${key}=" "$SCRIPT_DIR/EXTERNAL_PINS.env" 2>/dev/null | cut -d= -f2- | tr -d '[:space:]')
    if [[ "$value" =~ ^[0-9a-f]{7,40}$ ]] || [[ "$value" == "HEAD" ]]; then
      echo "$value"
    else
      echo "HEAD"
    fi
  }
  fetch_external_pack "pbakaus/impeccable"   "$(read_pin IMPECCABLE_PIN)"
  fetch_external_pack "emilkowalski/skill"   "$(read_pin EMIL_PIN)"
  fetch_external_pack "Leonxlnx/taste-skill" "$(read_pin TASTE_PIN)"
else
  echo "  WARN: EXTERNAL_PINS.env missing at $SCRIPT_DIR; skipping external pack install"
fi
export AW_EXTERNAL_SKILL_DIRS

# --- rtk ---
echo ""
echo "=== Installing rtk ==="
if command -v rtk &>/dev/null; then
  echo "  rtk: already installed ($(rtk --version 2>/dev/null || echo 'unknown version'))"
elif [ -x "$HOME/.local/bin/rtk" ]; then
  echo "  rtk: already installed at ~/.local/bin/rtk ($("$HOME/.local/bin/rtk" --version 2>/dev/null || echo 'unknown version'))"
elif aw_dry; then
  echo "  [dry-run] would install rtk (brew on macOS, install.sh elsewhere)"
else
  if [ "$(uname)" = "Darwin" ]; then
    { aw_ensure_brew && brew install rtk; } || { echo "FATAL: rtk installation failed."; exit 1; }
  else
    curl -fsSL https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh | sh \
      || { echo "FATAL: rtk installation failed."; exit 1; }
  fi
  # The installer may place rtk in ~/.local/bin which isn't necessarily in PATH
  if ! command -v rtk &>/dev/null && ! [ -x "$HOME/.local/bin/rtk" ]; then
    echo "FATAL: rtk not found after installation."; exit 1
  fi
  echo "  rtk: installed"
fi

# --- headroom ---
echo ""
echo "=== Installing headroom ==="

# headroom-ai requires Python >= 3.10; pick the newest one on PATH, including
# unversioned `python3` (e.g. a Homebrew or pyenv 3.14 install).
_aw_find_python() {
  HEADROOM_PYTHON=""
  local _best_minor=-1 _py _minor
  for _py in python3.14 python3.13 python3.12 python3.11 python3.10 python3 python; do
    command -v "$_py" &>/dev/null || continue
    _minor=$("$_py" -c "import sys; print(sys.version_info.minor if sys.version_info.major == 3 else -1)" 2>/dev/null)
    case "$_minor" in ''|*[!0-9-]*) continue ;; esac
    if [ "$_minor" -ge 10 ] && [ "$_minor" -gt "$_best_minor" ]; then
      HEADROOM_PYTHON="$_py"
      _best_minor="$_minor"
    fi
  done
}
_aw_find_python
if [ -z "$HEADROOM_PYTHON" ] && ! command -v headroom &>/dev/null; then
  echo "Installing Python 3.10+ (required for headroom-ai)..."
  aw_sys_install python3 pip || { echo "FATAL: Python could not be installed (see above)."; exit 1; }
  _aw_find_python
fi

HEADROOM_CMD=""
if command -v headroom &>/dev/null; then
  HEADROOM_CMD="$(command -v headroom)"
  echo "  headroom: already installed ($(headroom --version 2>/dev/null || echo 'unknown version'))"
elif [ -z "$HEADROOM_PYTHON" ]; then
  if aw_dry; then
    echo "  WARN: Python 3.10+ not found (a real run would stop here); headroom MCP skipped"
  else
    echo "FATAL: Python 3.10+ is required for headroom-ai, and the one the package manager installed is older."
    echo "  Install Python 3.10 or newer, then re-run setup.sh."
    exit 1
  fi
else
  # Derive user-install bin dir (e.g. ~/Library/Python/3.13/bin on macOS)
  HEADROOM_BIN="$("$HEADROOM_PYTHON" -m site --user-base 2>/dev/null)/bin/headroom"
  if [ -x "$HEADROOM_BIN" ]; then
    echo "  headroom: already installed at $HEADROOM_BIN ($("$HEADROOM_BIN" --version 2>/dev/null || echo 'unknown version'))"
  elif aw_dry; then
    echo "  [dry-run] would run: $HEADROOM_PYTHON -m pip install --user 'headroom-ai[all]'"
  else
    # On Debian/Ubuntu, pip may not be bundled — try ensurepip bootstrap first
    if ! "$HEADROOM_PYTHON" -m pip --version &>/dev/null; then
      "$HEADROOM_PYTHON" -m ensurepip --user 2>/dev/null \
        || { echo "Installing pip for $HEADROOM_PYTHON..."; aw_sys_install pip && "$HEADROOM_PYTHON" -m pip --version &>/dev/null; } \
        || { echo "FATAL: pip is not installed for $HEADROOM_PYTHON and could not be installed (see above)."; exit 1; }
    fi
    "$HEADROOM_PYTHON" -m pip install --break-system-packages --user "headroom-ai[all]" 2>/dev/null \
      || "$HEADROOM_PYTHON" -m pip install --user "headroom-ai[all]" \
      || { echo "FATAL: headroom installation failed."; exit 1; }
    [ -x "$HEADROOM_BIN" ] || { echo "FATAL: headroom binary not found after installation at $HEADROOM_BIN."; exit 1; }
    echo "  headroom: installed"
  fi
  # Use full path when headroom is not on PATH (common after --user install)
  HEADROOM_CMD="$HEADROOM_BIN"
fi

# --- MCP server catalog (registered with every selected provider) ---
PRISM_VERSION="20.21.17"       # pin: bump here when upgrading (5.1.0 hung on MCP initialize)
XCODEBUILDMCP_VERSION="2.3.0"  # pin: bump here when upgrading (keep config/mcp.json in sync)

AW_MCP_SERVERS="$(jq -nc \
  --arg serena "$HOME/.local/bin/serena-docker" \
  --arg headroom "$HEADROOM_CMD" \
  --arg prism "prism-mcp-server@$PRISM_VERSION" \
  --arg xcode "xcodebuildmcp@$XCODEBUILDMCP_VERSION" \
  --arg darwin "$([ "$(uname)" = "Darwin" ] && echo 1 || echo 0)" '
  [ {name: "serena", command: $serena, args: []},
    (if $headroom != "" then {name: "headroom", command: $headroom, args: ["mcp", "serve"]} else empty end),
    {name: "prism-mcp", command: "npx", args: ["-y", $prism], env: {PRISM_DASHBOARD_PORT: "7180"}},
    (if $darwin == "1" then {name: "xcodebuildmcp", command: "npx", args: ["-y", $xcode, "mcp"]} else empty end)
  ]')"
export AW_MCP_SERVERS
# Servers the toolkit no longer ships: each provider removes them from an existing install (agentic-bridge, the MCP
# bridge, retired 2026-10-10: never used; the ledger and judge's outbox carry agent handoffs).
AW_MCP_RETIRED="agentic-bridge"
export AW_MCP_RETIRED
# The bridge package itself: after the pull only its ignored output (dist, node_modules, bridge.db) is left.
aw_remove_retired_dir "$SCRIPT_DIR" mcp-bridge

# --- Per-provider install ---
REGISTRY_ENTRIES=()
for _p in $PROVIDERS; do
  "${_p}_install"
  REGISTRY_ENTRIES+=("$_p $("${_p}_skills_dir")")
done

# --- Permission mode (owner, 2026-10-14): every Claude session starts in bypassPermissions, so lanes never run in
# different modes (cross-session messages are held across a mode mismatch). After the provider installs, so a fresh
# ~/.claude/settings.json exists; AW_NO_CLAUDE_BYPASS=1 opts out ---
if [[ " $PROVIDERS " == *" claude "* ]]; then
  # shellcheck source=config/lib/claude-bypass.sh
  source "$SCRIPT_DIR/config/lib/claude-bypass.sh"
  aw_claude_bypass "$CLAUDE_DIR/settings.json"
fi

# --- Meta harness (opt-in: ./setup.sh --with-meta; owner decision 2026-10-14). After install-deps (its NativeAOT publish
# needs clang and zlib) and after the provider install (its hooks merge into the settings that install copies from the
# template). Each selected provider gets its own hooks: Claude Code the meta gates, Codex and Cursor the lane hooks for
# non-Claude Meta lanes (the Codex /hooks trust step is printed). The binary is published once, by the first provider ---
if [ "$WITH_META" = "1" ]; then
  # shellcheck source=config/lib/setup-meta-step.sh
  source "$SCRIPT_DIR/config/lib/setup-meta-step.sh"
  if aw_dry; then
    for _p in $PROVIDERS; do echo "  [dry-run] would run scripts/install-meta.sh --provider $_p"; done
  else
    # install-meta exits 3 when it refuses the tree (a worktree or a tree that is not the team's repo): setup warns and goes on;
    # any other failure (an unsupported OS, a failed publish) stops setup with install-meta's message: there is no quiet skip, so a
    # reinstall never leaves an old binary in place without saying so
    aw_setup_meta_step "$SCRIPT_DIR" $PROVIDERS
  fi
fi

# --- Provider registry (read by the skill preamble) ---
echo ""
echo "Writing provider registry..."
aw_write_provider_registry "${REGISTRY_ENTRIES[@]}"

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Skills installed ($((${#MANAGED_SKILLS[@]} + 1)) native = ${#MANAGED_SKILLS[@]} managed + bootstrap):"
echo "  Review pipeline:  review, postReview, addressReview"
echo "  Investigation:    rootCause"
echo "  Bug fixing:       bugFixOrchestrator"
echo "  QA:               bugHunt, bugReport, testAudit"
echo "  Release:          shipRelease, landAndDeploy, canary, syncDocs"
echo "  Retrospective:    weeklyRetro"
echo "  Planning:         officeHours, productReview, archReview, withInterview,"
echo "                    autoplan, planDesignReview, planDevexReview, specToProvenPR"
echo "  Security:         cso"
echo "  Design:           design-analyze [web|ios], design-language, design-evolve [web|ios],"
echo "                    design-mockup [web|ios], design-shotgun, design-implement [web|ios],"
echo "                    design-refine, design-verify [web|ios]"
echo "  Verification:     verify-app, verify-web, verify-ios, ui-evidence"
echo "  Memory/Status:    prismStatus"
echo "  C# scaffolds:     new-package, new-slice"
echo "  Utilities:        enhancePrompt, judge, bootstrap"
echo ""
echo "Providers:          $PROVIDERS"
for _entry in "${REGISTRY_ENTRIES[@]}"; do
  printf '  %-17s %s\n' "${_entry%% *}:" "${_entry#* }"
done
echo "Toolkit path:       $AW_STATE_ROOT/toolkit → $TOOLKIT_DIR"
echo "Output directory:   ~/.agentic-workflow/<repo-slug>/"
echo "MCP registered:     $(printf '%s' "$AW_MCP_SERVERS" | jq -r 'map(.name) | join(", ")')"
case " $PROVIDERS " in *" claude "*)
  echo "Claude statusline:  $CLAUDE_DIR/statusline.sh"
  echo "Claude plugins:     github, superpowers, compound-engineering, playwright" ;;
esac
echo "Custom agents:      run ./setup.sh --install-agents to install lean agent types"
