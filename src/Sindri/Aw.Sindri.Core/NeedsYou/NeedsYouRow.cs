using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// One row of the human's list (API spec §3.6; UI spec §4.3, E-7, E-16; P4 plan U3-API-1): everything the UI needs to draw it,
/// order it and act on it, with no logic of its own. The text fields are scrubbed and escaped by the projection that wrote them.
/// </summary>
/// <param name="Id">'decision:DEC-…', 'failure:service:&lt;name&gt;' or 'profile:drift'.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Label">The row's label ('decision · re-scope', 'failure · &lt;service&gt;', 'profile · drifted').</param>
/// <param name="Subject">What it is about.</param>
/// <param name="Item">The item it concerns, when it has one (a decision row names it so "Open in &lt;agent&gt;" has its item).</param>
/// <param name="Title">The headline.</param>
/// <param name="Reason">Why it needs the human.</param>
/// <param name="Repo">The repo it belongs to, or null (a session sees its own repo's rows and the repo-less ones).</param>
/// <param name="Urgency">How loudly it asks.</param>
/// <param name="Since">When it started to need the human.</param>
/// <param name="RecordedOnly">True when the answer is recorded and not yet acted on (API spec §3.4).</param>
/// <param name="Notice">The sentence saying so (EmptyText.RecordedOnly) when <paramref name="RecordedOnly"/>, else null.</param>
/// <param name="AnswerKind">The kind of answer it takes (engine spec §5.8); null for every P4 row.</param>
/// <param name="PrimaryAction">A catalog action id (never 'copy:'), a member of <paramref name="Actions"/>, or null when the row has no one-step action (a failure of a service this build lacks).</param>
/// <param name="Actions">The action ids that apply; ready, pending or cli-only is the catalog's say.</param>
/// <param name="Cli">The command line that does it, built through CliForm.Fill with typed ids.</param>
/// <param name="Via">"child" when the cli is a pointer to a child that decides the answer, not an answer form; null (omitted) otherwise.</param>
/// <param name="Child">The address of the child a pointer row ("child" in <paramref name="Via"/>) points at; null (omitted) otherwise.</param>
/// <param name="Stop">On a 'desk:' row, which desk state of the instance's recipe it waits in ("Approval n of of"); a read-time join, so null (omitted) when the recipe can't be read, and in the stored view.</param>
/// <param name="Timeout">On a 'desk:' or 'park:' row, the timeout the instance status serves for the same instance, InSeconds measured at the response; a read-time join, null (omitted) when there is none or the instance can't be read.</param>
public sealed record NeedsYouRow(
    NeedsYouRowId Id,
    NeedsYouKind Kind,
    string Label,
    NeedsYouSubject Subject,
    ItemId? Item,
    string Title,
    string Reason,
    RepoName? Repo,
    Urgency Urgency,
    DateTimeOffset Since,
    bool RecordedOnly,
    string? Notice,
    AnswerKind? AnswerKind,
    ActionId? PrimaryAction,
    IReadOnlyList<ActionId> Actions,
    string Cli,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Child = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NeedsYouStop? Stop = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StatusTimeout? Timeout = null);
