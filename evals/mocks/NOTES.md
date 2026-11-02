# Mock notes

One line per mock: what the canned answer plants or covers. The mock files hold only the canned tool result.

- `playwright/browser_click.md`: Click on any ref succeeds. Clicking Save recipe [ref=e9] is the step that triggers the console error in browser_console_messages.
- `playwright/browser_close.md`: Closes the session.
- `playwright/browser_console_messages.md`: Planted failure: one uncaught TypeError logged after the Save recipe click. The report must name it and the verdict must be FAIL.
- `playwright/browser_fill_form.md`: Fills any fields.
- `playwright/browser_navigate.md`: Canned answer for any URL on localhost:3000.
- `playwright/browser_network_requests.md`: No failed calls; the POST succeeds, so the console error is the only failure signal.
- `playwright/browser_resize.md`: Accepts any width and height.
- `playwright/browser_snapshot.md`: Accessibility tree of /recipes after the Add recipe form is open. Planted: the icon-only button [ref=e14] has no accessible name; the list still shows 2 recipes (the new one is not rendered).
- `playwright/browser_take_screenshot.md`: Returns a file name; no pixels. The skill saves artifacts under the run dir by name.
- `playwright/browser_wait_for.md`: Returns at once.
- `xcodebuildmcp/boot_sim.md`: 
- `xcodebuildmcp/build_run_sim.md`: 
- `xcodebuildmcp/build_sim.md`: 
- `xcodebuildmcp/discover_projs.md`: 
- `xcodebuildmcp/get_app_bundle_id.md`: 
- `xcodebuildmcp/install_app_sim.md`: 
- `xcodebuildmcp/launch_app_sim.md`: 
- `xcodebuildmcp/list_schemes.md`: 
- `xcodebuildmcp/list_sims.md`: The default simulator is already Booted, so boot_sim is not needed.
- `xcodebuildmcp/open_sim.md`: 
- `xcodebuildmcp/screenshot.md`: 
- `xcodebuildmcp/session_show_defaults.md`: Defaults are already set for the Fernly plant-watering app, so the skill needs no discover_projs step.
- `xcodebuildmcp/snapshot_ui.md`: Planted failure: the Snake plant row's water button (AXUniqueId waterToggle-2, an icon-only button) has AXLabel null, so VoiceOver has nothing to read. The accessibility lens must FAIL and the report must name it.
