// The shape of content.json as scripts/build_tdd.py writes it. Inline HTML strings are trusted output of the compiler.
// Every block node the compiler emits has a `type`; NODE_TYPES is the list the renderer (components/Blocks.tsx) draws,
// and build.ts warns about a node whose type is not in it instead of dropping it without a word.
/** A trusted inline-HTML string (reference links, chips and citations already rendered by the compiler). */
export type Html = string;
export type Align = "left" | "center" | "right";
export interface PNode { type: "p"; html: Html; cls?: string[]; id?: string }
export interface HeadingNode { type: "heading"; level: number; html: Html; id?: string }
export interface ListItem { html: Html; cls?: string[]; check: boolean | null; children?: ListNode[] }
export interface ListNode { type: "list"; ordered: boolean; start: number | null; steps: boolean; checks: boolean; items: ListItem[] }
export interface TableCell { html: Html; cls?: string[]; align: Align | null }
export interface TableBlock { type: "table"; cols: number; head: { html: Html; align: Align | null }[]; rows: { id: string | null; cells: TableCell[] }[] }
export interface CodeNode { type: "code"; lang: string; text: string; html: Html; section: string | null; anchor: string | null }
/** `::: callout` (note, warn, good, danger, violet, blue) or a markdown quote (tone "quote", drawn as a blockquote). */
export interface CalloutNode { type: "callout"; tone: string; blocks: BlockList }
export interface HrNode { type: "hr" }
/** What the compiler leaves where a diagram, image or the PR cards are missing. */
export interface PlaceholderNode { type: "placeholder"; html: Html; id?: string }
export interface ShotNode { type: "shot"; src: string; alt: string; caption: Html; size: string; width: number; height: number }
/** A paragraph of two or more images: the stills side by side. */
export interface ShotsNode { type: "shots"; grid: string; media: ShotNode[] }
export interface LegendItem { label: Html; tone: string }
export interface LegendNode { type: "legend"; items: LegendItem[] }
export interface DiagramNode {
  type: "diagram"; id: string; key: string; src: string; width: number; height: number; title: Html; alt: string; fit: boolean;
  legend: LegendItem[]; reading: Html; hotspots: Hotspot[];
}
export interface Card { title: Html; blocks: BlockList; id?: string }
export interface CardsNode { type: "cards"; four: boolean; stat: boolean; cards: Card[] }
export interface TodayNode { type: "today"; label: string; summary: Html; blocks: BlockList; media: ShotNode[]; grid: string }
export interface PersonaNode { type: "persona"; id: string; title: Html; chip: string; chipTone: string; grid: string; text: BlockList; media: ShotNode[]; today: TodayNode | null }
export interface DuoNode { type: "duo"; items: BlockList }
export interface FoldNode { type: "fold"; id: string; title: Html; one: Html; open: boolean; blocks: BlockList }
export interface GroupNode { type: "group"; id: string | null; title: Html; sub: Html; blocks: BlockList }
export interface DecisionsNode { type: "decisions"; blocks: BlockList }
export interface DecisionNode { type: "decision"; id: string; did: string; question: Html; status: Html; state: string; wide: boolean; blocks: BlockList }
export interface Option { title: Html; rec: boolean; blocks: BlockList }
export interface OptionsNode { type: "options"; options: Option[] }
export interface StepsNode { type: "steps"; blocks: BlockList }
export interface ColsNode { type: "cols"; cols: BlockList[] }
export interface ColNode { type: "col"; blocks: BlockList }
export interface GlossaryNode { type: "glossary"; items: { id: string; term: string; html: Html }[] }
export interface Pr {
  n: number; sub: string; label: string; id: string; title: Html; change: Html; dependsOn: string[]; deps: Html;
  decisions: string[]; tokens: string[]; paths: string[]; rollback: Html; bodyHtml: Html; file: string;
}
export interface PrsNode { type: "prs"; groups: { ms: Html; id: string | null; note: Html; prs: Pr[] }[] }
/** An unknown `::: kind`: the compiler warns and keeps its blocks in a plain div of that class. */
export interface BoxNode { type: "box"; kind: string; blocks: BlockList }
export type Node =
  | PNode | HeadingNode | ListNode | TableBlock | CodeNode | CalloutNode | HrNode | PlaceholderNode | ShotNode | ShotsNode
  | LegendNode | DiagramNode | CardsNode | PersonaNode | DuoNode | TodayNode | FoldNode | GroupNode | DecisionsNode
  | DecisionNode | OptionsNode | StepsNode | ColsNode | ColNode | GlossaryNode | PrsNode | BoxNode;
/** A list of blocks as content.json holds it: the compiler leaves null where a block produced nothing. */
export type BlockList = (Node | null)[];
export const NODE_TYPES = [
  "p", "heading", "list", "table", "code", "callout", "hr", "placeholder", "shot", "shots", "legend", "diagram", "cards",
  "persona", "duo", "today", "fold", "group", "decisions", "decision", "options", "steps", "cols", "col", "glossary", "prs", "box",
] as const satisfies readonly Node["type"][];
/** Fails to compile when a node type is added to the union and not to NODE_TYPES. */
export type _EveryNodeTypeListed = Node["type"] extends (typeof NODE_TYPES)[number] ? true : never;
const _everyNodeTypeListed: _EveryNodeTypeListed = true;
void _everyNodeTypeListed;
export interface Def { kind: string; title: string; text: string }
export interface Hotspot { label: string; x: number; y: number; w: number; h: number; href: string; note: string; tone: string; image?: string; table?: string }
export interface Column { name: string; type: string; change: "new" | "changed" | "dropped" | "unchanged"; note?: string; was?: string }
export interface Measure { value?: string | number; unit?: string; source?: string; date?: string; note?: string }
export interface SchemaTable {
  id: string; table: string; state: "new" | "changed" | "existing"; summary?: string; columns: Column[];
  sample_rows?: Record<string, any>[]; sample_source?: string;
  measured?: Record<string, Measure>; expected?: Record<string, Measure>; ddl?: string; indexes?: string[]; notes?: string[];
  /** D28, optional: what the design gains and costs for this table, two or three terse lines */
  tradeoffs?: (string | { gain?: string; cost?: string })[];
}
export interface Section { id: string; num: number; title: string; titleText: string; chip: string; chipTone: string; hasGroup: boolean; blocks: BlockList; subs: { id: string; title: string }[] }
export interface Content {
  template: string; title: string; titleText: string; tabTitle: string;
  meta: { eyebrow: string; project?: { label: string; href?: string }; people: { role: string; name: string }[]; updated: string; rev?: number | string; revNote: string; status: string; statusTone: string; repoUrl?: string; sha?: string; shaDate?: string; footer: string; dataSources?: { name: string; server: string; tool: string; kind: "prod" | "local" | "render" | "seed"; note?: string }[] };
  lede: BlockList; nav: { id: string; num: number; title: string; subs: { id: string; title: string }[] }[]; sections: Section[];
  defs: Record<string, Def>; decisions: any[]; cites: any[]; glossary: Record<string, string>; schema: SchemaTable[];
  refs: { linked: number; unlinked: string[] }; stats: Record<string, number>;
}
