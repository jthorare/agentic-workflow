#!/usr/bin/env bash
# Workspace for the meta cases: only the toolkit files the skill text reads. The aw-meta stub carries the team.
set -eu
AW_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/../_lib" && pwd)"
. "$AW_LIB/shim.sh"
shim_toolkit
: > transcript.jsonl
