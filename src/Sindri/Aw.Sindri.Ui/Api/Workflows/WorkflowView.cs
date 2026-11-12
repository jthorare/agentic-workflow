using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows/{slug}: a workflow with the revision it runs (or, when it is off, its newest one).</summary>
/// <param name="Id">The id (WF-…).</param>
/// <param name="Slug">The name commands use.</param>
/// <param name="Name">The name for people.</param>
/// <param name="Revision">The revision shown: the active one, else the newest.</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision.</param>
/// <param name="ArchivedAt">When it was archived, or null.</param>
/// <param name="CreatedBy">Who made the workflow: "owner" or "session:&lt;id&gt;".</param>
/// <param name="ChangedBy">Who saved its newest revision.</param>
/// <param name="ChangedAt">When the newest revision was saved.</param>
/// <param name="SpecHash">The shown revision's hash.</param>
/// <param name="Spec">The shown revision's graph, the stored document, kept raw: the page counts its nodes and draws nothing yet.</param>
/// <param name="Positions">Where each node is drawn.</param>
/// <param name="Summaries">The line of each node, in plain words; null from a Sindri that sends none.</param>
internal sealed record WorkflowView(
    string Id,
    string Slug,
    string Name,
    int Revision,
    int? ActiveRevision,
    int LatestRevision,
    DateTimeOffset? ArchivedAt,
    string CreatedBy,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string SpecHash,
    JsonElement Spec,
    IReadOnlyList<NodePositionView> Positions,
    IReadOnlyList<NodeSummaryView>? Summaries = null);
