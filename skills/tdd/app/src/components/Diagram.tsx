import { useState } from "react";
import { Raw, cx } from "../lib/raw";
import type { Hotspot, Node } from "../types";

export function Legend({ items }: { items: { label: string; tone: string }[] }) {
  return <span className="key">{items.map((k, i) => <span key={i}><i className={cx("sw", k.tone)} />{<Raw html={k.label} />}</span>)}</span>;
}

/** A diagram figure: the SVG/PNG as <img>, hotspot anchors in percent over it, a zoom toggle, legend and reading line. */
export function Diagram({ node: n }: { node: Node }) {
  const [zoomed, setZoomed] = useState(false);
  return (
    <figure className={cx("diagram", n.fit && !zoomed && "fit", zoomed && "zoomed")} id={n.id} data-key={n.key}>
      <div className="bar">
        <Raw className="fig-title" html={n.title} />
        <button type="button" className="zoom" aria-pressed={zoomed} onClick={() => setZoomed((z) => !z)}>{zoomed ? "Fit" : "Actual size"}</button>
      </div>
      <div className="view">
        <div className="stage">
          <img src={n.src} alt={n.alt} width={n.width || undefined} height={n.height || undefined} loading="lazy" decoding="async" />
          {(n.hotspots || []).map((h: Hotspot, i: number) => <HotspotA h={h} key={i} />)}
        </div>
      </div>
      {(n.legend?.length || n.reading) ? (
        <figcaption>
          {n.legend?.length ? <Legend items={n.legend} /> : null}
          {n.reading ? <Raw className="reading" html={n.reading} /> : null}
        </figcaption>
      ) : null}
    </figure>
  );
}

function HotspotA({ h }: { h: Hotspot }) {
  const style = { left: `${h.x}%`, top: `${h.y}%`, width: `${h.w}%`, height: `${h.h}%` };
  const href = h.href || (h.table ? `#${h.table}` : undefined);
  const Tag = (href ? "a" : "span") as any;
  return (
    <Tag className={cx("hs", h.table && "has-table", h.image && "has-image")} href={href} style={style} tabIndex={href ? undefined : 0}
      data-hs="" data-label={h.label} data-note={h.note || undefined} data-image={h.image || undefined} data-table={h.table || undefined} data-tone={h.tone || undefined}
      aria-label={h.table ? `${h.label}: open table details` : h.label}>
      <span className="sr-only">{h.label}</span>
    </Tag>
  );
}
