#!/usr/bin/env bash
# Offline fixture for design-mockup-web. Usage: fixture.sh <web|ios>. Writes only under $PWD; no URLs, no server.
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
# Design state the skills look for (AW_DIR is pointed here by case.yaml env).
mkdir -p .aw-fixture/design
printf '{"color":{"primary":{"$value":"#2f6f4f","$type":"color"}},"spacing":{"md":{"$value":"16px","$type":"dimension"}}}\n' > design-tokens.json
# no screens.json yet: mockups come first.
