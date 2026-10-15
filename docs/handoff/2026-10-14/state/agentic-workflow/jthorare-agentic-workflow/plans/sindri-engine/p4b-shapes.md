# P4b record shapes (contract for the API lane; mirrors API spec §3.8 field names and nesting)

Namespace `Aw.Sindri.Core.Workflow.Engine.Runtime.Layout`, folder `src/Sindri/Aw.Sindri.Core/Workflow/Engine/Runtime/Layout/`,
one public sealed record (or enum) per file. JSON: camelCase (the host's SindriJsonContext options), enums as the
lowercase/kebab strings shown, nulls written as `null` for optional (`?`) members, empty lists as `[]`.
All records are immutable; every member has a `///` summary.

## Enums (JsonStringEnumConverter<T> + [JsonStringEnumMemberName])
- `Urgency`: `incident`, `triage`, `work`
- `StateGlyph`: `done`, `active`, `pending`
- `BadgeKind`: `parked`, `escalated`, `taken-over`
- `OrderedBy`: `path`, `transition-list`
- `AnswerKind`: `text`, `resume`, `grant`, `grantTokens`, `accept`, `cancel-only`
- `ActionClass`: `desk`, `protected`
- `TimeoutKind`: `parks`, `escalates`
- `FactDirection`: `input`, `output`
- `FactStatus`: `produced`, `in-progress`, `pending`

## InstanceSummary (the list row, E-20)
```
InstanceSummary(
  string Address, string? ItemId, RecipeRef Recipe, string State, DateTimeOffset Since, EngineBadge? EngineOwned,
  SubjectRef? Subject, IReadOnlyList<InputSlot> Inputs, IReadOnlyList<StateCell> States, string Now,
  IReadOnlyList<string> Actions)
RecipeRef(string Id, string Hash, int Version, Urgency Urgency)
EngineBadge(BadgeKind Kind, string Reason)
SubjectRef(string Kind, string Key, string Label, string? Href)
InputSlot(string Key, string Label, string Kind, bool Required, bool Present)
```
`Now` = the `next` sentence cut to 80 characters (ellipsis "..." counts inside the 80).

## StateCell (E-20)
```
StateCell(string Id, int? Order, string? BranchOf, StateGlyph Glyph, EngineBadge? Badge, RoundOf? Round,
          IReadOnlyList<LoopPair> Loops, bool Desk, bool Child, ChildProgress? Children, string? Outcome, bool Terminal)
RoundOf(int N, int Max)
LoopPair(string With, int N, int Max)      // JSON "with"
ChildProgress(int Done, int Total)
```
Order: 0-based position in path order for a main-path state; null for a branch state (WF §3.10 says branch
`order: null`; API §3.8 says an int). Unresolved spec text: see report.

## StatusFields (E-23)
```
StatusFields(HeaderParts Header, string Next, AnswerLine? Answer, ChildPark? Child, DriftNote? Drift,
             IReadOnlyList<StepInput> Inputs, TimeoutLine? Timeout, BudgetLine? Budget, PausedLine? Paused,
             IReadOnlyList<CommandRow> Commands, string Text)
HeaderParts(string Address, string Recipe, string Hash12, int Version, string State, string? Detail,
            DateTimeOffset Since, long AgeSeconds)
AnswerLine(AnswerKind Kind, string ActionId, ActionClass Class, string Cli, bool AtTerminal)
ChildPark(string Address, string State, string? Reason)
DriftNote(string Block, bool NeedsReapproval)
StepInput(string Key, string Name, string? Hash)
TimeoutLine(TimeoutKind Kind, long InSeconds, string On)
BudgetLine(string Scope, long Used, long? Limit, InstanceBudget Instance)   // Scope is "state"
InstanceBudget(long Used, long? Limit)
PausedLine(long TimeoutLeftSeconds)
CommandRow(string Verb, string ActionId, ActionClass Class, string Cli)
```
`Text` is the CLI text form, produced by the SAME renderer `sindri workflow status` prints (the status goldens pin it).
Lines the layout leaves out are null / [].

## FactRow (E-20)
```
FactRow(string Key, string Label, string Type, FactDirection Direction, string? From, FactStatus Status, string Summary,
        DateTimeOffset? At, string? By, FactRef? Ref, ChildProgress? Progress, PendingOutput? Pending)
FactRef(string? Name, long? Size, string? Hash, string? Path, string? ItemId, string? Url, string? Host, string? Title, string? FactKey)
PendingOutput(bool Required, IReadOnlyList<string> Checks, string CompleteAction)
```
`Type`: `text|file|item|link|scope-map|packet|evidence|decision|payload`; an unknown type is served as `payload`.
`At`/`By`: when and by whom (`you`, a Step id, an agent) the fact was produced. Provenance is recorded on the instance's
fact entry (FactValue.ProducedBy/ProducedAt), not in the content store and not in the ledger (no migration).

## ChildGroup (E-20)
```
ChildGroup(string State, string Recipe, int Done, int Total, IReadOnlyList<Workstream> Workstreams,
           IReadOnlyList<ChildRow> Rows, int More)
Workstream(string Id, string Label, IReadOnlyList<ChildRow> Rows)
ChildRow(string Address, string? ItemId, string Title, StateGlyph Glyph, string State, RoundOf? Round, string? Agent,
         long AgeSeconds, EngineBadge? EngineOwned, ChildPark? MirroredPark, IReadOnlyList<string> Actions)
```
Empty until engine P7 (children); the producer returns `[]`.

## InstanceView additions (init properties; existing members unchanged)
`string? ItemId`, `Urgency Urgency` (default `work`), `EngineBadge? EngineOwned`, `SubjectRef? Subject`,
`IReadOnlyList<InputSlot> Inputs`, `OrderedBy OrderedBy`, `IReadOnlyList<StateCell> States` (path order),
`StatusFields Status`, `IReadOnlyList<FactRow> Facts`, `IReadOnlyList<ChildGroup> ChildGroups`.
The existing `Children` (live ChildView list) stays. `InstanceSummaries.Of(InstanceView)` builds the `InstanceSummary`.
Not engine-owned (the API adds): `title`, `counts`, `actions` on the view, `asOf`, and `current.step/gates/desk`
(recipe-descriptor data the API reads through ShowRecipe).

## Search attributes (visibility, registered with the dev server and set by RecipeWorkflow)
Existing: `Item` Keyword, `Recipe` Keyword, `State` Keyword, `NeedsYou` Bool, `Repo` Keyword, `Tenant` Keyword, `Ring` Keyword.
New: `Urgency` Keyword (`incident|triage|work`), `Subject` Keyword (the first input's key), `Root` Bool (true for a top-level
instance, false for a child).
Query port: `IWorkflowInstances.List(InstanceFilter, ended, ct)`; `InstanceFilter(string? Recipe, string? State,
bool? NeedsYou, Urgency? Urgency, string? Item, string? Subject, bool? Root)` compiled to a visibility query by a pure,
quote-escaping `InstanceFilters.ToQuery`. `InstanceRow` gains `Recipe`, `Urgency`, `Subject`, `Root`.
