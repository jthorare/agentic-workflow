#!/usr/bin/env bash
# Sourced by scripts/install-meta.sh and scripts/meta-launch.sh: what an installed aw-meta was built from (`aw-meta version --json`:
# commit, builtAt, os, rid), and whether that binary is older than the Meta sources of a checkout.
#
#   meta_version_json <bin>             the binary's `version --json` output; empty for a build that has no version command
#                                       (older than that command) or that can't run
#   meta_version_line <bin>             one line: "aw-meta commit <commit>, built <time>, <os> <rid>", or why there is no version
#   meta_stale_line <bin> <repo-dir>    one warning line when the binary is older than the sources it is run from, else nothing
#
# A binary is stale when the last commit that touched the sources it is built from (META_BUILD_PATHS, read from
# scripts/meta-build-paths.txt: the one list install-meta.sh copies into its publish tree) is not an ancestor of, or equal to, the
# commit the binary was built from. A binary built from a tree with uncommitted changes there is stamped <commit>-dirty and always
# warns: it is not the commit it names.

META_BUILD_PATHS=()
while IFS= read -r _meta_path; do
  case "$_meta_path" in '' | '#'*) ;; *) META_BUILD_PATHS+=("$_meta_path") ;; esac
done < "$(dirname "${BASH_SOURCE[0]}")/meta-build-paths.txt"
unset _meta_path

meta_version_json() { "$1" version --json 2> /dev/null || true; }

meta_version_line() {
  local bin="$1" json commit
  json="$(meta_version_json "$bin")"
  commit="$(jq -r '.commit // empty' <<< "$json" 2> /dev/null || true)"
  if [ -z "$commit" ]; then
    echo "$bin predates 'aw-meta version' (it carries no commit stamp); reinstall with scripts/install-meta.sh"
    return 0
  fi
  jq -r '"aw-meta commit \(.commit), built \(.builtAt), \(.os) \(.rid)"' <<< "$json"
}

meta_stale_line() {
  local bin="$1" repo="$2" json commit head
  [ -x "$bin" ] || return 0
  git -C "$repo" rev-parse --git-dir > /dev/null 2>&1 || return 0
  head="$(git -C "$repo" log -1 --format=%H -- "${META_BUILD_PATHS[@]}" 2> /dev/null || true)"
  [ -n "$head" ] || return 0
  json="$(meta_version_json "$bin")"
  commit="$(jq -r '.commit // empty' <<< "$json" 2> /dev/null || true)"
  if [ -z "$commit" ]; then
    echo "meta-launch: warning: the installed aw-meta ($bin) predates 'aw-meta version', so it is older than the Meta sources in $repo; run scripts/install-meta.sh"
  elif [ "$commit" = unknown ]; then
    echo "meta-launch: warning: the installed aw-meta ($bin) carries no commit stamp, so it cannot be compared with the Meta sources in $repo; run scripts/install-meta.sh"
  elif [[ "$commit" == *-dirty ]]; then
    echo "meta-launch: warning: the installed aw-meta was built from a dirty tree (${commit:0:12}), so it may not match the Meta sources in $repo; run scripts/install-meta.sh from a clean checkout"
  elif ! git -C "$repo" cat-file -e "$commit^{commit}" 2> /dev/null; then
    echo "meta-launch: warning: the installed aw-meta was built from ${commit:0:12}, which this checkout does not have; run scripts/install-meta.sh from here"
  elif ! git -C "$repo" merge-base --is-ancestor "$head" "$commit" 2> /dev/null; then
    echo "meta-launch: warning: the installed aw-meta (${commit:0:12}) is older than the Meta sources in $repo (last changed in ${head:0:12}); run scripts/install-meta.sh"
  fi
  return 0
}
