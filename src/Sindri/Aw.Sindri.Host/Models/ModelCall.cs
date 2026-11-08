using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>One Ask in flight: what it was asked, the route it resolved, and the tries not yet written as an event.</summary>
/// <param name="request">The request.</param>
/// <param name="settings">'context.models' as approved.</param>
/// <param name="job">The job the call runs under, or null.</param>
/// <param name="installed">The harnesses on PATH, in the lineup's order.</param>
internal sealed class ModelCall(ModelRequest request, ModelsSettings settings, JobContext? job, IReadOnlyList<InstalledHarness> installed)
{
    public ModelRequest Request { get; } = request;

    public ModelKind Kind { get; } = ModelKinds.Of(request.Purpose);

    public ModelsSettings Settings { get; } = settings;

    public JobContext? Job { get; } = job;

    public IReadOnlyList<InstalledHarness> Installed { get; } = installed;

    /// <summary>The harnesses to try, each with its model.</summary>
    public IReadOnlyList<RoutedModel> Route { get; set; } = [];

    /// <summary>The tries of the walk in progress: the pause and the 042 check read these.</summary>
    public List<ModelCallTry> Pass { get; set; } = [];

    /// <summary>The tries not yet written as an event.</summary>
    public List<ModelCallTry> Tries { get; set; } = [];

    /// <summary>The number of tries already written, for deciding whether an empty segment is worth an event.</summary>
    public int Written { get; set; }

    public DateTimeOffset SegmentAt { get; set; }

    public long SegmentStamp { get; set; }

    /// <summary>Whether the walk in progress met a usage limit, now or earlier in the job.</summary>
    public bool HitLimit => Pass.Exists(static t => IsLimit(t));

    /// <summary>Whether a try is a limit: the harness said so, or it was skipped for having said so earlier in the job.</summary>
    /// <param name="attempt">The try.</param>
    /// <returns>True when it is.</returns>
    public static bool IsLimit(ModelCallTry attempt) => attempt.Outcome == ModelCallOutcome.Limit || (attempt.Outcome == ModelCallOutcome.Skipped && string.Equals(attempt.Reason, "limited", StringComparison.Ordinal));
}
