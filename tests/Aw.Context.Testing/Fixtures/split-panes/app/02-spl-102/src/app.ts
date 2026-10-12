import { register, registerLayoutCommands } from "./commands/registry.js";
import { emptyLayout, type PaneLayout } from "./layout/PaneLayout.js";

export interface AppState {
  layout: PaneLayout;
}

export function startApp(): AppState {
  register({ id: "new-tab", title: "New tab", run: (state) => state });
  register({ id: "close-tab", title: "Close tab", run: (state) => state });
  registerLayoutCommands();
  return { layout: emptyLayout() };
}
