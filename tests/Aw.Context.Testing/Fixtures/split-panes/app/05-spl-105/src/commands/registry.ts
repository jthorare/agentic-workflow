import type { AppState } from "../app.js";
import { resizeFocused } from "../layout/PaneResize.js";
import { split, toggleOrientation } from "../layout/SplitPane.js";
import { defaultSettings } from "../settings/schema.js";

export type CommandId =
  | "new-tab"
  | "close-tab"
  | "next-tab"
  | "previous-tab"
  | "split-vertical"
  | "split-horizontal"
  | "toggle-orientation"
  | "resize-pane-left"
  | "resize-pane-right";

export interface Command {
  id: CommandId;
  title: string;
  run(state: AppState): AppState;
}

const commands = new Map<CommandId, Command>();

export function register(command: Command): void {
  commands.set(command.id, command);
}

export function runCommand(id: CommandId, state: AppState): AppState {
  const command = commands.get(id);
  if (command === undefined) {
    throw new Error(`unknown command: ${id}`);
  }
  return command.run(state);
}

export function listCommands(): Command[] {
  return [...commands.values()];
}

export function registerLayoutCommands(): void {
  const step = defaultSettings.paneResizeStep;
  register({ id: "split-vertical", title: "Split pane vertically", run: (s) => ({ ...s, layout: split(s.layout, "vertical") }) });
  register({ id: "split-horizontal", title: "Split pane horizontally", run: (s) => ({ ...s, layout: split(s.layout, "horizontal") }) });
  register({ id: "toggle-orientation", title: "Toggle split orientation", run: (s) => ({ ...s, layout: toggleOrientation(s.layout) }) });
  register({ id: "resize-pane-left", title: "Shrink focused pane", run: (s) => ({ ...s, layout: resizeFocused(s.layout, "left", step) }) });
  register({ id: "resize-pane-right", title: "Grow focused pane", run: (s) => ({ ...s, layout: resizeFocused(s.layout, "right", step) }) });
}
