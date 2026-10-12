import { withPanes, type PaneLayout } from "./PaneLayout.js";

// The narrowest a pane may get, in columns (SPL-105).
export const MIN_PANE_COLUMNS = 10;

export type Direction = "left" | "right";

// Moves the border on the focused pane's right by `step` columns, taking the width from its neighbour.
export function resizeFocused(layout: PaneLayout, direction: Direction, step: number): PaneLayout {
  const index = layout.panes.findIndex((pane) => pane.id === layout.focused);
  const neighbour = layout.panes[index + 1];
  const focused = layout.panes[index];
  if (focused === undefined || neighbour === undefined) {
    return layout;
  }
  const delta = direction === "right" ? step : -step;
  const grown = focused.columns + delta;
  const shrunk = neighbour.columns - delta;
  if (grown < MIN_PANE_COLUMNS || shrunk < MIN_PANE_COLUMNS) {
    return layout;
  }
  const panes = layout.panes.map((pane, i) =>
    i === index ? { ...pane, columns: grown } : i === index + 1 ? { ...pane, columns: shrunk } : pane);
  return withPanes(layout, panes, layout.focused);
}
