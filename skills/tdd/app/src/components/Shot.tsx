import { Raw, cx } from "../lib/raw";
import type { Node } from "../types";
/** A mock or screenshot. Click opens the lightbox (handled by delegation in App: img inside figure.shot). */
export function Shot({ node: n }: { node: Node }) {
  return (
    <figure className={cx("shot", n.size)} data-shot="">
      <img src={n.src} alt={n.alt || ""} width={n.width || undefined} height={n.height || undefined} loading="lazy" decoding="async" />
      {n.caption ? <Raw as="figcaption" html={n.caption} /> : null}
    </figure>
  );
}
