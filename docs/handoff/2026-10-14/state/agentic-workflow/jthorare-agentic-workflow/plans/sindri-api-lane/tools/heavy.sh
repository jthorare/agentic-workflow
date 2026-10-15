#!/bin/bash
# usage: heavy.sh <cmd...> — forwards to the shared lock wrapper ~/.agentic-workflow/bin-r1b/heavy (rules since the
# 2026-10-14 crash: one heavy job per session, shared by its subagents; whole-solution builds and tests, publish and
# AOT also take one of 2 box-wide slots). The wrapper waits for its slot itself (exit 75 on its timeout).
# gate.sh builds and tests the whole solution, so it always runs as --expensive.
export AW_HEAVY_SLOTS=2 AW_SESSION_HEAVY_SLOTS=1
case "$(basename "${1:-}")" in
  gate.sh) exec "$HOME/.agentic-workflow/bin-r1b/heavy" --expensive "$@" ;;
  *) exec "$HOME/.agentic-workflow/bin-r1b/heavy" "$@" ;;
esac
