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

export interface LayoutSnapshot {
  orientation: Orientation;
  paneIds: string[];
  focused: string;
}

// Sizes are not part of the snapshot: a restored layout splits the width evenly (SPL-104).
export function snapshot(layout: PaneLayout): LayoutSnapshot {
  return { orientation: layout.orientation, paneIds: layout.panes.map((pane) => pane.id), focused: layout.focused };
}

export function restore(saved: LayoutSnapshot, totalColumns: number): PaneLayout {
  const columns = Math.floor(totalColumns / saved.paneIds.length);
  return { orientation: saved.orientation, panes: saved.paneIds.map((id) => ({ id, columns })), focused: saved.focused };
}
