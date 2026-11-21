#!/usr/bin/env bash
# Install the lean pinned-model agent types (config/agents/*.md) for a provider.
#
#   install_agents <src_dir> <dest_dir> <manifest_file> [format]
#   uninstall_agents <dest_dir> <manifest_file>
#
# format:
#   claude (default) — copy the canonical .md verbatim (Claude Code subagent format)
#   codex            — render ~/.codex/agents/<name>.toml
#                      (fields: name, description, developer_instructions,
#                       model_reasoning_effort, sandbox_mode)
#   cursor           — render ~/.cursor/agents/<name>.md
#                      (frontmatter: name, description, model, readonly)
#
# Ownership is tracked in a content-hash manifest keyed by the installed file
# name; a file hand-edited since install (hash mismatch) is never overwritten.
set -euo pipefail

sha256_of() { shasum -a 256 "$1" | awk '{print $1}'; }

# --- frontmatter helpers -----------------------------------------------------

# _agent_field <file> <key> — value of a top-level "key: value" frontmatter line
_agent_field() {
  awk -v k="$2" '
    NR==1 && $0=="---" { infm=1; next }
    infm && $0=="---" { exit }
    infm {
      i = index($0, ":")
      if (i > 0 && substr($0, 1, i-1) == k) {
        v = substr($0, i+1); sub(/^[ \t]+/, "", v); sub(/[ \t]+$/, "", v); print v; exit
      }
    }' "$1"
}

# _agent_body <file> — everything after the closing frontmatter ---
_agent_body() {
  awk 'NR==1 && $0=="---" { infm=1; next }
       infm && $0=="---" { infm=0; body=1; next }
       body { print }' "$1"
}

# _agent_is_readonly <file> — true if the Claude tools list grants no write tools
_agent_is_readonly() {
  local tools
  tools="$(_agent_field "$1" tools)"
  [ -n "$tools" ] || return 1
  ! grep -qE '(^|[ ,])(Edit|Write|MultiEdit|NotebookEdit)([ ,]|$)' <<<"$tools"
}

# TOML basic-string escape (backslash and double quote).
_toml_escape() { sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }

# --- renderers -----------------------------------------------------------------

# Claude model pins have no stable cross-provider equivalent. Codex: omit
# `model` (inherit the parent session's model) and map the pin to a reasoning
# effort so the "lean" intent survives. Cursor: haiku -> fast, else inherit.
_codex_effort_for() {
  case "$1" in
    haiku) echo "low" ;;
    sonnet) echo "medium" ;;
    opus) echo "high" ;;
    *) echo "" ;;
  esac
}
_cursor_model_for() {
  case "$1" in
    haiku) echo "fast" ;;
    *) echo "inherit" ;;
  esac
}

render_agent_codex() {
  local src="$1" name desc model effort
  name="$(_agent_field "$src" name)"; [ -n "$name" ] || name="$(basename "$src" .md)"
  desc="$(_agent_field "$src" description)"
  model="$(_agent_field "$src" model)"
  effort="$(_codex_effort_for "$model")"
  echo "# Managed by Agentic Workflow — generated from config/agents/$(basename "$src")."
  echo "# Hand edits are preserved (setup.sh stops upgrading this file once it changes)."
  echo "# Claude model pin \"${model:-none}\" is not portable: model is inherited from the"
  echo "# parent session and the pin is mapped to model_reasoning_effort instead."
  printf 'name = "%s"\n' "$(printf '%s' "$name" | _toml_escape)"
  printf 'description = "%s"\n' "$(printf '%s' "$desc" | _toml_escape)"
  [ -n "$effort" ] && printf 'model_reasoning_effort = "%s"\n' "$effort"
  if _agent_is_readonly "$src"; then
    echo 'sandbox_mode = "read-only"'
  fi
  echo 'developer_instructions = """'
  _agent_body "$src" | _toml_escape
  echo '"""'
}

render_agent_cursor() {
  local src="$1" name desc model
  name="$(_agent_field "$src" name)"; [ -n "$name" ] || name="$(basename "$src" .md)"
  desc="$(_agent_field "$src" description)"
  model="$(_agent_field "$src" model)"
  echo "---"
  echo "name: $name"
  echo "description: $desc"
  echo "model: $(_cursor_model_for "$model")"
  if _agent_is_readonly "$src"; then
    echo "readonly: true"
  fi
  echo "---"
  echo "<!-- Managed by Agentic Workflow — generated from config/agents/$(basename "$src"). Claude model pin \"${model:-none}\" mapped to Cursor model \"$(_cursor_model_for "$model")\". -->"
  _agent_body "$src"
}

# render_agent <src.md> <format> <out_file>; echoes the installed file name
_agent_dest_name() {
  local src="$1" format="$2"
  case "$format" in
    codex) echo "$(basename "$src" .md).toml" ;;
    *) basename "$src" ;;
  esac
}

render_agent() {
  local src="$1" format="$2" out="$3"
  case "$format" in
    claude) cp "$src" "$out" ;;
    codex) render_agent_codex "$src" > "$out" ;;
    cursor) render_agent_cursor "$src" > "$out" ;;
    *) echo "install_agents: unknown format '$format'" >&2; return 1 ;;
  esac
}

# --- install / uninstall -----------------------------------------------------------

install_agents() {
  local src_dir="$1" dest_dir="$2" manifest_file="$3" format="${4:-claude}"
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    local f
    for f in "$src_dir"/*.md; do
      echo "  [dry-run] would install $(_agent_dest_name "$f" "$format") → $dest_dir ($format format)"
    done
    return 0
  fi
  mkdir -p "$dest_dir"
  [ -f "$manifest_file" ] || echo '{}' > "$manifest_file"
  local f name dest_file new_hash recorded_hash current_hash rendered
  rendered="$(mktemp)"
  for f in "$src_dir"/*.md; do
    name="$(_agent_dest_name "$f" "$format")"
    dest_file="$dest_dir/$name"
    render_agent "$f" "$format" "$rendered"
    new_hash="$(sha256_of "$rendered")"
    if [ -f "$dest_file" ]; then
      current_hash="$(sha256_of "$dest_file")"
      recorded_hash="$(jq -r --arg n "$name" '.[$n] // empty' "$manifest_file")"
      if [ -n "$recorded_hash" ] && [ "$recorded_hash" = "$current_hash" ]; then
        cp "$rendered" "$dest_file"   # unmodified since we installed it — safe to upgrade
      else
        echo "skip: $name exists and was modified since install (or was never installed by us) — not overwriting" >&2
        continue
      fi
    else
      cp "$rendered" "$dest_file"
    fi
    local tmp
    tmp="$(mktemp)"
    jq --arg n "$name" --arg h "$new_hash" '.[$n] = $h' "$manifest_file" > "$tmp"
    mv "$tmp" "$manifest_file"
  done
  rm -f "$rendered"
}

uninstall_agents() {
  local dest_dir="$1" manifest_file="$2"
  [ -f "$manifest_file" ] || return 0
  local name recorded_hash current_hash tmp
  tmp="$(mktemp)"
  cp "$manifest_file" "$tmp"
  for name in $(jq -r 'keys[]' "$manifest_file"); do
    recorded_hash="$(jq -r --arg n "$name" '.[$n]' "$manifest_file")"
    if [ -f "$dest_dir/$name" ]; then
      current_hash="$(sha256_of "$dest_dir/$name")"
      if [ "$current_hash" = "$recorded_hash" ]; then
        rm "$dest_dir/$name"
        jq --arg n "$name" 'del(.[$n])' "$tmp" > "$tmp.next" && mv "$tmp.next" "$tmp"
      else
        echo "skip removing $name: modified since install" >&2
      fi
    fi
  done
  mv "$tmp" "$manifest_file"
}
