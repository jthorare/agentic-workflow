import type { CommandId } from "../commands/registry.js";

export const defaultKeybindings: Record<string, CommandId> = {
  "ctrl+shift+t": "new-tab",
  "ctrl+shift+w": "close-tab",
};
