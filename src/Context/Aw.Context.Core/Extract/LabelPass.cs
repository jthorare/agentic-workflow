namespace Aw.Context.Core.Extract;

/// <summary>What a labeling pass named.</summary>
/// <param name="Labels">A label per cluster the model could name.</param>
/// <param name="Complete">False when a model call failed after some batches had been answered: <paramref name="Labels"/> holds what those named, and the pass is degraded.</param>
/// <param name="Unavailable">Why no model could be asked (labeling is on, none answers), with the fix: the build goes on without labels and says it as one degraded SND-CONTEXT-042 note. Null when a model answered.</param>
public sealed record LabelPass(IReadOnlyList<ClusterLabel> Labels, bool Complete = true, string? Unavailable = null)
{
    /// <summary>The fix a note about a missing model names.</summary>
    public const string NoModelFix = "log in to an agent CLI (claude, codex or cursor-agent) so Sindri can ask it, or set 'context.labeling.enabled: false' in the profile";

    /// <summary>How each label was chosen, when the pass had the Jev pick on; null otherwise.</summary>
    public LabelHow? Chosen { get; init; }

    /// <summary>The reason code Jev could not be asked for the rest of the pass ('network-error', 'http-429', ...), or null. The build says 'labels: jev unavailable (reason)'. Never set for a pick that was off or had no key: that is today's behavior, not a degradation.</summary>
    public string? PickUnavailable { get; init; }
}
