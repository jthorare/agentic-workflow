import { useEffect, useState } from "react";
import { useContent } from "../lib/context";
import { cx } from "../lib/raw";
/** The floating numbered contents rail. Sections only until hovered or opened; the active one follows the scroll. */
export function Rail() {
  const { nav } = useContent();
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState<string>(nav[0]?.id || "");
  useEffect(() => {
    const secs = nav.map((s) => document.getElementById(s.id)).filter(Boolean) as HTMLElement[];
    if (!secs.length || !("IntersectionObserver" in window)) return;
    const seen = new Map<string, number>();
    const io = new IntersectionObserver((entries) => {
      for (const e of entries) seen.set((e.target as HTMLElement).id, e.isIntersecting ? e.boundingClientRect.top : Infinity);
      let best = "", top = Infinity;
      for (const s of secs) { const t = seen.get(s.id); if (t !== undefined && t < top) { top = t; best = s.id; } }
      if (best) setActive(best);
    }, { rootMargin: "-10% 0px -70% 0px", threshold: [0, 0.01] });
    secs.forEach((s) => io.observe(s));
    const onScroll = () => { if (window.scrollY < 40 && secs[0]) setActive(secs[0].id); };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => { io.disconnect(); window.removeEventListener("scroll", onScroll); };
  }, [nav]);
  return (
    <nav className={cx("fnav", open && "open")} aria-label="Contents">
      <button type="button" className="fnav-toggle" aria-expanded={open} aria-label="Contents" onClick={() => setOpen((o) => !o)}><span aria-hidden="true">☰</span></button>
      <div className="fnav-panel">
        <div className="fnav-h">Contents</div>
        <ol>
          {nav.map((s) => (
            <li key={s.id}>
              <a href={`#${s.id}`} className={cx(s.id === active && "active")} onClick={() => setOpen(false)}><span className="n">{s.num}</span><span className="l">{s.title}</span></a>
              {s.subs?.length ? <ol className="sub">{s.subs.map((u) => <li key={u.id}><a href={`#${u.id}`} onClick={() => setOpen(false)}>{u.title}</a></li>)}</ol> : null}
            </li>
          ))}
        </ol>
      </div>
    </nav>
  );
}
