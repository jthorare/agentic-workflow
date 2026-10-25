export type ContentClass = "code" | "diff" | "brief" | "transcript" | "message-meta" | "image";

// Agent-CLI providers: one per supported host (planning/PROVIDERS.md). Each
// runs its host's headless mode on a small fast model from an empty temp cwd.
export type AgentCliName = "claude-cli" | "codex-cli" | "cursor-cli";

export type ProviderName = "rules" | AgentCliName | "jev";

// A JSON Schema fragment for one extra response field (e.g. { type: "integer" }).
export type JsonSchemaFragment = Readonly<Record<string, unknown>>;

export interface Decision<O extends string> {
  decision: O;
  confidence: number;
  model: ProviderName;
  reason_code: string;
  id: string;
  extra?: Record<string, unknown>;
}

export type ProviderResult<O extends string> =
  // `extra` carries fields a provider's raw response has beyond the enum
  // decision itself (ui-element-repair's chosenIndex, visual-critique's
  // reasons) — never part of evaluate()'s typed Decision<O> contract, but
  // threaded through so a dedicated thin CLI subcommand can surface it
  // without evaluate() itself gaining a new shape.
  | { status: "decided"; decision: O; confidence: number; reason_code: string; extra?: Record<string, unknown>; probabilities?: Record<string, number> }
  | { status: "unavailable"; reason_code: string }
  | { status: "error"; reason_code: string };

// QuestionModule is defined fully in src/question.ts (Task 3). This alias lets
// provider.ts (Task 4) reference it without a circular import: providers take
// an already-narrowed prompt string and schema, not the whole module.
export interface QuestionRef<O extends string> {
  name: string;
  outputs: readonly O[];
  prompt: string;
  contentClass: ContentClass;
  // Response fields beyond `decision` the question wants back (chosenIndex,
  // reasons). Providers with schema-constrained output (codex-cli's strict
  // mode) can only return fields declared here; they ride back as `extra`.
  extraProperties?: Readonly<Record<string, JsonSchemaFragment>>;
  criteria?: Readonly<Record<string, string>>;
  // How the question bounds the input before a provider uses it (see boundedInput).
  providerInput?: (input: unknown) => unknown;
}

export interface Provider {
  name: ProviderName;
  classes: ReadonlySet<ContentClass>;
  decide<O extends string>(question: QuestionRef<O>, input: unknown, budgetMs: number): Promise<ProviderResult<O>>;
}
