using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetItem;

/// <summary>Item detail (API spec §3.1 'the summary, plus …'; U2-API-3, U2-API-7): the summary's fields flattened, then the detail.</summary>
/// <param name="Id">The item.</param>
/// <param name="Title">Its title, scrubbed.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="State">Its state.</param>
/// <param name="Step">Its recipe step, or null.</param>
/// <param name="Since">When it entered its state.</param>
/// <param name="PrimaryAction">The primary action, a member of Actions, or null.</param>
/// <param name="Actions">The action ids it offers.</param>
/// <param name="Owner">Reserved for multi-user (README X61); always null for now.</param>
/// <param name="Version">Its version: the one a close sends back.</param>
/// <param name="CreatedBy">The actor that created it, verbatim.</param>
/// <param name="WaitingOn">The pending decision it waits on (API P4), or null.</param>
/// <param name="Instance">The workflow instance running it (API P11), or null.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="Body">Its body, scrubbed (only here: never on a list row or a public event).</param>
/// <param name="Link">The tracker record it is linked to, or null.</param>
/// <param name="Runs">The workflow runs linked to it (ids, as 'sindri:run/&lt;id&gt;' links hold them), newest first; empty when it has none or the links can't be read.</param>
/// <param name="Launch">What the app may offer to open, attach or resume (UI spec E-2), from projections only.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record ItemView(
    ItemId Id,
    string Title,
    RepoName? Repo,
    ItemState State,
    string? Step,
    DateTimeOffset Since,
    ActionId? PrimaryAction,
    IReadOnlyList<ActionId> Actions,
    string? Owner,
    long Version,
    string CreatedBy,
    string? WaitingOn,
    string? Instance,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Body,
    RecordKey? Link,
    IReadOnlyList<string> Runs,
    ItemLaunch Launch,
    AsOf AsOf);
