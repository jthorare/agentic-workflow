using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>A workflow with the revision it runs (or, when it is off, its newest one).</summary>
/// <param name="Id">The id.</param>
/// <param name="Slug">The name commands use.</param>
/// <param name="Name">The name for people.</param>
/// <param name="Revision">The revision shown: the active one, else the newest.</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision.</param>
/// <param name="ArchivedAt">When it was archived, or null.</param>
/// <param name="CreatedBy">Who made the workflow: 'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="ChangedBy">Who saved its newest revision.</param>
/// <param name="ChangedAt">When the newest revision was saved.</param>
/// <param name="SpecHash">The shown revision's hash.</param>
/// <param name="Spec">The shown revision's graph, the stored document; GraphJson.Parse reads it back.</param>
/// <param name="Positions">Where each node of the graph is drawn.</param>
internal sealed record GetWorkflowResponse(
    WorkflowId Id,
    WorkflowSlug Slug,
    string Name,
    int Revision,
    int? ActiveRevision,
    int LatestRevision,
    DateTimeOffset? ArchivedAt,
    string CreatedBy,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string SpecHash,
    JsonNode Spec,
    IReadOnlyList<NodePosition> Positions);
