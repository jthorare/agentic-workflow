# split-panes

A small terminal-style app: one window, tabs, and panes inside each tab. It exists only as a test fixture.

- `src/commands/` holds the command registry (every user action is a named command).
- `src/keybindings/` maps keys to commands.
- `settings/default-settings.json` is the settings file; `src/settings/schema.ts` describes it.
- `src/layout/PaneLayout.ts` is the pane layout state that the layout features share.

See `docs/architecture.md`.
