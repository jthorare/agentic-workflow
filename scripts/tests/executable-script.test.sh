#!/usr/bin/env bash
# A test that writes a shell script and runs it must write it through ExecutableScript.Write (tests/Aw.Kernel.Testing): a script
# written by hand and made executable can fail to start with ETXTBSY ('text file busy') while a parallel test forks (#1251).
# A test .cs file that holds a '#!' line and sets the exec bit (SetUnixFileMode with UserExecute, or chmod +x) without calling
# ExecutableScript is flagged. Allow-list: a file that deliberately needs the file to be busy or unrunnable, one reason each.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ALLOW=(
  # (empty: add "tests/<path>.cs" here with its reason when a test needs the busy or unrunnable file)
)
fail=0
while IFS= read -r file; do
  rel="${file#"$ROOT"/}"
  skip=0
  for allowed in "${ALLOW[@]+"${ALLOW[@]}"}"; do [ "$rel" = "$allowed" ] && skip=1; done
  [ "$skip" -eq 0 ] || continue
  grep -q 'ExecutableScript' "$file" && continue
  grep -q '#!/' "$file" || continue
  if grep -qE 'SetUnixFileMode\(.*Execute|chmod \+x|chmod 7' "$file"; then
    echo "FAIL: $rel writes a '#!' script and sets the exec bit by hand; use ExecutableScript.Write"
    fail=1
  fi
done < <(find "$ROOT/tests" -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*')
[ "$fail" -eq 0 ] || exit 1
echo "PASS: tests write executable scripts through ExecutableScript"
