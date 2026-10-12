export interface Settings {
  fontSize: number;
  theme: "dark" | "light";
  // Columns moved per resize-pane keypress.
  paneResizeStep: number;
}

export const defaultSettings: Settings = { fontSize: 13, theme: "dark", paneResizeStep: 2 };
