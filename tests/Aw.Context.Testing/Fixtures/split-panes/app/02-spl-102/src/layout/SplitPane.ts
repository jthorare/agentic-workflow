import { withPanes, type Orientation, type PaneLayout } from "./PaneLayout.js";

let nextId = 2;

export function split(layout: PaneLayout, orientation: Orientation): PaneLayout {
  const id = `pane-${nextId++}`;
  const total = layout.panes.reduce((sum, pane) => sum + pane.columns, 0);
  const count = layout.panes.length + 1;
  const panes = [...layout.panes, { id, columns: 0 }].map((pane) => ({ ...pane, columns: Math.floor(total / count) }));
  return { ...withPanes(layout, panes, id), orientation };
}
