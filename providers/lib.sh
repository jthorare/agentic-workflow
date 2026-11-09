#!/usr/bin/env bash
# Shared helpers for Agentic Workflow installer.
#
# Sourced by setup.sh and by providers/<name>/install.sh. Provider-neutral:
# everything here takes the target directory as an argument so the same logic
# (symlink install, collision detection, stale cleanup, external-pack linking)
# runs once per provider skills dir.
#
# Inputs (set by setup.sh before calling):
#   TOOLKIT_DIR               repo root (absolute)
#   MANAGED_SKILLS            bash array of native skill names (excl. bootstrap)
#   DEPRECATED_SKILLS         bash array of skill names to remove on sight
#   AW_EXTERNAL_SKILL_DIRS    newline-separated list of external-pack skill dirs
#   AW_DRY_RUN                1 = print what would change, touch nothing
#
# Compatible with macOS /bin/bash 3.2: no associative arrays, no mapfile, and
# empty-array expansions are guarded for `set -u`.

# One state root: AW_STATE_DIR (what the tools and hooks read). AW_STATE_ROOT stays as an alias for it.
AW_STATE_ROOT="${AW_STATE_DIR:-${AW_STATE_ROOT:-$HOME/.agentic-workflow}}"
export AW_STATE_DIR="$AW_STATE_ROOT"   # so the install scripts setup.sh runs see the same root

aw_dry() { [ "${AW_DRY_RUN:-0}" = "1" ]; }

# Run a command, or print it in dry-run mode.
aw_run() {
  if aw_dry; then
    printf '  [dry-run]'
    printf ' %q' "$@"
    printf '\n'
  else
    "$@"
  fi
}

# Portable canonical path resolver — works on macOS (Python fallback for pre-Big Sur,
# where BSD readlink lacks -f) and Linux (GNU readlink -f).
aw_canonicalize() {
  if command -v readlink &>/dev/null && readlink -f / &>/dev/null; then
    readlink -f "$1" 2>/dev/null || echo "$1"
  elif command -v python3 &>/dev/null; then
    python3 -c "import os, sys; print(os.path.realpath(sys.argv[1]))" "$1"
  else
    (cd "$(dirname "$1")" 2>/dev/null && echo "$(pwd)/$(basename "$1")")
  fi
}

# Ask a y/n question. Returns 0 on "y". Never blocks in dry-run, and treats a
# closed stdin (non-interactive run) as "n" instead of aborting under set -e.
aw_confirm() {
  local answer=""
  if aw_dry; then
    echo "    (dry-run: would prompt here; assuming n)"
    return 1
  fi
  if [ "${AW_ASSUME_YES:-0}" = "1" ]; then
    echo "    (AW_ASSUME_YES=1: answering y)"
    return 0
  fi
  read -r answer || answer="n"
  [ "$answer" = "y" ]
}

# True if a symlink target string points into this toolkit checkout (or
# another checkout of it).
aw_is_ours() {
  local link_target="$1"
  local toolkit_canon
  toolkit_canon="$(aw_canonicalize "$TOOLKIT_DIR")"
  case "$link_target" in
    "$TOOLKIT_DIR"/*|"$toolkit_canon"/*) return 0 ;;
    "$AW_STATE_ROOT/toolkit/"*) return 0 ;;
    */agentic-workflow/*|*/agentic-workflow-*) return 0 ;;
  esac
  return 1
}

# Install or refresh one skill symlink: aw_install_skill <name> <target-link> <source-dir>
aw_install_skill() {
  local skill="$1" target="$2" source="$3"

  if [ -L "$target" ]; then
    local current_target
    current_target=$(readlink "$target" 2>/dev/null || echo "")

    if [ "$current_target" = "$source" ]; then
      echo "  $skill: up to date"
    elif aw_is_ours "$current_target"; then
      # Symlink points to a previous toolkit install — refresh it
      aw_run rm "$target"
      aw_run ln -s "$source" "$target"
      echo "  $skill: refreshed (was: $current_target)"
    else
      echo ""
      echo "  ⚠ COLLISION: $skill"
      echo "    Existing symlink points to: $current_target"
      echo "    Our source is:              $source"
      echo "    This may be a different skill with the same name from another toolkit."
      echo "    Replace with ours? (y/n)"
      if aw_confirm; then
        aw_run rm "$target"
        aw_run ln -s "$source" "$target"
        echo "  $skill: replaced (original still exists at: $current_target)"
      else
        echo "  $skill: skipped (keeping existing)"
      fi
    fi
  elif [ -d "$target" ]; then
    echo ""
    echo "  ⚠ COLLISION: $skill"
    echo "    A non-symlinked directory exists at: $target"
    if [ -f "$target/SKILL.md" ]; then
      local existing_name
      existing_name=$(grep -m1 '^name:' "$target/SKILL.md" 2>/dev/null | sed 's/^name:[[:space:]]*//' || echo "")
      if [ "$existing_name" = "$skill" ]; then
        echo "    It contains a skill named '$existing_name' — this appears to match ours."
      else
        echo "    It contains a skill named '$existing_name' — this does NOT match our '$skill' skill."
        echo "    This is likely a DIFFERENT skill from another toolkit."
      fi
    else
      echo "    (no SKILL.md found — unknown origin)"
    fi
    echo "    Back up and replace with symlink? (y/n)"
    if aw_confirm; then
      aw_run mv "$target" "$target.bak.$(date +%s)"
      aw_run ln -s "$source" "$target"
      echo "  $skill: backed up and linked"
    else
      echo "  $skill: skipped (keeping existing directory)"
    fi
  elif [ -f "$target" ]; then
    echo "  $skill: WARNING — a file (not directory) exists at $target, skipping"
  else
    aw_run ln -s "$source" "$target"
    echo "  $skill: linked"
  fi
}

# Remove stale copies of skills that were retired (e.g. old impeccable forks).
aw_remove_deprecated_skills() {
  local dir="$1" s target
  for s in ${DEPRECATED_SKILLS[@]+"${DEPRECATED_SKILLS[@]}"}; do
    target="$dir/$s"
    if [ -L "$target" ]; then
      aw_run rm -f "$target"
      echo "  removed stale symlink: $s"
    elif [ -d "$target" ]; then
      aw_run rm -rf "$target"
      echo "  removed deprecated dir: $s"
    fi
  done
}

# Is <name> one of MANAGED_SKILLS or bootstrap?
aw_is_managed() {
  local name="$1" m
  [ "$name" = "bootstrap" ] && return 0
  for m in ${MANAGED_SKILLS[@]+"${MANAGED_SKILLS[@]}"}; do
    [ "$m" = "$name" ] && return 0
  done
  return 1
}

# Offer to remove symlinks into this toolkit that are no longer managed.
aw_cleanup_stale_skills() {
  local dir="$1" existing skill_name link_target
  for existing in "$dir"/*; do
    [ -L "$existing" ] || continue
    skill_name=$(basename "$existing")
    aw_is_managed "$skill_name" && continue
    link_target=$(readlink "$existing" 2>/dev/null || echo "")
    aw_is_ours "$link_target" || continue
    echo "  ⚠ STALE: $skill_name → $link_target"
    echo "    This skill was installed by a previous version of Agentic Workflow but is no longer in the current version."
    echo "    Remove it? (y/n)"
    if aw_confirm; then
      aw_run rm "$existing"
      echo "  $skill_name: removed"
    else
      echo "  $skill_name: kept"
    fi
  done
}

# Link every external-pack skill into <dir>. Native toolkit symlinks win on
# name collision.
aw_link_external_skills() {
  local dir="$1" skill_dir name link pack
  [ -n "${AW_EXTERNAL_SKILL_DIRS:-}" ] || return 0
  while IFS= read -r skill_dir; do
    [ -n "$skill_dir" ] || continue
    [ -f "$skill_dir/SKILL.md" ] || continue
    skill_dir="${skill_dir%/}"
    name="$(basename "$skill_dir")"
    pack="${skill_dir#*/external-skills/}"; pack="${pack%%/*}"
    link="$dir/$name"
    if [ -L "$link" ] && [ -e "$link" ] && aw_is_ours "$(aw_canonicalize "$link")"; then
      echo "  $name: skipping (native skill takes precedence)"
      continue
    fi
    if [ -L "$link" ] && [ "$(readlink "$link")" = "$skill_dir" ]; then
      echo "  $name: up to date ($pack)"
      continue
    fi
    if [ -d "$link" ] && [ ! -L "$link" ]; then
      local backup
      backup="$link.bak.$(date +%s)"
      aw_run mv "$link" "$backup"
      echo "  $name: backed up existing dir to $(basename "$backup") before symlinking"
    fi
    [ -L "$link" ] && aw_run rm -f "$link"
    aw_run ln -sfn "$skill_dir" "$link"
    echo "  $name: symlinked from $pack"
  done <<EOF
$AW_EXTERNAL_SKILL_DIRS
EOF
}

# Full skill install into one provider skills dir.
aw_install_skills_into() {
  local dir="$1" skill
  echo "Installing skills into $dir..."
  aw_run mkdir -p "$dir"
  aw_remove_deprecated_skills "$dir"
  for skill in ${MANAGED_SKILLS[@]+"${MANAGED_SKILLS[@]}"}; do
    aw_install_skill "$skill" "$dir/$skill" "$TOOLKIT_DIR/skills/$skill"
  done
  aw_install_skill "bootstrap" "$dir/bootstrap" "$TOOLKIT_DIR/bootstrap"
  echo "Checking for stale skills in $dir..."
  aw_cleanup_stale_skills "$dir"
  echo "Linking external skill packs into $dir..."
  aw_link_external_skills "$dir"
}

# $HOME/.agentic-workflow/toolkit -> repo root. Skills resolve shared fragments
# via $HOME/.agentic-workflow/toolkit/skills/_shared regardless of provider.
aw_link_toolkit() {
  local link="$AW_STATE_ROOT/toolkit"
  aw_run mkdir -p "$AW_STATE_ROOT"
  if [ -L "$link" ] && [ "$(readlink "$link")" = "$TOOLKIT_DIR" ]; then
    echo "  toolkit: $link → $TOOLKIT_DIR (up to date)"
    return 0
  fi
  if [ -e "$link" ] && [ ! -L "$link" ]; then
    echo "  WARN: $link exists and is not a symlink — moving it aside"
    aw_run mv "$link" "$link.bak.$(date +%s)"
  fi
  aw_run ln -sfn "$TOOLKIT_DIR" "$link"
  echo "  toolkit: $link → $TOOLKIT_DIR"
}

# Write <src>'s content into <file> between "<!-- aw:<tag>:start -->" and "<!-- aw:<tag>:end -->":
# replaced in place when the block exists, appended when it doesn't (the file is created if missing).
# Text outside the markers is never touched, and the file keeps its inode and mode. A start marker
# with no end marker after it is refused (exit 1, file untouched): the rest of the file might be the user's.
# Usage: aw_write_managed_block <file> <tag> <src>
aw_write_managed_block() {
  local file="$1" tag="$2" src="$3"
  local start="<!-- aw:$tag:start -->" end="<!-- aw:$tag:end -->"
  if aw_dry; then
    echo "  [dry-run] would write the aw:$tag block from $src into $file"
    return 0
  fi
  mkdir -p "$(dirname "$file")"
  if [ ! -f "$file" ] || ! grep -qxF -- "$start" "$file"; then
    # A last line without a newline would glue the start marker onto it, and the next run would not find the block.
    { [ -s "$file" ] && { [ -z "$(tail -c 1 "$file")" ] || printf '\n'; printf '\n'; }; echo "$start"; cat "$src"; echo "$end"; } >> "$file"
    return 0
  fi
  local tmp
  tmp="$(mktemp)"
  awk -v s="$start" -v e="$end" -v src="$src" '
    $0 == s { print; while ((getline line < src) > 0) print line; close(src); skip = 1; next }
    $0 == e && skip { skip = 0; print; next }
    !skip { print }
    END { if (skip) exit 3 }
  ' "$file" > "$tmp" || {
    rm -f "$tmp"
    echo "  WARN: $file has $start but no $end after it; left it alone (fix it by hand)" >&2
    return 1
  }
  cat "$tmp" > "$file"
  rm -f "$tmp"
}

# The reply voice (config/voice/reply.md) names the person the agent replies to as {{user}}. The copy
# setup installs has the name of whoever runs setup there instead, from aw_user_name.
#
# aw_user_name prints the first of:
#   1 AW_USER_NAME, when it passes the checks below (the override when the account's name is wrong)
#   2 the first name in the account's full name: `id -F` or dscl's RealName on macOS, the passwd GECOS
#     field elsewhere (before the first comma; "&" in it stands for the login, capitalised). Leading
#     titles ("Dr.", "Mr", "Prof") are skipped; when the next word is no name, the full name is skipped
#   3 the login, cut at the first ".", "_", "-" or digit and capitalised: "tanner.s" -> "Tanner"
#   4 "human"
# Under sudo it reads SUDO_USER's account, not root's. A word counts as a name only when it is letters
# (ASCII, or any non-ASCII so "José" and "Zoë" pass) with "'" or "-" only inside, at most 32 characters
# and 64 bytes, and not a service or shared account name (root, admin, ubuntu, runner, vscode, ...).
# So "System Administrator" (root's full name on macOS) and a CI box's "runner" both end at "human".
aw_user_name() {
  local login full word
  if word="$(aw_name_word "${AW_USER_NAME:-}")"; then printf '%s\n' "$word"; return 0; fi
  login="$(id -un 2>/dev/null)" || login=""
  [ -n "$login" ] || login="${USER:-${LOGNAME:-}}"
  if [ "$login" = "root" ] && [ -n "${SUDO_USER:-}" ]; then login="$SUDO_USER"; fi
  if [ -n "$login" ]; then
    full="$(aw_account_full_name "$login")"
    if word="$(aw_first_name "$full")"; then printf '%s\n' "$word"; return 0; fi
    if word="$(aw_name_word "${login%%[._0-9-]*}")"; then printf '%s\n' "$word"; return 0; fi
  fi
  printf 'human\n'
}

# The account's full name as the OS records it, or nothing. Usage: aw_account_full_name <login>
aw_account_full_name() {
  local login="$1" full="" cap
  full="$(id -F "$login" 2>/dev/null)" || full=""
  if [ -z "$full" ] && command -v dscl >/dev/null 2>&1; then
    # "RealName: Ann Lee", or "RealName:" with the value on the next line.
    full="$(dscl . -read "/Users/$login" RealName 2>/dev/null |
      awk 'NR == 1 { sub(/^RealName:[ \t]*/, ""); if ($0 != "") { print; exit } next } { sub(/^[ \t]+/, ""); print; exit }')"
  fi
  if [ -z "$full" ]; then
    if command -v getent >/dev/null 2>&1; then
      full="$(getent passwd "$login" 2>/dev/null | cut -d: -f5)"
    elif [ -r /etc/passwd ]; then
      full="$(awk -F: -v u="$login" '$1 == u { print $5; exit }' /etc/passwd)"
    fi
    full="${full%%,*}"
    cap="$(printf '%s' "${login:0:1}" | LC_ALL=C tr '[:lower:]' '[:upper:]')${login:1}"
    full="${full//&/$cap}"
  fi
  printf '%s' "$full"
}

# The first name in a full name: the first word after any titles, when it is a usable name.
# Usage: aw_first_name <full name>
aw_first_name() {
  local lower
  # shellcheck disable=SC2086 # splitting the name into words is the point
  set -f; set -- $1; set +f
  while [ $# -gt 0 ]; do
    lower="$(printf '%s' "${1%.}" | LC_ALL=C tr '[:upper:]' '[:lower:]')"
    case "$lower" in
      dr | mr | mrs | ms | mx | miss | prof | sir | dame | rev) shift ;;
      *) break ;;
    esac
  done
  [ $# -gt 0 ] || return 1
  aw_name_word "$1"
}

# Print <word> with its first letter capitalised when it is a usable first name, else fail.
# Usage: aw_name_word <word>
aw_name_word() {
  local w="$1" lower
  [ -n "$w" ] && [ "${#w}" -le 32 ] || return 1
  [ "$(printf '%s' "$w" | LC_ALL=C wc -c | tr -d ' ')" -le 64 ] || return 1
  # Letters only, ASCII or not, plus ' and - (O'Neil, Anne-Marie): no space, digit, symbol or control byte.
  [ -z "$(printf '%s' "$w" | LC_ALL=C tr -d "A-Za-z'\200-\377-")" ] || return 1
  case "$w" in [\'-]* | *[\'-]) return 1 ;; esac
  lower="$(printf '%s' "$w" | LC_ALL=C tr '[:upper:]' '[:lower:]')"
  case "$lower" in
    root | admin | administrator | system | sysadmin | user | users | guest | nobody | daemon | operator | \
      ubuntu | debian | centos | fedora | rocky | alma | alpine | arch | pi | vagrant | docker | node | \
      runner | vscode | codespace | codespaces | gitpod | coder | jenkins | build | builder | buildkite | ci | \
      dev | developer | default | owner | test | tester | local | localuser | shared | service | app | \
      human | unknown | anonymous | me | ec | azureuser | cloudshell | linuxbrew | homebrew)
      return 1 ;;
  esac
  printf '%s%s\n' "$(printf '%s' "${w:0:1}" | LC_ALL=C tr '[:lower:]' '[:upper:]')" "${w:1}"
}

# Copy <src> to <dst> with every {{user}} replaced by <name>: a literal replace, so nothing in the name
# is read as a pattern or an escape (ENVIRON, not -v, which would turn "\1" into a byte).
# Usage: aw_render_voice <src> <dst> <name>
aw_render_voice() {
  AW_VOICE_NAME="$3" awk '{
    name = ENVIRON["AW_VOICE_NAME"]; out = ""; line = $0
    while ((i = index(line, "{{user}}")) > 0) { out = out substr(line, 1, i - 1) name; line = substr(line, i + 8) }
    print out line
  }' "$1" > "$2"
}

# The name the installed voice uses, in this order:
#   1 AW_USER_NAME, when aw_name_word accepts it (the override)
#   2 the name already in an installed copy, in argument order: the "# Talking to <Name>" line inside a
#     file's aw:voice block, or the first line of a file without one (the rendered copy), so a box that set its name once (or carries the old
#     "Joi" text) keeps it on the next setup instead of flipping to the account name
#   3 aw_user_name (the account's name, the login, then "human")
# Usage: aw_voice_name [<installed file>...]
aw_voice_name() {
  local f found word
  if word="$(aw_name_word "${AW_USER_NAME:-}")"; then printf '%s\n' "$word"; return 0; fi
  for f in "$@"; do
    [ -f "$f" ] || continue
    if grep -qxF -- "<!-- aw:voice:start -->" "$f"; then
      # Only inside the block: the user's own text around it is never read.
      found="$(awk '
        $0 == "<!-- aw:voice:start -->" { inblock = 1; next }
        $0 == "<!-- aw:voice:end -->" { inblock = 0; next }
        inblock && /^# Talking to / { sub(/^# Talking to /, ""); print; exit }
      ' "$f")"
    else
      # A rendered copy: the whole file is the voice, and its first line names the user.
      found="$(sed -n '1s/^# Talking to //p' "$f")"
    fi
    if word="$(aw_name_word "$found")"; then printf '%s\n' "$word"; return 0; fi
  done
  aw_user_name
}

# The rendered voice every provider's skills read (the preamble, the lean agents, Cursor's paste copy).
aw_voice_copy_path() { printf '%s\n' "$AW_STATE_ROOT/voice/reply.md"; }

# Write the rendered voice for <name> to aw_voice_copy_path. Usage: aw_write_voice_copy <name>
aw_write_voice_copy() {
  local out
  out="$(aw_voice_copy_path)"
  if aw_dry; then
    echo "  [dry-run] would write the reply voice for $1 to $out"
    return 0
  fi
  mkdir -p "$(dirname "$out")"
  aw_render_voice "$TOOLKIT_DIR/config/voice/reply.md" "$out" "$1"
}

# The reply voice, with the user's name in it, inlined into a provider's global instruction file, and
# the rendered copy under the state root refreshed with the same name.
# Usage: aw_install_voice <file>
aw_install_voice() {
  local name tmp rc=0
  name="$(aw_voice_name "$1" "$(aw_voice_copy_path)")"
  aw_write_voice_copy "$name"
  tmp="$(mktemp)"
  aw_render_voice "$TOOLKIT_DIR/config/voice/reply.md" "$tmp" "$name"
  aw_write_managed_block "$1" voice "$tmp" || rc=$?
  rm -f "$tmp"
  [ "$rc" -eq 0 ] || return 0
  echo "  voice: $1 (replies to $name)"
}

# Write $HOME/.agentic-workflow/providers: one "<name> <skills-dir>" line per
# installed provider. Lines for providers not touched by this run are kept as
# long as their skills dir still exists (so `--providers codex` doesn't drop
# a previously-installed claude entry).
# Usage: aw_write_provider_registry "claude /Users/x/.claude/skills" "codex ..."
aw_write_provider_registry() {
  local registry="$AW_STATE_ROOT/providers" tmp line name dir keep entry
  tmp="$(mktemp)"
  for entry in "$@"; do
    printf '%s\n' "$entry" >> "$tmp"
  done
  if [ -f "$registry" ]; then
    while IFS= read -r line; do
      [ -n "$line" ] || continue
      name="${line%% *}"
      dir="${line#* }"
      keep=1
      for entry in "$@"; do
        [ "${entry%% *}" = "$name" ] && keep=0
      done
      if [ "$keep" = "1" ] && [ -d "$dir" ]; then
        printf '%s\n' "$line" >> "$tmp"
      fi
    done < "$registry"
  fi
  sort -u -o "$tmp" "$tmp"
  if aw_dry; then
    echo "  [dry-run] would write $registry:"
    sed 's/^/    /' "$tmp"
    rm -f "$tmp"
  else
    mkdir -p "$AW_STATE_ROOT"
    mv "$tmp" "$registry"
    echo "  provider registry: $registry"
    sed 's/^/    /' "$registry"
  fi
}

# Is <cli> on PATH?
aw_has() { command -v "$1" &>/dev/null; }

# Iterate the MCP server specs (JSON array in AW_MCP_SERVERS) one compact
# object per line: {"name":..,"command":..,"args":[..],"env":{..}}
aw_mcp_each() {
  printf '%s' "${AW_MCP_SERVERS:-[]}" | jq -c '.[]'
}

# Unpack one JSON spec into globals (bash 3.2-safe):
#   AW_MCP_NAME, AW_MCP_CMD, AW_MCP_ARGV (array), AW_MCP_ENV (array of KEY=VALUE)
aw_mcp_unpack() {
  local spec="$1" a
  AW_MCP_NAME="$(printf '%s' "$spec" | jq -r '.name')"
  AW_MCP_CMD="$(printf '%s' "$spec" | jq -r '.command')"
  AW_MCP_ARGV=()
  while IFS= read -r a; do
    AW_MCP_ARGV+=("$a")
  done < <(printf '%s' "$spec" | jq -r '.args[]?')
  AW_MCP_ENV=()
  while IFS= read -r a; do
    [ -n "$a" ] && AW_MCP_ENV+=("$a")
  done < <(printf '%s' "$spec" | jq -r '(.env // {}) | to_entries[] | "\(.key)=\(.value)"')
  return 0
}

# Register every MCP server with a CLI-based provider.
#   aw_mcp_register_cli <label> <present-fn> <add-fn>
# <present-fn> NAME → 0 if already registered; <add-fn> is called after
# aw_mcp_unpack and must run the provider's add command (via aw_run).
aw_mcp_register_cli() {
  local label="$1" present_fn="$2" add_fn="$3" spec
  echo ""
  echo "Registering MCP servers with $label..."
  while IFS= read -r spec; do
    aw_mcp_unpack "$spec"
    if "$present_fn" "$AW_MCP_NAME" </dev/null; then
      echo "  $AW_MCP_NAME: already registered in $label"
      continue
    fi
    if aw_dry; then
      "$add_fn" </dev/null
    elif "$add_fn" </dev/null >/dev/null 2>&1; then
      echo "  $AW_MCP_NAME: registered in $label"
    else
      echo "  WARN: $AW_MCP_NAME registration failed in $label (non-fatal)"
    fi
  done < <(aw_mcp_each)
}

# Remove MCP servers the toolkit no longer ships (AW_MCP_RETIRED, space separated) from a CLI-based provider, so a
# re-run of setup.sh cleans an existing install. Only a registered server is removed, never one still in
# AW_MCP_SERVERS (as cursor's upsert keeps it); dry-run prints instead.
#   aw_mcp_retire_cli <label> <present-fn> <remove-fn>
# <remove-fn> reads AW_MCP_NAME and must run the provider's remove command (via aw_run).
aw_mcp_retire_cli() {
  local label="$1" present_fn="$2" remove_fn="$3" name shipped
  shipped=" $(aw_mcp_each | jq -r '.name' | tr '\n' ' ')"
  for name in ${AW_MCP_RETIRED:-}; do
    case "$shipped" in *" $name "*) continue ;; esac
    "$present_fn" "$name" </dev/null || continue
    AW_MCP_NAME="$name"
    if aw_dry; then
      echo "  [dry-run] would remove $name from $label (retired)"
    elif "$remove_fn" </dev/null >/dev/null 2>&1; then
      echo "  $name: removed from $label (retired)"
    else
      echo "  WARN: $name removal failed in $label (non-fatal)"
    fi
  done
}

# Remove a retired package's directory from the toolkit checkout once everything left in it is ignored build output
# (dist/, node_modules/, a database). Kept: a directory with tracked files or with untracked files git doesn't ignore
# (someone's uncommitted work), and anything outside a git checkout.
#   aw_remove_retired_dir <repo> <relative-dir>
aw_remove_retired_dir() {
  local repo="$1" rel="$2"
  [ -d "$repo/$rel" ] || return 0
  git -C "$repo" rev-parse --git-dir >/dev/null 2>&1 || return 0
  [ -z "$(git -C "$repo" ls-files -- "$rel")" ] || return 0
  [ -z "$(git -C "$repo" ls-files --others --exclude-standard -- "$rel")" ] || return 0
  if aw_dry; then
    echo "  [dry-run] would remove $repo/$rel (retired; only ignored build output left)"
  else
    rm -rf "${repo:?}/${rel:?}" && echo "  removed $repo/$rel (retired)"
  fi
}

# Cheap-agent-harness lever hooks for one provider. Each scripts/install-*.sh
# takes --provider (default claude) and no-ops with a note where a lever is
# unsupported on that provider.
AW_LEVER_INSTALLERS="install-wake-gating install-context-guard install-scope-gate install-done-gate install-external-write-guard"
aw_install_levers() {
  local provider="$1" inst
  echo ""
  echo "=== Installing lever hooks for $provider (wake gating 1A, context-guard 2B, evaluator gates 3) ==="
  for inst in $AW_LEVER_INSTALLERS; do
    if aw_dry; then
      echo "  [dry-run] would run scripts/$inst.sh --provider $provider"
    else
      bash "$TOOLKIT_DIR/scripts/$inst.sh" --provider "$provider"
    fi
  done
}
