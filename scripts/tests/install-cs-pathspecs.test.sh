#!/usr/bin/env bash
# Guard for scripts/install-cs.sh (#1027): every project a published binary builds, followed through its ProjectReference
# chain, sits inside the pathspecs install-cs copies into its build tree (SINDRI_BUILD_PATHSPECS plus its own extras). A new
# reference outside that list fails here, not as MSB9008 on an owner's box. Reads the real checkout; writes nothing.
# Run: bash scripts/tests/install-cs-pathspecs.test.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
fail=0
# shellcheck source=../lib/sindri-build-inputs.sh
. "$ROOT/scripts/lib/sindri-build-inputs.sh"
extras_line="$(grep -E '^PATHSPECS=\(' "$ROOT/scripts/install-cs.sh")"
[ -n "$extras_line" ] || { echo "not ok - install-cs.sh has no PATHSPECS=( line"; exit 1; }
read -r -a extras <<< "$(printf '%s' "$extras_line" | sed -E 's/^PATHSPECS=\(\"\$\{SINDRI_BUILD_PATHSPECS\[@\]\}\" *//; s/\)$//')"
dirs=()
for p in "${SINDRI_BUILD_PATHSPECS[@]}" "${extras[@]}"; do case "$p" in src/*) dirs+=("${p%/}") ;; esac; done

# embedded_covered <repo-relative path>: a file or glob a project embeds sits under one of the pathspecs (any directory, not only src/)
embedded_covered() {
  local p
  for p in "${SINDRI_BUILD_PATHSPECS[@]}" "${extras[@]}"; do case "$1" in "${p%/}"/*) return 0 ;; esac; done
  return 1
}

covered() { # covered <repo-relative path>
  local d
  for d in "${dirs[@]}"; do case "$1" in "$d"/*) return 0 ;; esac; done
  return 1
}

published=()
while IFS= read -r p; do published+=("$p"); done < <(grep -oE '^ *pub [a-z-]+ [^ ]+\.csproj' "$ROOT/scripts/install-cs.sh" | awk '{print $3}')
[ "${#published[@]}" -gt 0 ] || { echo "not ok - found no 'pub <name> <csproj>' lines in install-cs.sh"; exit 1; }

# normalize <repo-relative path>: resolves . and .. segments in text (bash 3.2 has no associative arrays, and realpath -m is GNU-only)
normalize() {
  local out="" rest="$1" seg
  while [ -n "$rest" ]; do
    seg="${rest%%/*}"
    if [ "$rest" = "$seg" ]; then rest=""; else rest="${rest#*/}"; fi
    case "$seg" in
      ''|.) ;;
      ..) case "$out" in ''|..|*/..) out="${out:+$out/}.." ;; */*) out="${out%/*}" ;; *) out="" ;; esac ;;
      *) out="${out:+$out/}$seg" ;;
    esac
  done
  printf '%s\n' "$out"
}

# The model-check runner install-cs copies to tlc/ (TLC_FILES) is in the pathspecs, so a change to it republishes.
read -r -a tlc_files <<< "$(grep -E '^TLC_FILES=\(' "$ROOT/scripts/install-cs.sh" | sed -E 's/^TLC_FILES=\(//; s/\).*$//')"
[ "${#tlc_files[@]}" -gt 0 ] || { echo "not ok - install-cs.sh has no TLC_FILES=( line"; fail=1; }
for f in ${tlc_files[@]+"${tlc_files[@]}"}; do
  if [ ! -f "$ROOT/$f" ]; then echo "not ok - $f (TLC_FILES) does not exist"; fail=1
  elif printf '%s\n' "${SINDRI_BUILD_PATHSPECS[@]}" | grep -Fxq -- "$f"; then echo "ok - $f is in SINDRI_BUILD_PATHSPECS"
  else echo "not ok - $f is copied to tlc/ but is not in SINDRI_BUILD_PATHSPECS"; fail=1; fi
done

seen=""
queue=("${published[@]}")
while [ "${#queue[@]}" -gt 0 ]; do
  proj="${queue[0]}"; queue=("${queue[@]:1}")
  grep -Fxq -- "$proj" <<< "$seen" && continue
  seen="$seen$proj"$'\n'
  if [ ! -f "$ROOT/$proj" ]; then echo "not ok - $proj does not exist"; fail=1; continue; fi
  if covered "$proj"; then echo "ok - $proj is inside the build pathspecs"; else
    echo "not ok - $proj is outside the build pathspecs (add its src/ directory to SINDRI_BUILD_PATHSPECS)"; fail=1
  fi
  base="$(dirname "$proj")"
  while IFS= read -r ref; do
    [ -n "$ref" ] || continue
    ref="${ref//\\//}"
    queue+=("$(normalize "$base/$ref")")
  done < <(grep -oE '<ProjectReference[^>]*Include="[^"]+"' "$ROOT/$proj" | sed -E 's/.*Include="([^"]+)"/\1/')
  # An embedded file outside the project's own directory (judge's labeled screenshots) must be copied into the build tree too, or
  # the glob matches nothing there and the published binary ships without it.
  while IFS= read -r embed; do
    [ -n "$embed" ] || continue
    embed="$(normalize "$base/${embed//\\//}")"
    case "$embed" in "$base"/*) continue ;; esac
    if embedded_covered "$embed"; then echo "ok - $proj embeds $embed, inside the build pathspecs"; else
      echo "not ok - $proj embeds $embed, outside the build pathspecs (add its directory to SINDRI_BUILD_PATHSPECS)"; fail=1
    fi
  done < <(grep -oE '<EmbeddedResource[^>]*Include="[^"]+"' "$ROOT/$proj" | sed -E 's/.*Include="([^"]+)"/\1/')
done
exit "$fail"
