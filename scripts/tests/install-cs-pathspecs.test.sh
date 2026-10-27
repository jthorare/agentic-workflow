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
done
exit "$fail"
