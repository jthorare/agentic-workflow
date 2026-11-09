using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>One workflow in the list.</summary>
/// <param name="Slug">The name commands use.</param>
/// <param name="Name">The name for people.</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision.</param>
/// <param name="ChangedBy">Who saved the newest revision: 'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="ChangedAt">When the newest revision was saved.</param>
internal sealed record WorkflowSummary(WorkflowSlug Slug, string Name, int? ActiveRevision, int LatestRevision, string ChangedBy, DateTimeOffset ChangedAt);
