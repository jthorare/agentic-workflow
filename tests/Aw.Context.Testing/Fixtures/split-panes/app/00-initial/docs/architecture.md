# Architecture

The app has one window. A window holds tabs; a tab holds a pane layout.

## State

`PaneLayout` (`src/layout/PaneLayout.ts`) is the single source of truth for how a tab's panes are arranged:
the split orientation, the panes in order, which pane has focus, and each pane's width in columns. Features
change the layout only through the functions in that file.

## Commands and keys

Every action a person can take is a command in the registry (`src/commands/registry.ts`). Keybindings
(`src/keybindings/defaults.ts`) only name commands; they never call layout code directly.

## Settings

`settings/default-settings.json` holds the defaults. A user settings file with the same keys overrides them.
