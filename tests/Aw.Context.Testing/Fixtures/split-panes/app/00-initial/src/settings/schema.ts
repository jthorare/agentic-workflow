export interface Settings {
  fontSize: number;
  theme: "dark" | "light";
}

export const defaultSettings: Settings = { fontSize: 13, theme: "dark" };
