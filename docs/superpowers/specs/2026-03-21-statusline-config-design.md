# Statusline Configuration Design

**Date:** 2026-03-21
**Issue:** #9 — Add statusline configuration
**Branch:** feat/statusline-config

## Summary

Add a Claude Code statusline showing model, git branch, context usage, session cost, elapsed time, cache hit rate, API wait percentage, lines changed, and rate limit usage. Two-line output with a dimmed header row for scannability.

## Output Format

```
Model   │ Branch          │ Context    │ Cost  │ Time │ Cache │ API  │ Lines    │ Rate
Opus    │ feat/my-branch  │ ████████░░ │ $2.40 │ 45m  │ 82%   │ 60%  │ +142 -38 │ 5h: 34%
```

Line 1: Header row rendered in dim/grey (`\033[2m`), reset with `\033[0m` at end.
Line 2: Values with color-coded context bar and rate limit.

The `│` separator is a visual delimiter, not a strict column boundary. Each field uses fixed-width `printf` formatting for reasonable alignment, but long branch names will be truncated (max 20 chars) rather than breaking the layout.

## Fields

| # | Field | Source | Format | Fallback |
|---|-------|--------|--------|----------|
| 1 | Model | `model.display_name` | Raw string | `--` |
| 2 | Branch | `git -C $DIR rev-parse --abbrev-ref HEAD` | Truncated to 20 chars | `--` (detached HEAD shows short hash) |
| 3 | Context | `context_window.used_percentage` | 10-char bar (█ filled, ░ empty) | Empty bar `░░░░░░░░░░` |
| 4 | Cost | `cost.total_cost_usd` | `$X.XX` | `$0.00` |
| 5 | Time | `cost.total_duration_ms` | `Xm` or `Xh Ym` | `0m` |
| 6 | Cache | `current_usage.cache_read_input_tokens / (cache_read + cache_creation + input_tokens)` | `X%` | `--` |
| 7 | API | `total_api_duration_ms / total_duration_ms * 100` | `X%` | `--` |
| 8 | Lines | `cost.total_lines_added`, `cost.total_lines_removed` | `+N -N` | `+0 -0` |
| 9 | Rate | `rate_limits.five_hour.used_percentage` | `5h: X%` | Hidden (column omitted) |

## Color Coding

### Context Bar
- **Green** (`\033[32m`): <50% usage
- **Yellow** (`\033[33m`): 50-75% usage
- **Red** (`\033[31m`): >75% usage

### Rate Limit
Same thresholds as context bar applied to `five_hour.used_percentage`.

All color sequences are terminated with `\033[0m` to prevent bleed.

## Graceful Degradation

- **All fields**: If stdin is empty or not valid JSON, output a minimal fallback line (`-- │ -- │ ░░░░░░░░░░ │ -- │ -- │ -- │ -- │ -- │ --`) rather than exiting non-zero (which blanks the statusline).
- **Cache/API**: Show `--` before first API call (`current_usage` is null) and when denominator is zero (division-by-zero guard).
- **Context**: Show empty bar when `used_percentage` is null (early session).
- **Rate limit**: Entire column hidden when `rate_limits` is null (API-key users, not Pro/Max). Header row adjusts to match.
- **Git branch**: Use `git -C "$DIR"` with `$DIR` from `workspace.current_dir`. Detached HEAD falls back to short commit hash via `git rev-parse --short HEAD`. Non-git directory shows `--`.
- **Model**: Show `--` if `model` or `model.display_name` is null/missing.

## Implementation Constraints

### Performance
- **Single jq invocation**: Extract all fields in one `jq` call with tab-separated output, then `read` to split. Avoids spawning 9+ subprocesses per invocation.
- **No `set -e`**: Individual field failures should not blank the entire statusline. Each field has its own fallback.
- **Use `printf '%b'`** for all output lines containing ANSI escape sequences (`echo -e` is unreliable across shells).

### Unicode Characters
- Filled bar: `█` (U+2588)
- Empty bar: `░` (U+2591)

## Data Interface

The statusline script receives JSON via **stdin** (not environment variables). Key paths:

```json
{
  "model": { "display_name": "Opus" },
  "context_window": {
    "used_percentage": 78.2,
    "current_usage": {
      "input_tokens": 1200,
      "cache_creation_input_tokens": 500,
      "cache_read_input_tokens": 3000
    }
  },
  "cost": {
    "total_cost_usd": 2.40,
    "total_duration_ms": 2700000,
    "total_api_duration_ms": 1620000,
    "total_lines_added": 142,
    "total_lines_removed": 38
  },
  "rate_limits": {
    "five_hour": { "used_percentage": 34.0 }
  },
  "workspace": { "current_dir": "/path/to/repo" }
}
```

## File Layout

| File | Purpose |
|------|---------|
| `config/statusline.sh` | Statusline script (bash, reads JSON stdin via single jq call) |
| `config/settings.json` | Add `statusLine` config block |
| `setup.sh` | Copy script to `~/.claude/statusline.sh`, chmod +x, verify jq |

## Settings.json Addition

```json
{
  "statusLine": {
    "type": "command",
    "command": "~/.claude/statusline.sh"
  }
}
```

## Setup.sh Changes

### jq dependency check

Add near the top with other prerequisite checks:

```bash
if ! command -v jq &>/dev/null; then
  echo "  ⚠ jq not found — statusline will not work. Install: brew install jq"
fi
```

### Script installation

After the existing settings.json copy step:

```bash
cp config/statusline.sh ~/.claude/statusline.sh
chmod +x ~/.claude/statusline.sh
echo "  statusline script installed"
```

### Settings merge for existing users

Since setup.sh does not overwrite an existing `settings.json`, add a `jq`-based merge step that inserts the `statusLine` key if absent:

```bash
if [ -f ~/.claude/settings.json ]; then
  if ! jq -e '.statusLine' ~/.claude/settings.json &>/dev/null; then
    jq '. + {"statusLine": {"type": "command", "command": "~/.claude/statusline.sh"}}' \
      ~/.claude/settings.json > ~/.claude/settings.json.tmp \
      && mv ~/.claude/settings.json.tmp ~/.claude/settings.json
    echo "  statusLine config added to existing settings.json"
  fi
fi
```

## Acceptance Criteria

- [ ] `config/statusline.sh` created with all 9 fields
- [ ] Two-line output: dimmed header + color-coded values
- [ ] Single `jq` invocation for all field extraction
- [ ] `printf '%b'` for ANSI escape output
- [ ] Color-coded context bar (green/yellow/red at 50%/75% thresholds)
- [ ] Color-coded rate limit (same thresholds)
- [ ] Graceful fallbacks for missing/null data, malformed JSON, and division-by-zero
- [ ] Detached HEAD shows short hash, non-git shows `--`
- [ ] Git uses `workspace.current_dir` via `git -C`
- [ ] Rate limit column hidden entirely when not available
- [ ] `config/settings.json` updated with `statusLine` block
- [ ] `setup.sh` updated: jq check, script install, settings merge for existing users
