#!/usr/bin/env bash
# Offline fixture for design-implement. Usage: fixture.sh <web|ios>. Writes only under $PWD; no URLs, no server.
set -euo pipefail
kind="${1:-web}"
case "$kind" in
  web)
    printf '{"name":"recipe-box","private":true,"dependencies":{"next":"14.2.0","react":"18.3.0"}}\n' > package.json
    mkdir -p src/app && printf 'export default function Page() { return <main /> }\n' > src/app/page.tsx ;;
  ios)
    printf '// swift-tools-version:5.9\nimport PackageDescription\nlet package = Package(name: "PlantWater", targets: [.target(name: "PlantWater")])\n' > Package.swift
    mkdir -p Sources/PlantWater && printf 'import SwiftUI\nstruct RootView: View { var body: some View { Text("hi") } }\n' > Sources/PlantWater/RootView.swift ;;
  *) echo "unknown kind: $kind" >&2; exit 1 ;;
esac
# Design state the skills look for: the preamble sets AW_DIR=$AW_STATE_DIR/<repo slug>, the slug is the workspace dir name (no remote), AW_STATE_DIR=<workspace>/.aw-state under the runner.
AWD="$PWD/.aw-state/$(basename "$PWD")/design"
mkdir -p "$AWD"
printf '{"color":{"primary":{"$value":"#2f6f4f","$type":"color"}},"spacing":{"md":{"$value":"16px","$type":"dimension"}}}\n' > design-tokens.json
# Approved mockup baseline for every screen the cases name, so the mockup-first gate passes.
for s in saved-recipes login pricing settings onboarding streak; do
  : > "$AWD/mockup-web-$s.png"; : > "$AWD/mockup-ios-$s.png"; : > "$AWD/mockup-$s.html"
done
printf '{"screens":{' > "$AWD/screens.json"
first=1
for s in saved-recipes login pricing settings onboarding streak; do
  [ $first = 1 ] || printf ',' >> "$AWD/screens.json"; first=0
  printf '"%s":{"approved_at":"2026-10-30T10:00:00Z","baselines":{"web":"mockup-web-%s.png","ios":"mockup-ios-%s.png"}}' "$s" "$s" "$s" >> "$AWD/screens.json"
done
printf '}}\n' >> "$AWD/screens.json"
