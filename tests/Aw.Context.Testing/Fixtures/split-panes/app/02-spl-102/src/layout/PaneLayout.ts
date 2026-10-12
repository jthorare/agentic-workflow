export type Orientation = "vertical" | "horizontal";

export interface Pane {
  id: string;
  columns: number;
}

export interface PaneLayout {
  orientation: Orientation;
  panes: Pane[];
  focused: string;
}

export function emptyLayout(): PaneLayout {
  return { orientation: "vertical", panes: [{ id: "pane-1", columns: 80 }], focused: "pane-1" };
}

export function withPanes(layout: PaneLayout, panes: Pane[], focused: string): PaneLayout {
  return { ...layout, panes, focused };
}
