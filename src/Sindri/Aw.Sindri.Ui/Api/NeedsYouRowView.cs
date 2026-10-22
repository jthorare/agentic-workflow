namespace Aw.Sindri.Ui.Api;

/// <summary>One row of GET /sindri/needs-you (API P4): what waits on the human, in the API's order.</summary>
/// <param name="Id">The row's id: "decision:DEC-…", "failure:service:name", "profile:drift".</param>
/// <param name="Kind">The row's kind (decision, failure, profile, park).</param>
/// <param name="Label">The served label ("decision · re-scope").</param>
/// <param name="Subject">What it is about; null for a profile row before its owner decision.</param>
/// <param name="Item">The item the row names, when it names one.</param>
/// <param name="Title">The row's title.</param>
/// <param name="Reason">The served reason sentence.</param>
/// <param name="Repo">The repo tag, when there is one.</param>
/// <param name="Urgency">"pulse", "colour" or "calm".</param>
/// <param name="Since">When it began waiting.</param>
/// <param name="RecordedOnly">True when the answer is only recorded now.</param>
/// <param name="Notice">The served recorded-only sentence.</param>
/// <param name="AnswerKind">What the answer takes; null on every P4 row.</param>
/// <param name="PrimaryAction">The catalog id of the primary action, or null for a failure of a service this build does not have.</param>
/// <param name="Actions">The catalog ids of every action.</param>
/// <param name="Cli">The one command to copy.</param>
/// <param name="Chip">The served chip (U3'-API-9); null until the API serves one.</param>
/// <param name="Via">"child" on a row whose cli points at a child that decides the answer, else null (omitted).</param>
/// <param name="Child">The deciding child instance's address (what `sindri workflow status` takes); set only on a via child row, else null (omitted).</param>
/// <param name="Stop">On a desk row, which desk state of its recipe it waits in; null (omitted) otherwise or when the recipe could not be read.</param>
/// <param name="Timeout">On a desk or park row, the timeout the instance status serves, measured at the response; null (omitted) when there is none.</param>
internal sealed record NeedsYouRowView(
    string Id,
    string Kind,
    string Label,
    SubjectRefView? Subject,
    string? Item,
    string Title,
    string Reason,
    string? Repo,
    string Urgency,
    DateTimeOffset Since,
    bool RecordedOnly,
    string? Notice,
    string? AnswerKind,
    string? PrimaryAction,
    IReadOnlyList<string> Actions,
    string Cli,
    Workflow.StateChip? Chip = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Via = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Child = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NeedsYouStopView? Stop = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Workflow.StatusTimeout? Timeout = null);
