using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph;

/// <summary>A workflow: its name, which revision triggers run, and whether it is archived.</summary>
/// <param name="Id">The id.</param>
/// <param name="Slug">The name commands and branches use.</param>
/// <param name="Name">The name for people (from the latest revision).</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision, 1 or more.</param>
/// <param name="ArchivedAt">When it was archived, or null.</param>
/// <param name="CreatedBy">'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="WasActivated">True once a revision was pinned, and never cleared: off with this set is "paused", off without it is "never turned on" (#3683).</param>
public sealed record WorkflowRecord(WorkflowId Id, WorkflowSlug Slug, string Name, int? ActiveRevision, int LatestRevision, DateTimeOffset? ArchivedAt, string CreatedBy, bool WasActivated = false);
