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

# A call that left files out writes one line to the probe log (only when the probe directory exists), with what it sent and dropped.
PROBE="$(mktemp -d)"
trap 'find "$REPO" "$OUTSIDE" "$PROBE" -delete 2> /dev/null' EXIT
( cd "$REPO" && . "$LIB" && AW_PROBE_DIR="$PROBE" aw_image_paths "$REPO/a.png $REPO/b.png $REPO/c.png $REPO/d.png $REPO/e.png" probe-test > /dev/null )
check "probe-log: one line with the sent and dropped counts" "$(jq -c 'select(.event == "image-paths") | .input' "$PROBE/image-paths.jsonl" 2> /dev/null)" '{"hook":"probe-test","sent":4,"dropped":1}'
( cd "$REPO" && . "$LIB" && AW_PROBE_DIR="$PROBE" aw_image_paths "$REPO/a.png" probe-test > /dev/null )
check "probe-log: nothing is logged when nothing was left out" "$(wc -l < "$PROBE/image-paths.jsonl" | tr -d ' ')" "1"
( cd "$REPO" && . "$LIB" && AW_PROBE_DIR="$PROBE/none" aw_image_paths "$REPO/a.png $REPO/b.png $REPO/c.png $REPO/d.png $REPO/e.png" > /dev/null )
check "probe-log: no probe directory, no file" "$([ -e "$PROBE/none" ] && echo made || echo none)" "none"

# aw_images_attach: the input is untouched without paths, and gains images + imagesBestEffort with them.
check "attach: no paths leaves the input byte for byte" "$(. "$LIB"; aw_images_attach '{"text":"x","senderKind":"main"}' "")" '{"text":"x","senderKind":"main"}'
check "attach: paths are added after the existing keys" "$(. "$LIB"; aw_images_attach '{"text":"x"}' "$(printf '/a.png\n/b.png')")" '{"text":"x","images":["/a.png","/b.png"],"imagesBestEffort":true}'

# Symlinks are followed before the checkout rule: a link that leaves the checkout is dropped, one that stays is kept.
ln -s "$OUTSIDE/elsewhere.png" "$REPO/link-out.png"
ln -s "$OUTSIDE" "$REPO/outdir"
ln -s "$REPO/a.png" "$REPO/link-in.png"
mkdir "$REPO/sub"; ln -s ../b.png "$REPO/sub/rel-in.png"
check "symlink in the checkout to a file outside: dropped" "$(paths "$REPO/link-out.png")" ""
check "symlinked directory in the checkout that points out: dropped" "$(paths "$REPO/outdir/elsewhere.png")" ""
check "symlink that stays inside the checkout: kept" "$(paths "$REPO/link-in.png")" "link-in.png"
check "relative symlink that stays inside: kept" "$(paths "$REPO/sub/rel-in.png")" "sub/rel-in.png"
# A relative ".." link inside a symlinked directory is resolved against the real directory, not the path text.
mkdir "$OUTSIDE/inner"; png "$OUTSIDE/secret.png"; ln -s ../secret.png "$OUTSIDE/inner/x.png"; ln -s "$OUTSIDE/inner" "$REPO/indir"
check "relative .. link inside a symlinked directory that points out: dropped" "$(paths "$REPO/indir/x.png")" ""

exit $fail
