#!/usr/bin/env bash
# Tests for config/lib/image-paths.sh (aw_image_paths): the one place the hooks decide which image files a text names.
# Run: bash config/lib/tests/image-paths.test.sh
set -uo pipefail
LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/image-paths.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

REPO="$(mktemp -d)"; OUTSIDE="$(mktemp -d)"
trap 'find "$REPO" "$OUTSIDE" -delete 2> /dev/null' EXIT
git -C "$REPO" init -q
png() { printf '\211PNG\r\n\032\n' > "$1"; }
for n in a b c d e; do png "$REPO/$n.png"; done
printf '\377\330\377\340' > "$REPO/photo.jpg"
printf 'not an image' > "$REPO/notes.txt"
png "$OUTSIDE/elsewhere.png"
# aw_image_paths runs in the checkout, like a hook does.
paths() { ( cd "$REPO" && . "$LIB" && aw_image_paths "$1" ) | sed "s|$REPO/||" | tr '\n' ' ' | sed 's/ $//'; }

check "a text that names no image gives nothing" "$(paths "fix the login button")" ""
check "an existing png in the checkout" "$(paths "see $REPO/a.png please")" "a.png"
check "a jpg and a jpeg count too" "$(cp "$REPO/photo.jpg" "$REPO/photo.jpeg"; paths "$REPO/photo.jpg and $REPO/photo.jpeg")" "photo.jpeg photo.jpg"
check "newest first: the last one named leads" "$(paths "$REPO/a.png then $REPO/b.png")" "b.png a.png"
check "a path named twice is listed once" "$(paths "$REPO/a.png $REPO/b.png $REPO/a.png")" "a.png b.png"
check "a path that does not exist is dropped" "$(paths "$REPO/missing.png and $REPO/a.png")" "a.png"
check "a relative path is dropped" "$(paths "look at a.png and ./b.png")" ""
check "a file that is not an image is dropped" "$(paths "$REPO/notes.txt $REPO/a.png")" "a.png"
check "a path outside the checkout is dropped" "$(paths "$OUTSIDE/elsewhere.png $REPO/a.png")" "a.png"
check "at most 4 files, the newest 4" "$(paths "$REPO/a.png $REPO/b.png $REPO/c.png $REPO/d.png $REPO/e.png")" "e.png d.png c.png b.png"

head -c 3000000 /dev/zero > "$REPO/big1.png"; head -c 3000000 /dev/zero > "$REPO/big2.png"
check "4 MiB in all: the newest fits, the one that would pass 4 MiB is left out" "$(paths "$REPO/big1.png $REPO/big2.png")" "big2.png"

exit $fail
