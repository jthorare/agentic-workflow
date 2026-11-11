// The component catalog (templates/components.json): what a TDD source can use, read for `tdd components` and turned
// into a gallery packet whose page shows every component beside the markdown that makes it. Plain TypeScript.
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

export interface Component {
  name: string;
  kind: "markdown" | "container" | "marker" | "inline";
  /** The content.json node type the example compiles to; null for inline syntax and for a section. */
  node: string | null;
  what: string;
  syntax: string;
  attrs?: Record<string, string>;
  where: string;
  renders: string;
  notes?: string[];
  /** The example carries its own `## ` section line. */
  top?: boolean;
  example: string;
}
export interface Catalog { _about: string; internal: Record<string, string>; components: Component[] }

/** skills/tdd-artifact, from cli/src/components. */
export const SKILL = join(import.meta.dir, "..", "..", "..");
export const CATALOG_PATH = join(SKILL, "templates", "components.json");

export function loadCatalog(path = CATALOG_PATH): Catalog {
  return JSON.parse(readFileSync(path, "utf8")) as Catalog;
}

/** One example as a whole source: a title, and a section around it unless the example brings its own. */
export function exampleSource(c: Component): string {
  return c.top ? `# ${c.name}\n\n${c.example}\n` : `# ${c.name}\n\n## Example {#example; lead: no}\n\n${c.example}\n`;
}

/** The example quoted for the page. A `:::` line is read as a container even inside a fence, so those lines get a
 *  leading space (which a reader drops when copying). */
function quoted(example: string): string {
  const body = example.split("\n").map((l) => (/^:{3,}/.test(l) ? ` ${l}` : l)).join("\n");
  return `~~~~markdown\n${body}\n~~~~`;
}

const KINDS: [Component["kind"], string, string][] = [
  ["markdown", "Markdown", "Plain markdown the compiler gives a house shape."],
  ["container", "Containers", "`::: kind Title {attrs}` blocks; nest by adding a colon."],
  ["marker", "Markers", "One-line markers in square brackets or a comment."],
  ["inline", "Inline", "Syntax inside running text."],
];

/** The gallery source: one section per kind, and per component its purpose, syntax, the rendered example and its markdown. */
export function gallerySource(cat: Catalog): string {
  const out: string[] = ["# Component gallery", ""];
  for (const [kind, title, lead] of KINDS) {
    const list = cat.components.filter((c) => c.kind === kind && !c.top);
    if (!list.length) continue;
    out.push(`## ${title} {#${kind}}`, "", lead, "");
    for (const c of list) {
      out.push(`### ${c.name} {#c-${c.name}}`, "", `${c.what} {small}`, "", c.example, "", quoted(c.example), "");
      const attrs = Object.entries(c.attrs ?? {});
      if (attrs.length) out.push("| Attribute | Meaning |", "|---|---|", ...attrs.map(([k, v]) => `| \`${k}\` | ${v} |`), "");
      for (const n of c.notes ?? []) out.push(`${n} {small; muted}`, "");
    }
  }
  for (const c of cat.components.filter((x) => x.top)) out.push(c.example, "", quoted(c.example), "");
  return out.join("\n");
}

const PR_CARD = `---
milestone: "M1 · The worker"
title: Add the delivery table
change: One new table; nothing reads it yet
rollback: Drop the table
---
# PR 1: Add the delivery table

## Summary
Adds \`webhook_deliveries\`. Nothing reads it yet. Rollback: drop the table.

## Tests
The migration runs up and down on an empty database.
`;

function copyTree(from: string, to: string): void {
  mkdirSync(to, { recursive: true });
  for (const e of readdirSync(from, { withFileTypes: true })) {
    if (e.isDirectory()) copyTree(join(from, e.name), join(to, e.name));
    else copyFileSync(join(from, e.name), join(to, e.name));
  }
}

/** Write the gallery packet into `dir` (which must be new or empty): gallery.md, meta.json, one PR card and the
 *  scaffold's placeholder images. Returns the files written, relative to `dir`. */
export function writeGallery(dir: string, cat = loadCatalog()): string[] {
  if (existsSync(dir) && readdirSync(dir).length) throw new Error(`${dir} is not empty: the gallery is written into a new or empty folder`);
  mkdirSync(join(dir, "prs"), { recursive: true });
  copyTree(join(SKILL, "templates", "packet"), dir);
  const meta = { title: "Component gallery", eyebrow: "tdd-artifact", people: [], updated: "", rev: 1, rev_note: "generated from templates/components.json", status: "Reference", refs: {} };
  writeFileSync(join(dir, "gallery.md"), gallerySource(cat));
  writeFileSync(join(dir, "meta.json"), `${JSON.stringify(meta, null, 1)}\n`);
  writeFileSync(join(dir, "prs", "pr-01.md"), PR_CARD);
  return ["gallery.md", "meta.json", "prs/pr-01.md", ...readdirSync(join(SKILL, "templates", "packet"), { recursive: true, withFileTypes: true }).filter((e) => e.isFile()).map((e) => join(e.parentPath, e.name).slice(join(SKILL, "templates", "packet").length + 1)).sort()];
}

/** The list `tdd components` prints: name, kind, node and purpose, one line each. */
export function listing(cat: Catalog): string {
  const w = Math.max(...cat.components.map((c) => c.name.length));
  return cat.components.map((c) => `${c.name.padEnd(w)}  ${c.kind.padEnd(9)}  ${(c.node ?? "-").padEnd(9)}  ${c.what}`).join("\n");
}

/** One component in full, as text an agent can read before writing it. */
export function describe(c: Component): string {
  const out = [`${c.name} (${c.kind}${c.node ? `, node "${c.node}"` : ""})`, c.what, "", "Syntax:", ...c.syntax.split("\n").map((l) => `  ${l}`)];
  const attrs = Object.entries(c.attrs ?? {});
  if (attrs.length) out.push("", "Attributes:", ...attrs.map(([k, v]) => `  ${k}: ${v}`));
  out.push("", `Where: ${c.where}`, `Renders: ${c.renders}`);
  if (c.notes?.length) out.push("", "Notes:", ...c.notes.map((n) => `  - ${n}`));
  out.push("", "Example:", ...c.example.split("\n").map((l) => `  ${l}`));
  return out.join("\n");
}
