import { restore, snapshot, type LayoutSnapshot, type PaneLayout } from "../layout/PaneLayout.js";

export interface Tab {
  id: string;
  saved: LayoutSnapshot;
}

export class TabManager {
  private readonly tabs: Tab[] = [];
  private active = 0;

  constructor(private readonly totalColumns: number) {}

  open(id: string, layout: PaneLayout): void {
    this.tabs.push({ id, saved: snapshot(layout) });
  }

  // Reads the pane layout state: switching tabs saves the current layout and restores the target's.
  switchTo(index: number, current: PaneLayout): PaneLayout {
    const from = this.tabs[this.active];
    const to = this.tabs[index];
    if (from === undefined || to === undefined) {
      throw new Error(`no tab at ${index}`);
    }
    from.saved = snapshot(current);
    this.active = index;
    return this.restoreLayout(to);
  }

  restoreLayout(tab: Tab): PaneLayout {
    return restore(tab.saved, this.totalColumns);
  }
}
