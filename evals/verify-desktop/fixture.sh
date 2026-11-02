#!/usr/bin/env bash
# Workspace for the verify-desktop cases: a made-up budget-planner desktop app (generic window-layout files), its
# evidence config, a screens manifest with two screens and their mockups. Offline, no remote, four commits;
# the last one reworks SettingsPage.window.json, so the diff maps to the "settings" screen only.
# The stub `ui-evidence` reads .ui-evidence-stub-scenario: "desktop-mismatch" plants one failing screen
# (settings: the footer action bar with Save and Cancel is missing in the app frame, 6.8% over a 2% limit).
# Writes only under $PWD.
set -eu
mkdir -p .agentic-workflow docs/ui/mockups src/BudgetPlanner/Views src/BudgetPlanner/ViewModels
cat > .agentic-workflow/evidence.json <<'P'
{
  "schema": "evidence.repo/1",
  "desktop": {
    "manifest": "docs/ui/screens.json",
    "homeEnv": "BUDGET_HOME",
    "fixture": { "command": ["scripts/fixture.sh"], "up": [["up"]], "down": ["down"] },
    "app": { "command": ["scripts/start-app.sh"] },
    "driver": { "command": ["scripts/aw-ui-driver.sh"] },
    "sizes": ["1440x900"],
    "threshold": 2,
    "timeoutSeconds": 300
  }
}
P
cat > docs/ui/screens.json <<'P'
{
  "schema": "ui-evidence.desktop-screens/1",
  "designRoot": "mockups",
  "threshold": 2,
  "screens": [
    {
      "name": "home",
      "design": "home.html",
      "steps": [
        { "action": "launch" },
        { "action": "screenshot" }
      ]
    },
    {
      "name": "settings",
      "design": "settings.html",
      "steps": [
        { "action": "launch" },
        { "action": "click", "target": { "type": "ListBoxItem", "name": "Settings" }, "expectedState": { "state": "visible" } },
        { "action": "screenshot" }
      ]
    }
  ]
}
P
cat > docs/ui/mockups/home.html <<'P'
<!doctype html><title>Home</title><body style="font-family:sans-serif"><h1>This month</h1><p>Spent 1,240 of 2,000</p></body>
P
cat > docs/ui/mockups/settings.html <<'P'
<!doctype html><title>Settings</title><body style="font-family:sans-serif"><h1>Settings</h1><label>Currency <select><option>EUR</option></select></label>
<footer style="position:fixed;bottom:0;padding:12px"><button>Save</button> <button>Cancel</button></footer></body>
P
cat > src/BudgetPlanner/BudgetPlanner.csproj <<'P'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
</Project>
P
cat > src/BudgetPlanner/Views/HomePage.window.json <<'P'
{ "window": "Home", "children": [
  { "type": "label", "text": "This month", "fontSize": 24 },
  { "type": "label", "bind": "SpentSummary" } ] }
P
cat > src/BudgetPlanner/Views/SettingsPage.window.json <<'P'
{ "window": "Settings", "children": [
  { "type": "label", "text": "Settings", "fontSize": 24 },
  { "type": "select", "bind": "Currency" },
  { "type": "footer", "children": [ { "type": "button", "text": "Save" }, { "type": "button", "text": "Cancel" } ] } ] }
P
cat > src/BudgetPlanner/ViewModels/SettingsViewModel.cs <<'P'
namespace BudgetPlanner.ViewModels;
public sealed class SettingsViewModel { public string Currency { get; set; } = "EUR"; }
P
cat > README.md <<'P'
# BudgetPlanner
Desktop budget planner. Screens and mockups: docs/ui/screens.json.
P
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial budget planner"
printf '\n<!-- v0.2 -->\n' >> README.md
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "docs: note the version"
printf 'namespace BudgetPlanner.ViewModels;\npublic sealed class SettingsViewModel { public string Currency { get; set; } = "EUR"; public bool Dirty { get; set; } }\n' > src/BudgetPlanner/ViewModels/SettingsViewModel.cs
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "settings: track unsaved changes"
cat > src/BudgetPlanner/Views/SettingsPage.window.json <<'P'
{ "window": "Settings", "children": [
  { "type": "label", "text": "Settings", "fontSize": 24 },
  { "type": "select", "bind": "Currency" },
  { "type": "button", "text": "Save", "enabledWhen": "Dirty" } ] }
P
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "settings: move Save next to the currency picker"
printf 'desktop-mismatch\n' > .ui-evidence-stub-scenario
