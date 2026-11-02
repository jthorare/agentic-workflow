#!/usr/bin/env bash
# Offline fixture: a web app and an iOS app side by side, no server, no URLs.
set -euo pipefail
mkdir -p web/src ios/Plants
cat > web/package.json <<'EOF'
{ "name": "plants-web", "private": true, "dependencies": { "react": "18.3.1", "vite": "5.4.0" } }
EOF
cat > web/src/Reminders.tsx <<'EOF'
export function Reminders() { return <main><h1>Reminders</h1></main>; }
EOF
cat > ios/Plants/ReminderView.swift <<'EOF'
import SwiftUI
struct ReminderView: View { var body: some View { Text("Reminders") } }
EOF
