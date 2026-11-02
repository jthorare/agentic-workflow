#!/usr/bin/env bash
# Workspace for the verify-ios cases: a small plant-watering SwiftUI app (Fernly). Offline, nothing to build.
set -eu
AW_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/../_lib" && pwd)"
. "$AW_LIB/fixture-lib.sh"
mkdir -p Fernly.xcodeproj Fernly
echo '// !$*UTF8*$!' > Fernly.xcodeproj/project.pbxproj
cat > AGENTS.md <<'P'
# Fernly
A plant-watering iOS app. Scheme Fernly, bundle id com.example.fernly, simulator iPhone 16 (already booted).
P
cat > Fernly/TodayView.swift <<'P'
import SwiftUI

struct TodayView: View {
    let plants: [Plant]
    var body: some View {
        List(plants) { plant in
            HStack {
                Text(plant.name)
                Spacer()
                Button(action: { plant.markWatered() }) { Image(systemName: "drop.fill") }
            }
        }
        .navigationTitle("Today")
    }
}
P
aw_fixture_repo "Today list"
