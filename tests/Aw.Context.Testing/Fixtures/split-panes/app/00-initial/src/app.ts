import { emptyLayout, type PaneLayout } from "./layout/PaneLayout.js";

export interface AppState {
  layout: PaneLayout;
}

export function startApp(): AppState {
  return { layout: emptyLayout() };
}
