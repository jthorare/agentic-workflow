#!/usr/bin/env bash
# Saves one Figma node as a PNG through Figma's REST API, for when the Figma MCP is not connected.
#
#   figma-frame.sh <figma-url-with-node-id> <out.png>
#     The token comes from FIGMA_TOKEN or the macOS keychain item "figma-read-token".
#     Exit 7: no token. 1: Figma did not return an image. 64: usage, or the link has no node-id.
set -u

KEYCHAIN_ITEM="${UI_BEFORE_AFTER_FIGMA_KEYCHAIN:-figma-read-token}"

[ $# -eq 2 ] || { echo "usage: figma-frame.sh <figma-url-with-node-id> <out.png>" >&2; exit 64; }
url="$1"
out="$2"

# A branch link's file key is its branch key.
file_key=$(echo "$url" | sed -n 's|.*/branch/\([A-Za-z0-9]*\)/.*|\1|p')
[ -n "$file_key" ] || file_key=$(echo "$url" | sed -n 's|.*/design/\([A-Za-z0-9]*\).*|\1|p')
node_id=$(echo "$url" | sed -n 's|.*[?&]node-id=\([0-9]*\)[-:]\([0-9]*\).*|\1:\2|p')
if [ -z "$file_key" ] || [ -z "$node_id" ]; then
  echo "need a figma.com/design link with a node-id, got: $url" >&2
  exit 64
fi

token="${FIGMA_TOKEN:-$(security find-generic-password -s "$KEYCHAIN_ITEM" -w 2>/dev/null)}"
if [ -z "$token" ]; then
  echo "No Figma token. Make a personal access token with only file_content:read" >&2
  echo "(Figma > Settings > Security), then run:" >&2
  echo "  security add-generic-password -s $KEYCHAIN_ITEM -a \"\$USER\" -w" >&2
  exit 7
fi

response=$(curl -fsS -H "X-Figma-Token: $token" "https://api.figma.com/v1/images/$file_key?ids=$node_id&format=png&scale=2") || exit 1
image_url=$(echo "$response" | sed -n 's|.*"images":{"[^"]*":"\([^"]*\)".*|\1|p')
if [ -z "$image_url" ]; then
  echo "Figma returned no image for $node_id: $response" >&2
  exit 1
fi
curl -fsS -L -o "$out" "$image_url" || exit 1
