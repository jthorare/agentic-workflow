import { useContent } from "../lib/context";
import { Raw, cx } from "../lib/raw";

const MD_LINK = /^\[([^\]]+)\]\(([^)\s]+)\)$/;

/** meta.project.label may still be a markdown link ("[P-XXX-000](https://...)"); show its text, link to href. */
function project(p?: { label: string; href?: string }) {
  if (!p || !p.label) return null;
  const m = MD_LINK.exec(p.label.trim());
  return { label: m ? m[1] : p.label, href: p.href || (m ? m[2] : undefined) };
}

/** Eyebrow, title, the people and dates as one .meta row, the status chip, and the evidence commit. */
export function Masthead() {
  const c = useContent();
  const m = c.meta;
  const proj = project(m.project);
  const rev = m.rev !== undefined && m.rev !== null && m.rev !== "" ? `rev ${m.rev}` : "";
  const updated = [m.updated, rev, m.revNote].filter(Boolean).join(" · ");
  const sha = m.sha ? m.sha.slice(0, 7) : "";
  const shaHref = m.repoUrl && m.sha ? `${m.repoUrl.replace(/\/+$/, "")}/tree/${m.sha}` : undefined;
  return (
    <header className="masthead">
      {m.eyebrow ? <Raw as="div" className="eyebrow" html={m.eyebrow} /> : null}
      <Raw as="h1" html={c.title} />
      <div className="meta">
        {proj ? <span><b>Project</b> {proj.href ? <a href={proj.href}>{proj.label}</a> : proj.label}</span> : null}
        {(m.people || []).map((p, i) => <span key={i}><b>{p.role}</b> {p.name}</span>)}
        {updated ? <span><b>Updated</b> {updated}</span> : null}
        {sha ? <span><b>Evidence</b> at {shaHref ? <a href={shaHref}><code>{sha}</code></a> : <code>{sha}</code>}{m.shaDate ? ` (${m.shaDate})` : ""}</span> : null}
        {m.status ? <span className={cx("chip", m.statusTone)}>{m.status}</span> : null}
      </div>
    </header>
  );
}
