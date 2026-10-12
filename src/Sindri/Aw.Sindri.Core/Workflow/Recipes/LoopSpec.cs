using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A loop (spec §3.7): '{ cap, noProgress }'. The long form always lists the no-progress signals (default: findings, emptyDiff, sameFailure).</summary>
/// <param name="Cap">The rounds allowed, at least 1.</param>
/// <param name="NoProgress">The signals that count a round as no progress; never empty.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoopSpec(int Cap, IReadOnlyList<NoProgressSignal> NoProgress)
{
    /// <summary>The signals a loop uses when the recipe doesn't name them.</summary>
    public static IReadOnlyList<NoProgressSignal> DefaultNoProgress { get; } =
        [NoProgressSignal.Findings, NoProgressSignal.EmptyDiff, NoProgressSignal.SameFailure];
}
