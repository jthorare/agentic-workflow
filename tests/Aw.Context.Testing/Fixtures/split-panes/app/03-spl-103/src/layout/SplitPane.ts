import { withPanes, type Orientation, type PaneLayout } from "./PaneLayout.js";

let nextId = 2;

export function split(layout: PaneLayout, orientation: Orientation): PaneLayout {
  const id = `pane-${nextId++}`;
  const total = layout.panes.reduce((sum, pane) => sum + pane.columns, 0);
  const count = layout.panes.length + 1;
  const panes = [...layout.panes, { id, columns: 0 }].map((pane) => ({ ...pane, columns: Math.floor(total / count) }));
  return { ...withPanes(layout, panes, id), orientation };
}

// Writes the layout's orientation. The focused pane moves to the front so the cursor never jumps
// (decision in SPL-103, docs/decisions/0001-orientation-toggle.md).
export function setOrientation(layout: PaneLayout, orientation: Orientation): PaneLayout {
  const focused = layout.panes.filter((pane) => pane.id === layout.focused);
  const others = layout.panes.filter((pane) => pane.id !== layout.focused);
  return { ...withPanes(layout, [...focused, ...others], layout.focused), orientation };
}

export function toggleOrientation(layout: PaneLayout): PaneLayout {
  return setOrientation(layout, layout.orientation === "vertical" ? "horizontal" : "vertical");
}
