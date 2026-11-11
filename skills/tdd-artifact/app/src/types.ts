// The shape of content.json as scripts/build_tdd.py writes it. Inline HTML strings are trusted output of the compiler.
export type Node = any;
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
export interface Section { id: string; num: number; title: string; titleText: string; chip: string; chipTone: string; hasGroup: boolean; blocks: Node[]; subs: { id: string; title: string }[] }
export interface Content {
  template: string; title: string; titleText: string; tabTitle: string;
  meta: { eyebrow: string; project?: { label: string; href?: string }; people: { role: string; name: string }[]; updated: string; rev?: number | string; revNote: string; status: string; statusTone: string; repoUrl?: string; sha?: string; shaDate?: string; footer: string; dataSources?: { name: string; server: string; tool: string; kind: "prod" | "local" | "render" | "seed"; note?: string }[] };
  lede: Node[]; nav: { id: string; num: number; title: string; subs: { id: string; title: string }[] }[]; sections: Section[];
  defs: Record<string, Def>; decisions: any[]; cites: any[]; glossary: Record<string, string>; schema: SchemaTable[];
  refs: { linked: number; unlinked: string[] }; stats: Record<string, number>;
}
