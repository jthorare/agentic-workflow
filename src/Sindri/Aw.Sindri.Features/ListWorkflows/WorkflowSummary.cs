using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>One workflow in the list.</summary>
/// <param name="Slug">The name commands use.</param>
/// <param name="Name">The name for people.</param>
/// <param name="ActiveRevision">The revision triggers run, or null when the workflow is off.</param>
/// <param name="LatestRevision">The newest revision.</param>
/// <param name="ChangedBy">Who saved the newest revision: 'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="ChangedAt">When the newest revision was saved.</param>
internal sealed record WorkflowSummary(WorkflowSlug Slug, string Name, int? ActiveRevision, int LatestRevision, string ChangedBy, DateTimeOffset ChangedAt)
{
    /// <summary>True for an archived workflow (only in a list asked with includeArchived); absent otherwise, so the default answer is unchanged.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Archived { get; init; }

    /// <summary>'active' (triggers run), 'paused' (off after it was on: it was activated once and then paused) or 'off' (never turned on). Issue 3683.</summary>
    public string State { get; init; } = "";

    /// <summary>The trigger in words: 'By hand', 'Every weekday 7:00', 'Every 20 min'; the trigger's type when no words are known. Issue 3683.</summary>
    public string Trigger { get; init; } = "";

    /// <summary>The newest run's status word and start time, or null when the workflow never ran. Issue 3683.</summary>
    public WorkflowLastRun? LastRun { get; init; }

    /// <summary>The next tick of an active schedule, or null (by hand, paused, off, no tick). Issue 3683.</summary>
    public DateTimeOffset? NextRun { get; init; }

    /// <summary>The status words of the newest 10 runs, newest first (the strip). Issue 3683.</summary>
    public IReadOnlyList<string> RecentRuns { get; init; } = [];

    /// <summary>True when the last run failed, or the workflow is paused and has a schedule: the list's "Needs a look" group. Issue 3683.</summary>
    public bool NeedsLook { get; init; }
}
