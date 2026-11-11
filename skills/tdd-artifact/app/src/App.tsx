// The TDD page: masthead, lede, numbered sections of blocks, footer in one .wrap column; the contents rail and the
// overlays outside it. Pure render from content (no Date, random or window during render), hydrated by client.tsx.
import { useRef } from "react";
import type { Content, Node, Section as SectionT } from "./types";
import { ContentCtx } from "./lib/context";
import { useDelegation } from "./lib/delegate";
import { Raw, cx } from "./lib/raw";
import { Guard } from "./lib/guard";
import { Blocks } from "./components/Blocks";
import { Masthead } from "./components/Masthead";
import { Rail } from "./components/Rail";
import { Overlays } from "./components/Overlays";

export function App({ content }: { content: Content }) {
  const wrap = useRef<HTMLDivElement>(null);
  useDelegation(wrap, content.defs);
  return (
    <ContentCtx.Provider value={content}>
      <div className="wrap" ref={wrap}>
        <Masthead />
        {content.lede?.length ? <div className="lede"><Blocks nodes={content.lede} /></div> : null}
        {content.sections.map((s) => <Section key={s.id} s={s} />)}
        {content.meta.footer ? <Raw as="footer" className="muted small" html={content.meta.footer} /> : null}
      </div>
      <Guard name="contents rail"><Rail /></Guard>
      <Overlays />
    </ContentCtx.Provider>
  );
}

/** Fold blocks at a section's top level and directly inside its groups. */
function foldCount(blocks: Node[]) {
  let n = 0;
  for (const b of blocks || []) {
    if (b?.type === "fold") n++;
    else if (b?.type === "group") n += (b.blocks || []).filter((x: Node) => x?.type === "fold").length;
  }
  return n;
}

function Section({ s }: { s: SectionT }) {
  const folds = foldCount(s.blocks);
  const lead = s.blocks[0]?.type === "p" && (s.blocks[0].cls || []).includes("lead") ? 1 : 0;
  return (
    <section id={s.id}>
      <h2>
        <span className="sec-no">{s.num}</span>
        <Raw html={s.title} />
        {s.chip ? <> <span className={cx("chip", s.chipTone)}>{s.chip}</span></> : null}
      </h2>
      {folds >= 3 ? (
        <>
          <Blocks nodes={s.blocks.slice(0, lead)} />
          <ExpandLine count={folds} />
          <Blocks nodes={s.blocks.slice(lead)} />
        </>
      ) : <Blocks nodes={s.blocks} />}
    </section>
  );
}

/** "Expand all" / "Collapse all" for a section's reference blocks; shown only with JavaScript (the .js class). */
function ExpandLine({ count }: { count: number }) {
  const line = useRef<HTMLParagraphElement>(null);
  const setAll = (open: boolean) => {
    line.current?.closest("section")?.querySelectorAll<HTMLDetailsElement>("details.more").forEach((d) => { d.open = open; });
  };
  return (
    <p className="expand-line small" ref={line}>
      <span className="muted">{count} reference blocks</span>
      <button type="button" className="linkish" onClick={() => setAll(true)}>Expand all</button>
      <button type="button" className="linkish" onClick={() => setAll(false)}>Collapse all</button>
    </p>
  );
}
