// A tiny event bus so body components and raw-HTML delegation can drive the overlays (ref card, ref panel, hotspot card,
// lightbox, schema drawer) without re-rendering the document tree. Server render never emits.
type Handler = (payload: any) => void;
const handlers: Record<string, Set<Handler>> = {};
export const bus = {
  on(ev: string, fn: Handler): () => void { (handlers[ev] ||= new Set()).add(fn); return () => { handlers[ev]?.delete(fn); }; },
  emit(ev: string, payload?: any) { if (typeof document === "undefined") return; handlers[ev]?.forEach((fn) => fn(payload)); },
};
/** Open every <details> above el so an anchor inside a fold is visible, then flash a table row. */
export function reveal(id: string) {
  const el = document.getElementById(id);
  if (!el) return false;
  let p: HTMLElement | null = el;
  while (p) { if (p instanceof HTMLDetailsElement && !p.open) p.open = true; p = p.parentElement; }
  if (el.tagName === "TR") { el.classList.add("lit"); setTimeout(() => el.classList.remove("lit"), 2400); }
  return true;
}
