// Harness client: hydrates the server-rendered (closed, empty) drawer, stands in for App's click delegation on diagram
// hotspots that carry a table, and exposes window.__harness for the screenshot script. Bundled by harness/render.tsx.
import { useEffect } from "react";
import { hydrateRoot } from "react-dom/client";
import { bus } from "../src/lib/bus";
import { ContentCtx } from "../src/lib/context";
import { SchemaDrawer } from "../src/components/SchemaDrawer";

const w = window as any;
const errors: string[] = [];
w.__harness = { ready: false, errors, emit: (id: string) => bus.emit("table", { id }) };

function Ready() {
  useEffect(() => { w.__harness.ready = true; }, []);
  return null;
}

const content = JSON.parse(document.getElementById("tdd-data")!.textContent || "{}");
hydrateRoot(
  document.getElementById("drawer-root")!,
  <ContentCtx.Provider value={content}><SchemaDrawer /><Ready /></ContentCtx.Provider>,
  { onRecoverableError: (e: any) => errors.push(String(e?.message || e)) },
);

// The App does this in production (CONTRACTS.md, bus event "table"): a click on [data-hs][data-table] opens the drawer.
document.addEventListener("click", (e) => {
  const a = (e.target as HTMLElement | null)?.closest?.("[data-hs][data-table]") as HTMLElement | null;
  if (!a) return;
  e.preventDefault();
  bus.emit("table", { id: a.dataset.table });
});
