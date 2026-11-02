#!/usr/bin/env bash
# Workspace for the verify-app cases. Offline on purpose: the dispatcher must pick a sub-skill from the files alone,
# and the run stops (small max_turns) before the sub-skill can reach for a server or a simulator.
# Usage: fixture.sh web|web-trap|ios   (default web). web-trap adds vendored iOS files that platform-detection must ignore.
set -eu
AW_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/../_lib" && pwd)"
. "$AW_LIB/shim.sh"
shim_toolkit
case "${1:-web}" in
  web|web-trap)
    cat > package.json <<'P'
{
  "name": "book-club-tracker",
  "private": true,
  "scripts": { "dev": "vite" },
  "dependencies": { "react": "19.0.0", "react-dom": "19.0.0" },
  "devDependencies": { "vite": "6.0.0" }
}
P
    if [ "${1:-web}" = web-trap ]; then
      mkdir -p vendor/swift-lib node_modules/native-demo/Demo.xcodeproj
      echo '// swift-tools-version:5.9' > vendor/swift-lib/Package.swift
      echo '{}' > node_modules/native-demo/Demo.xcodeproj/project.pbxproj
    fi
    ;;
  ios)
    mkdir -p PlantWater.xcodeproj
    printf '// !$*UTF8*$!\n{ archiveVersion = 1; objects = {}; rootObject = ""; }\n' > PlantWater.xcodeproj/project.pbxproj
    ;;
  *) echo "fixture.sh: unknown platform $1" >&2; exit 1;;
esac
git init -q . 2>/dev/null || true
