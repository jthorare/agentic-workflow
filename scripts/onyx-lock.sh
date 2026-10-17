#!/usr/bin/env bash
# scripts/onyx-lock.sh — check the shape of context/onyx.lock, the pins Sindri-managed Onyx is built from
# (spec docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md §6).
#
# Usage: scripts/onyx-lock.sh check [--file F]
#        scripts/onyx-lock.sh verify [--file F]
#   check  every row is well formed, there is exactly one version row, there is at least one image and every image has both
#          platforms once, the compose file docker_compose/docker-compose.yml has a file row, and the two need rows are present once. Prints one line per problem: 'ONYX-LOCK-00n <line>: <what>'.
#   verify maintainer-run, needs the network and docker buildx, never in CI: runs check, then downloads every `file` row from
#          raw.githubusercontent.com/onyx-dot-app/onyx/<commit>/deployment/<path> and compares its sha256, compares every
#          `image` row with `docker buildx imagetools inspect <repository>:<tag> --raw` for its platform, and confirms the
#          compose file at the commit names only pinned images (each `image:` line, with its shell-style defaults resolved and
#          the image tag variable taken as the version, is a repository and tag of an image row; a line that pins a digest with
#          `@sha256:...` is resolved with `docker buildx imagetools inspect <repository>@<digest> --raw`: an index must hold the
#          lock's linux/arm64 and linux/amd64 digests of that repository, a single manifest must be one of them; a compose with
#          no image line at all fails). Prints one line per mismatch
#          'ONYX-LOCK-01n <what>: expected X, got Y'. Needs curl, jq, docker and a sha256 tool. Exit: 0 all match, 1 a
#          mismatch (or check fails), 2 usage, I/O or network error.
# Rows (whitespace-separated; '#' comments and blank lines are ignored):
#   version <tag> <commit-sha-40>                    exactly one; tag is vX.Y.Z
#   file <path under deployment/> <sha256-64>        a deployment file Sindri fetches at <commit> and checks
#   image <repository> <tag> <platform> <sha256:digest>   every image, for linux/arm64 and linux/amd64; one tag per repository
#   need disk-bytes <n>                              exactly one
#   need docker-memory-bytes <n>                     exactly one
#   A repository matches ^[a-z0-9][a-z0-9._/-]*$ and a tag ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$; a file path matches ^[A-Za-z0-9._/-]+$, is relative, and has no empty or .. segment;
#   a line holds no NUL, CR, VT or FF (the lock is LF-only); no file path is listed twice; a need is 1 to 18 digits and positive (it fits a long).
# Parity: OnyxPins.Parse (src/Sindri/Aw.Sindri.Core/Onyx/OnyxPins.cs) refuses exactly the files this check refuses, as far as the
#   fixtures of scripts/tests/onyx-lock.test.sh and tests/Aw.Sindri.Tests/OnyxPinsTests/OnyxPinsParseTests.cs go: both suites
#   run the same malformed cases (need of 19 digits, duplicate file or image row, bad repository name, bad file paths, NUL, CR, VT, FF).
#   The C# parser reports every refusal as SND-ONYX-004 where this script uses the codes below.
# Codes: 001 version row (missing, repeated, bad tag), 002 a malformed value, 003 image platforms (missing or repeated),
#        004 need rows (missing, repeated, not a number), 005 unknown row kind or need name.
# Exit: 0 ok, 1 a check failed, 2 usage or I/O error.
# Codes of verify: 010 a file's sha256 differs, 011 an image digest differs or is absent, 012 the compose names an unpinned image, a digest the lock doesn't hold, or no image at all.
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() { sed -n '5,6p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; { [ "$cmd" = "check" ] || [ "$cmd" = "verify" ]; } || usage; shift
file="$ROOT/context/onyx.lock"
while [ $# -gt 0 ]; do
  case "$1" in
    --file) file="${2:?--file needs a path}"; shift 2 ;;
    *) usage ;;
  esac
done
[ -r "$file" ] || { echo "onyx-lock: cannot read $file" >&2; exit 2; }

# A NUL byte anywhere is refused here, before awk (which would truncate at it); OnyxPins.Parse refuses it the same way.
if [ "$(LC_ALL=C tr -d '\000' <"$file" | wc -c)" -ne "$(LC_ALL=C wc -c <"$file")" ]; then
  echo "ONYX-LOCK-002 0: the file contains a NUL byte"
  exit 1
fi

# A CR, VT or FF anywhere on a line is refused too (the shipped lock is LF-only); OnyxPins.Parse refuses it the same way.
ctl="$(LC_ALL=C grep -n $'[\r\v\f]' "$file" | cut -d: -f1 | head -n1 || true)"
if [ -n "$ctl" ]; then
  echo "ONYX-LOCK-002 $ctl: the line contains a CR, VT or FF control character"
  exit 1
fi

do_check() {
LC_ALL=C awk '
  function bad(code, msg) { printf "%s %d: %s\n", code, NR, msg; failed = 1 }
  function hex(s, n) { return s ~ /^[0-9a-f]+$/ && length(s) == n }
  /^[[:space:]]*(#|$)/ { next }
  $1 == "version" {
    versions++
    if (NF != 3) { bad("ONYX-LOCK-001", "version wants 2 fields (tag commit), got " (NF - 1)); next }
    if ($2 !~ /^v[0-9]+\.[0-9]+\.[0-9]+$/) bad("ONYX-LOCK-001", "tag \"" $2 "\" is not vX.Y.Z")
    if (!hex($3, 40)) bad("ONYX-LOCK-002", "commit is not 40 lowercase hex digits")
    next
  }
  $1 == "file" {
    if (NF != 3) { bad("ONYX-LOCK-002", "file wants 2 fields (path sha256), got " (NF - 1)); next }
    if ($2 !~ /^[A-Za-z0-9._\/-]+$/ || $2 ~ /^\// || $2 ~ /\/\// || $2 ~ /\/$/ || $2 ~ /(^|\/)\.\.(\/|$)/) bad("ONYX-LOCK-002", "path \"" $2 "\" is not a relative path of [A-Za-z0-9._/-] with non-empty segments and no .. inside deployment/")
    if ($2 in files) bad("ONYX-LOCK-002", "path " $2 " is listed twice")
    files[$2] = 1
    if (!hex($3, 64)) bad("ONYX-LOCK-002", "sha256 is not 64 lowercase hex digits")
    next
  }
  $1 == "image" {
    if (NF != 5) { bad("ONYX-LOCK-002", "image wants 4 fields (repository tag platform digest), got " (NF - 1)); next }
    if ($2 !~ /^[a-z0-9][a-z0-9._\/-]*$/) bad("ONYX-LOCK-002", "repository \"" $2 "\" does not match ^[a-z0-9][a-z0-9._/-]*$")
    if ($3 !~ /^[A-Za-z0-9_][A-Za-z0-9_.-]*$/ || length($3) > 128) bad("ONYX-LOCK-002", "tag \"" $3 "\" does not match ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$")
    if ($4 != "linux/arm64" && $4 != "linux/amd64") { bad("ONYX-LOCK-002", "platform \"" $4 "\" is not linux/arm64 or linux/amd64"); next }
    if ($5 !~ /^sha256:[0-9a-f]+$/ || length($5) != 71) bad("ONYX-LOCK-002", "digest is not sha256: and 64 lowercase hex digits")
    if (($2 SUBSEP $4) in seen) bad("ONYX-LOCK-003", $2 " " $4 " is listed twice")
    if (($2 in tagof) && tagof[$2] != $3) bad("ONYX-LOCK-002", $2 " is listed with tags " tagof[$2] " and " $3)
    tagof[$2] = $3
    seen[$2, $4] = 1; repos[$2] = 1
    next
  }
  $1 == "need" {
    if (NF != 3) { bad("ONYX-LOCK-004", "need wants 2 fields (name number), got " (NF - 1)); next }
    if ($2 != "disk-bytes" && $2 != "docker-memory-bytes") { bad("ONYX-LOCK-005", "need \"" $2 "\" is not disk-bytes or docker-memory-bytes"); next }
    if ($3 !~ /^[0-9]+$/ || length($3) > 18 || $3 + 0 == 0) bad("ONYX-LOCK-004", $2 " \"" $3 "\" is not a positive integer of at most 18 digits")
    if ($2 in needs) bad("ONYX-LOCK-004", $2 " is listed twice")
    needs[$2] = 1
    next
  }
  { bad("ONYX-LOCK-005", "unknown row kind \"" $1 "\"") }
  END {
    if (versions == 0) { print "ONYX-LOCK-001 0: no version row"; failed = 1 }
    if (versions > 1) { print "ONYX-LOCK-001 0: " versions " version rows, want 1"; failed = 1 }
    if (length(repos) == 0) { print "ONYX-LOCK-003 0: no image rows"; failed = 1 }
    if (!("docker_compose/docker-compose.yml" in files)) { print "ONYX-LOCK-002 0: no file row for docker_compose/docker-compose.yml"; failed = 1 }
    for (r in repos) {
      if (!((r SUBSEP "linux/arm64") in seen)) { printf "ONYX-LOCK-003 %s: no linux/arm64 row\n", r; failed = 1 }
      if (!((r SUBSEP "linux/amd64") in seen)) { printf "ONYX-LOCK-003 %s: no linux/amd64 row\n", r; failed = 1 }
    }
    if (!("disk-bytes" in needs)) { print "ONYX-LOCK-004 0: no need disk-bytes row"; failed = 1 }
    if (!("docker-memory-bytes" in needs)) { print "ONYX-LOCK-004 0: no need docker-memory-bytes row"; failed = 1 }
    exit failed
  }
' "$file"
}

# resolve <image: value>: the image tag variable's default becomes the pinned version, every other variable its own default, innermost first.
resolve() {
  local value="$1" next
  value="$(printf '%s' "$value" | sed -E 's/\$\{IMAGE_TAG:-[^${}]*\}/'"$version"'/g')"
  while [[ "$value" == *'${'* ]]; do
    next="$(printf '%s' "$value" | sed -E 's/\$\{[A-Za-z_][A-Za-z0-9_]*:-([^${}]*)\}/\1/')"
    [ "$next" != "$value" ] || break
    value="$next"
  done
  printf '%s' "$value"
}

# sha256 of a file, with whichever tool this box has
sha256_of() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

# check_pinned_digest <compose value> <repository[:tag]> <digest> <1 when the ref has a tag>: a compose line that pins `@digest`
# must name a repository (and tag) of the lock and a digest the lock holds: an index holds the lock's two platform digests of the
# repository, a single manifest is one of them. Prints ONYX-LOCK-012 and returns 1 when not; exit 2 when the registry can't be asked.
check_pinned_digest() {
  local value="$1" ref="$2" digest="$3" has_tag="$4" repo tag arm amd raw got_arm got_amd
  repo="$ref"; tag=""
  if [ "$has_tag" -eq 1 ]; then repo="${ref%:*}"; tag="${ref##*:}"; fi
  if ! [[ "$digest" =~ ^sha256:[0-9a-f]{64}$ ]]; then
    echo "ONYX-LOCK-012 docker-compose.yml: line \"$value\" pins $digest, which is not sha256: and 64 lowercase hex digits"; return 1
  fi
  if [ -n "$tag" ] && ! awk -v r="$repo" -v t="$tag" '$1 == "image" && $2 == r && $3 == t { found = 1 } END { exit !found }' "$file"; then
    echo "ONYX-LOCK-012 docker-compose.yml: line \"$value\" names $repo:$tag, expected an image row for it, got none"; return 1
  fi
  arm="$(awk -v r="$repo" '$1 == "image" && $2 == r && $4 == "linux/arm64" { print $5 }' "$file")"
  amd="$(awk -v r="$repo" '$1 == "image" && $2 == r && $4 == "linux/amd64" { print $5 }' "$file")"
  if [ -z "$arm" ] || [ -z "$amd" ]; then
    echo "ONYX-LOCK-012 docker-compose.yml: line \"$value\" names $repo, which the lock has no image rows for"; return 1
  fi
  raw="$(docker buildx imagetools inspect "$repo@$digest" --raw 2>"$tmp/error" </dev/null)" \
    || { echo "onyx-lock verify: cannot inspect $repo@$digest: $(head -c 300 "$tmp/error")" >&2; exit 2; }
  if printf '%s' "$raw" | jq -e 'type == "object" and (.manifests | type) == "array"' >/dev/null 2>&1; then
    got_arm="$(printf '%s' "$raw" | jq -r '[.manifests[]? | select(.platform.os == "linux" and .platform.architecture == "arm64") | .digest] | (first // "absent")')"
    got_amd="$(printf '%s' "$raw" | jq -r '[.manifests[]? | select(.platform.os == "linux" and .platform.architecture == "amd64") | .digest] | (first // "absent")')"
    if [ "$got_arm" != "$arm" ] || [ "$got_amd" != "$amd" ]; then
      echo "ONYX-LOCK-012 docker-compose.yml: line \"$value\" pins an index whose linux/arm64 is $got_arm and linux/amd64 is $got_amd, expected $arm and $amd"; return 1
    fi
  elif ! printf '%s' "$raw" | jq -e 'type == "object"' >/dev/null 2>&1; then
    echo "onyx-lock verify: $repo@$digest did not answer with JSON" >&2; exit 2
  elif [ "$digest" != "$arm" ] && [ "$digest" != "$amd" ]; then
    echo "ONYX-LOCK-012 docker-compose.yml: line \"$value\" pins the manifest $digest, expected $arm or $amd"; return 1
  fi
  return 0
}

do_verify() {
  local out rc=0 tool
  # tmp stays global: the EXIT trap runs after this function returns
  set +e; out="$(do_check)"; rc=$?; set -e
  if [ "$rc" -ne 0 ]; then printf '%s\n' "$out"; exit 1; fi
  for tool in curl jq docker; do
    command -v "$tool" >/dev/null 2>&1 || { echo "onyx-lock verify: $tool not found" >&2; exit 2; }
  done
  command -v sha256sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1 || { echo "onyx-lock verify: no sha256sum or shasum" >&2; exit 2; }

  local commit version base mismatches=0 files=0 images=0 refs=0
  tmp="$(mktemp -d "${TMPDIR:-/tmp}/onyx-lock-verify.XXXXXX")"
  trap 'find "$tmp" -delete' EXIT
  commit="$(awk '$1 == "version" { print $3 }' "$file")"
  version="$(awk '$1 == "version" { print $2 }' "$file")"
  base="https://raw.githubusercontent.com/onyx-dot-app/onyx/$commit/deployment"

  local _kind path want got
  while read -r _kind path want; do
    curl -fsSLg --proto '=https' --proto-redir '=https' --retry 2 --max-time 60 -o "$tmp/download" "$base/$path" \
      || { echo "onyx-lock verify: cannot download $base/$path" >&2; exit 2; }
    got="$(sha256_of "$tmp/download")"
    files=$((files + 1))
    if [ "$got" != "$want" ]; then
      echo "ONYX-LOCK-010 $path: expected $want, got $got"; mismatches=$((mismatches + 1))
    fi
    if [ "$path" = "docker_compose/docker-compose.yml" ]; then cp "$tmp/download" "$tmp/compose"; fi
  done < <(awk '$1 == "file"' "$file")

  local repo tag platform last="" raw os arch
  while read -r _kind repo tag platform want; do
    if [ "$repo:$tag" != "$last" ]; then
      raw="$(docker buildx imagetools inspect "$repo:$tag" --raw 2>"$tmp/error" </dev/null)" \
        || { echo "onyx-lock verify: cannot inspect $repo:$tag: $(head -c 300 "$tmp/error")" >&2; exit 2; }
      last="$repo:$tag"
    fi
    os="${platform%/*}"; arch="${platform#*/}"
    got="$(printf '%s' "$raw" | jq -r --arg os "$os" --arg arch "$arch" '[.manifests[]? | select(.platform.os == $os and .platform.architecture == $arch) | .digest] | (first // "absent")')" \
      || { echo "onyx-lock verify: $repo:$tag did not answer with an image index" >&2; exit 2; }
    images=$((images + 1))
    if [ "$got" != "$want" ]; then
      echo "ONYX-LOCK-011 $repo:$tag $platform: expected $want, got $got"; mismatches=$((mismatches + 1))
    fi
  done < <(awk '$1 == "image"' "$file")

  if [ -r "$tmp/compose" ]; then
    local value ref first digest row_tag
    while IFS= read -r value; do
      value="${value#[\"\']}"; value="${value%[\"\']}"   # YAML allows the value quoted
      ref="$(resolve "$value")"
      refs=$((refs + 1))
      if [[ "$ref" == *'${'* ]]; then
        echo "ONYX-LOCK-012 docker-compose.yml: $value has a variable this check can't resolve"; mismatches=$((mismatches + 1)); continue
      fi
      digest=""
      case "$ref" in *@*) digest="${ref#*@}"; ref="${ref%%@*}" ;; esac
      first="${ref%%/*}"
      case "$first" in *.*|*:*|localhost) ;; *) ref="docker.io/$ref" ;; esac
      row_tag=1
      case "${ref##*/}" in *:*) ;; *) if [ -n "$digest" ]; then row_tag=0; else ref="$ref:latest"; fi ;; esac
      if [ -n "$digest" ]; then
        check_pinned_digest "$value" "$ref" "$digest" "$row_tag" || mismatches=$((mismatches + 1))
        continue
      fi
      if ! awk -v r="${ref%:*}" -v t="${ref##*:}" '$1 == "image" && $2 == r && $3 == t { found = 1 } END { exit !found }' "$file"; then
        echo "ONYX-LOCK-012 docker-compose.yml: names $ref, expected an image row for it, got none"; mismatches=$((mismatches + 1))
      fi
    done < <(sed -nE 's/^[[:space:]]*image:[[:space:]]*([^[:space:]#]+).*/\1/p' "$tmp/compose")
    if [ "$refs" -eq 0 ]; then
      echo "ONYX-LOCK-012 docker-compose.yml: extracted 0 image lines, so its images can't be checked"; mismatches=$((mismatches + 1))
    fi
  else
    echo "ONYX-LOCK-012 docker-compose.yml: the lock has no file row for docker_compose/docker-compose.yml, so its images can't be checked"; mismatches=$((mismatches + 1))
  fi

  if [ "$mismatches" -gt 0 ]; then echo "onyx-lock verify: $mismatches mismatch(es)"; exit 1; fi
  echo "onyx-lock verify: ok ($files files, $images image rows, $refs compose image lines)"
}

if [ "$cmd" = "verify" ]; then do_verify; else do_check; fi
