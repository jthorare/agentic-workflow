#!/usr/bin/env bash
# Offline workspace for the dispatcher cases. Writes only under $PWD; nothing here fetches anything.
#   $1 = web          package.json + README (a React app, no tokens yet)
#   $1 = webtokens    web + design-tokens.json + .impeccable.md (a design language already exists)
#   $1 = ios          Package.swift + Sources/PlantWater/Theme.swift (no tokens yet)
#   $1 = ios-tokens   ios + design-tokens.json
set -eu
variant="${1:-web}"
tokens() {
  cat > design-tokens.json <<'J'
{
  "color": {
    "$type": "color",
    "primary": { "$value": "#2f7d4f" },
    "surface": { "$value": "#fbfaf6" }
  },
  "spacing": { "$type": "dimension", "md": { "$value": "16px" } }
}
J
}
web() {
  cat > package.json <<'J'
{ "name": "recipe-app", "private": true, "dependencies": { "react": "18.3.1", "next": "14.2.5" } }
J
  printf '# Recipe app\n\nA small recipe collection site.\n' > README.md
}
ios() {
  mkdir -p Sources/PlantWater
  cat > Package.swift <<'P'
// swift-tools-version:5.9
import PackageDescription
let package = Package(name: "PlantWater", targets: [.target(name: "PlantWater")])
P
  cat > Sources/PlantWater/Theme.swift <<'P'
import SwiftUI
enum Theme {
    static let leaf = Color(red: 0.18, green: 0.49, blue: 0.31)
    static let soil = Color(red: 0.36, green: 0.25, blue: 0.20)
    static let spacingMedium: CGFloat = 16
    static let title = Font.system(size: 28, weight: .bold)
}
P
}
case "$variant" in
  web) web ;;
  webtokens) web; tokens; printf '# Design context\n\nCalm, friendly, paper-like surfaces.\n' > .impeccable.md ;;
  ios) ios ;;
  ios-tokens) ios; tokens ;;
  *) echo "unknown variant: $variant" >&2; exit 1 ;;
esac
