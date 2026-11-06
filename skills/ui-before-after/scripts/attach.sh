#!/usr/bin/env bash
# Uploads every ./...png and ./...webm that <dir>/body.md references into the PR's description,
# moves the appended video links under their headings, and checks each video's size.
#
#   attach.sh <pr-number> <dir>
#     Prints the PR URL. Exit 1: an upload failed. 6: an uploaded video's size differs from its
#     local file. 64: usage. <dir>/uploaded-body.md and <dir>/final-body.md hold the two bodies.
set -u

REPO="${UI_BEFORE_AFTER_REPO:-vitalizecare/web-app}"
HERE="$(cd "$(dirname "$0")" && pwd)"

[ $# -eq 2 ] && [ -f "$2/body.md" ] || {
  echo "usage: attach.sh <pr-number> <dir containing body.md>" >&2
  exit 64
}
pr="$1"
cd "$2" || exit 64

attach_args=()
videos=()
while IFS=$'\t' read -r kind path; do
  attach_args+=(--attach "$path")
  [ "$kind" = "video" ] && videos+=("$path")
done < <(node "$HERE/attach-body.mjs" plan body.md)

gh pr edit "$pr" -R "$REPO" --body-file body.md ${attach_args[@]+"${attach_args[@]}"} || {
  echo "gh pr edit failed; some uploads may be missing" >&2
  exit 1
}

status=0
if [ ${#videos[@]} -gt 0 ]; then
  gh pr view "$pr" -R "$REPO" --json body --jq .body >uploaded-body.md || exit 1
  urls=$(node "$HERE/attach-body.mjs" place uploaded-body.md final-body.md "${videos[@]}") || exit 1
  gh pr edit "$pr" -R "$REPO" --body-file final-body.md || exit 1
  token=$(gh auth token)
  i=0
  for url in $urls; do
    remote=$(curl -sL -H "Authorization: token $token" -o /dev/null -w '%{size_download}' "$url")
    local_size=$(wc -c <"${videos[$i]}" | tr -d ' ')
    if [ "$remote" != "$local_size" ]; then
      echo "${videos[$i]} is $local_size bytes locally but $url is $remote bytes: wrong order or failed upload" >&2
      status=6
    fi
    i=$((i + 1))
  done
fi

gh pr view "$pr" -R "$REPO" --json url --jq .url
exit "$status"
