#!/usr/bin/env bash
# aw:image-paths — which PNG/JPEG files a hook's text names, for the judge's `images` input (judge-images spec 5.2).
# Source it; it defines aw_image_paths and prints nothing else.
#
#   aw_image_paths <text>   one absolute path per line: the existing .png/.jpg/.jpeg files the text names, deduped, newest
#                           (last named) first, kept only under the checkout of the current directory, at most 4 files and
#                           4 MiB in all. Nothing for a text that names none. Always returns 0.
#
# Not written yet: this stub names the contract (config/lib/tests/image-paths.test.sh) and prints nothing.
aw_image_paths() {
  return 0
}
