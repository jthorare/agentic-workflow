import { Fragment } from "react";
import { Raw, cx } from "../lib/raw";
import type { Node } from "../types";
import { Diagram, Legend } from "./Diagram";
import { Shot } from "./Shot";
import { Prs } from "./Prs";

export function Blocks({ nodes }: { nodes: Node[] }) {
  return <>{(nodes || []).map((n, i) => <Block key={i} node={n} />)}</>;
}

export function Block({ node: n }: { node: Node }) {
  if (!n) return null;
  switch (n.type) {
    case "p": return <Raw as="p" id={n.id} className={cx(...(n.cls || [])) || undefined} html={n.html} />;
    case "heading": { const H = `h${n.level}` as any; return <Raw as={H} id={n.id} html={n.html} />; }
    case "list": return <List node={n} />;
    case "table": return <Table node={n} />;
    case "code": return <pre data-lang={n.lang || undefined}><Raw as="code" className={n.lang ? `lang-${n.lang}` : undefined} html={n.html} /></pre>;
    case "callout":
      if (n.tone === "quote") return <blockquote><Blocks nodes={n.blocks} /></blockquote>;
      return <aside className={cx("callout", n.tone)}><Blocks nodes={n.blocks} /></aside>;
    case "hr": return <hr />;
    case "placeholder": return <Raw as="p" id={n.id} className="muted small" html={n.html} />;
    case "shot": return <Shot node={n} />;
    case "legend": return <Legend items={n.items} />;
    case "diagram": return <Diagram node={n} />;
    case "cards": return (
      <div className={cx("cards", n.four && "four", n.stat && "stat")}>
        {n.cards.map((c: any, i: number) => (
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
        {n.options.map((o: any, i: number) => (
          <div className={cx("opt", o.rec && "rec")} key={i}><Raw as="b" html={o.title} />{o.rec ? <span className="chip rec">Recommended</span> : null}<Blocks nodes={o.blocks} /></div>
        ))}
      </div>
    );
    case "steps": return <Blocks nodes={n.blocks} />;
    case "cols": return <div className="two-col">{n.cols.map((c: Node[], i: number) => <div key={i}><Blocks nodes={c} /></div>)}</div>;
    case "col": return <div><Blocks nodes={n.blocks} /></div>;
    case "glossary": return (
      <dl className="glossary">
        {n.items.map((g: any) => <div id={g.id} key={g.id}><dt>{g.term}</dt><Raw as="dd" html={g.html} /></div>)}
      </dl>
    );
    case "prs": return <Prs node={n} />;
    case "box": return <div className={n.kind}><Blocks nodes={n.blocks} /></div>;
    default: return null;
  }
}

function List({ node: n }: { node: Node }) {
  const Tag = (n.ordered ? "ol" : "ul") as any;
  return (
    <Tag className={n.steps ? "steps" : undefined} start={n.ordered && n.start && n.start !== 1 ? n.start : undefined}>
      {n.items.map((it: any, i: number) => (
        <li key={i} className={cx(...(it.cls || []), it.check === true && "done", it.check === false && "todo") || undefined}>
          {n.checks ? <input type="checkbox" checked={!!it.check} readOnly disabled /> : null}
          <Raw html={it.html} />
          {it.children?.map((c: Node, j: number) => <List node={c} key={j} />)}
        </li>
      ))}
    </Tag>
  );
}

function Table({ node: n }: { node: Node }) {
  return (
    <div className="tbl">
      <table>
        {n.head.some((h: any) => h.html) ? (
          <thead><tr>{n.head.map((h: any, j: number) => <Raw as="th" key={j} style={h.align ? { textAlign: h.align } : undefined} html={h.html} />)}</tr></thead>
        ) : null}
        <tbody>
          {n.rows.map((r: any, i: number) => (
            <tr key={i} id={r.id || undefined}>
              {r.cells.map((c: any, j: number) => <Raw as="td" key={j} className={cx(...(c.cls || [])) || undefined} style={c.align ? { textAlign: c.align } : undefined} html={c.html} />)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Persona({ node: n }: { node: Node }) {
  return (
    <article className="fe-box" id={n.id}>
      <div className="fe-text">
        <h3><Raw html={n.title} />{n.chip ? <span className={cx("chip", n.chipTone)}>{n.chip}</span> : null}</h3>
        <Blocks nodes={n.text} />
      </div>
      <div className={cx("fe-media", n.grid)}>
        {n.media.map((m: Node, i: number) => <Shot node={m} key={i} />)}
      </div>
      {n.today ? <Today node={n.today} /> : null}
    </article>
  );
}

function Today({ node: n }: { node: Node }) {
  return (
    <details className="more today">
      <summary><span className="t">{n.label}</span>{n.summary ? <Raw className="one" html={n.summary} /> : null}</summary>
      <div className="more-body">
        {n.media?.length ? <div className={cx("fe-media", n.grid)}>{n.media.map((m: Node, i: number) => <Shot node={m} key={i} />)}</div> : null}
        <Blocks nodes={n.blocks} />
      </div>
    </details>
  );
}
