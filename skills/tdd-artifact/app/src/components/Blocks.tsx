import type { CSSProperties, ElementType } from "react";
import { Raw, cx } from "../lib/raw";
import type { Align, BlockList, ListNode, Node, PersonaNode, ShotNode, TableBlock, TodayNode } from "../types";
import { Diagram, Legend } from "./Diagram";
import { Shot } from "./Shot";
import { Prs } from "./Prs";

export function Blocks({ nodes }: { nodes: BlockList | undefined }) {
  return <>{(nodes || []).map((n, i) => <Block key={i} node={n} />)}</>;
}

const align = (a: Align | null | undefined): CSSProperties | undefined => (a ? { textAlign: a } : undefined);

/** One block of content.json. Every node type the compiler emits has a case (types.ts NODE_TYPES); a type this build
 *  does not know renders nothing, and build.ts names it in a warning. */
export function Block({ node: n }: { node: Node | null }) {
  if (!n) return null;
  switch (n.type) {
    case "p": return <Raw as="p" id={n.id} className={cx(...(n.cls || [])) || undefined} html={n.html} />;
    case "heading": return <Raw as={`h${n.level}`} id={n.id} html={n.html} />;
    case "list": return <List node={n} />;
    case "table": return <Table node={n} />;
    case "code": return <pre data-lang={n.lang || undefined}><Raw as="code" className={n.lang ? `lang-${n.lang}` : undefined} html={n.html} /></pre>;
    case "callout":
      if (n.tone === "quote") return <blockquote><Blocks nodes={n.blocks} /></blockquote>;
      return <aside className={cx("callout", n.tone)}><Blocks nodes={n.blocks} /></aside>;
    case "hr": return <hr />;
    case "placeholder": return <Raw as="p" id={n.id} className="muted small" html={n.html} />;
    case "shot": return <Shot node={n} />;
    case "shots": return <Media grid={n.grid} media={n.media} />;
    case "legend": return <Legend items={n.items} />;
    case "diagram": return <Diagram node={n} />;
    case "cards": return (
      <div className={cx("cards", n.four && "four", n.stat && "stat")}>
        {n.cards.map((c, i) => (
          <div className="card" id={c.id} key={i}>
            {c.title ? (n.stat ? <Raw as="div" className="k" html={c.title} /> : <Raw as="h4" html={c.title} />) : null}
            <Blocks nodes={c.blocks} />
          </div>
        ))}
      </div>
    );
    case "persona": return <Persona node={n} />;
    case "duo": return <div className="fe-duo"><Blocks nodes={n.items} /></div>;
    case "today": return <Today node={n} />;
    case "fold": return (
      <details className="more" id={n.id} open={n.open || undefined}>
        <summary><Raw className="t" html={n.title} />{n.one ? <Raw className="one" html={n.one} /> : null}</summary>
        <div className="more-body"><Blocks nodes={n.blocks} /></div>
      </details>
    );
    case "group": return (
      <div className="group" id={n.id || undefined}>
        <Raw as="h3" className="group-h" html={n.title} />
        {n.sub ? <Raw as="p" className="small muted" html={n.sub} /> : null}
        <Blocks nodes={n.blocks} />
      </div>
    );
    case "decisions": return <div className="dec-grid"><Blocks nodes={n.blocks} /></div>;
    case "decision": return (
      <article className={cx("dec", n.state, n.wide && "wide")} id={n.id}>
        <div className="head"><span className="id">{n.did}</span><Raw as="h3" html={n.question} />{n.status ? <Raw className={cx("chip", n.state)} html={n.status} /> : null}</div>
        <Blocks nodes={n.blocks} />
      </article>
    );
    case "options": return (
      <div className="opts">
        {n.options.map((o, i) => (
          <div className={cx("opt", o.rec && "rec")} key={i}><Raw as="b" html={o.title} />{o.rec ? <span className="chip rec">Recommended</span> : null}<Blocks nodes={o.blocks} /></div>
        ))}
      </div>
    );
    case "steps": return <Blocks nodes={n.blocks} />;
    case "cols": return <div className="two-col">{n.cols.map((c, i) => <div key={i}><Blocks nodes={c} /></div>)}</div>;
    case "col": return <div><Blocks nodes={n.blocks} /></div>;
    case "glossary": return (
      <dl className="glossary">
        {n.items.map((g) => <div id={g.id} key={g.id}><dt>{g.term}</dt><Raw as="dd" html={g.html} /></div>)}
      </dl>
    );
    case "prs": return <Prs node={n} />;
    case "box": return <div className={n.kind}><Blocks nodes={n.blocks} /></div>;
    default: return unknown(n);
  }
}

/** Compile-time proof that the switch above covers the Node union; at run time (a newer compiler) nothing is drawn. */
function unknown(_: never) { return null; }

function List({ node: n }: { node: ListNode }) {
  const Tag: ElementType = n.ordered ? "ol" : "ul";
  return (
    <Tag className={n.steps ? "steps" : undefined} start={n.ordered && n.start && n.start !== 1 ? n.start : undefined}>
      {n.items.map((it, i) => (
        <li key={i} className={cx(...(it.cls || []), it.check === true && "done", it.check === false && "todo") || undefined}>
          {n.checks ? <input type="checkbox" checked={!!it.check} readOnly disabled /> : null}
          <Raw html={it.html} />
          {it.children?.map((c, j) => <List node={c} key={j} />)}
        </li>
      ))}
    </Tag>
  );
}

function Table({ node: n }: { node: TableBlock }) {
  return (
    <div className="tbl">
      <table>
        {n.head.some((h) => h.html) ? (
          <thead><tr>{n.head.map((h, j) => <Raw as="th" key={j} style={align(h.align)} html={h.html} />)}</tr></thead>
        ) : null}
        <tbody>
          {n.rows.map((r, i) => (
            <tr key={i} id={r.id || undefined}>
              {r.cells.map((c, j) => <Raw as="td" key={j} className={cx(...(c.cls || [])) || undefined} style={align(c.align)} html={c.html} />)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Stills side by side: a persona box's shots, a today fold's, or a paragraph of several images. */
function Media({ grid, media }: { grid: string; media: ShotNode[] }) {
  return <div className={cx("fe-media", grid)}>{media.map((m, i) => <Shot node={m} key={i} />)}</div>;
}

function Persona({ node: n }: { node: PersonaNode }) {
  return (
    <article className="fe-box" id={n.id}>
      <div className="fe-text">
        <h3><Raw html={n.title} />{n.chip ? <span className={cx("chip", n.chipTone)}>{n.chip}</span> : null}</h3>
        <Blocks nodes={n.text} />
      </div>
      <Media grid={n.grid} media={n.media} />
      {n.today ? <Today node={n.today} /> : null}
    </article>
  );
}

function Today({ node: n }: { node: TodayNode }) {
  return (
    <details className="more today">
      <summary><span className="t">{n.label}</span>{n.summary ? <Raw className="one" html={n.summary} /> : null}</summary>
      <div className="more-body">
        {n.media?.length ? <Media grid={n.grid} media={n.media} /> : null}
        <Blocks nodes={n.blocks} />
      </div>
    </details>
  );
}
