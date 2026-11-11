import { Fragment } from "react";
import { Raw } from "../lib/raw";
import type { Node } from "../types";
/** Milestone-labelled PR cards; each is a <details> whose body is the pr-NN.md rendered at compile time. */
export function Prs({ node: n }: { node: Node }) {
  return (
    <div className="prs">
      {n.groups.map((g: any, i: number) => (
        <Fragment key={i}>
          <div className="ms" id={g.id || undefined}><Raw html={g.ms} />{g.note ? <span> · <Raw html={g.note} /></span> : null}</div>
          {g.prs.map((p: any) => (
            <details className="pr" id={p.id} key={p.id}>
              <summary><span className="n">PR {p.label ?? p.n}</span><Raw className="t" html={p.title} />{p.change ? <Raw className="one" html={p.change} /> : null}</summary>
              <Raw as="div" className="body" html={(p.deps ? `<p class="small muted deps">${p.deps}</p>` : "") + (p.bodyHtml || "") + (p.rollback ? `<div class="rb"><b>Rollback:</b> ${p.rollback}</div>` : "")} />
            </details>
          ))}
        </Fragment>
      ))}
    </div>
  );
}
