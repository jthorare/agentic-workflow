namespace Aw.Context.Testing.Fixtures;

/// <summary>The anchors a correct answer to a probe contains (and, for <see cref="AbsentNodes"/>, must not).</summary>
/// <param name="Nodes">Graph node keys (§6.1 key scheme) the answer names.</param>
/// <param name="AbsentNodes">Node keys the answer must not name, e.g. a sibling the cascade must not reach.</param>
/// <param name="Records">Canonical record keys the answer cites.</param>
/// <param name="Files">Repo-relative paths the answer points at.</param>
/// <param name="Decisions">Decisions the answer carries, with their status and any code conflict.</param>
public sealed record ProbeExpectation(
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> AbsentNodes,
    IReadOnlyList<string> Records,
    IReadOnlyList<string> Files,
    IReadOnlyList<ProbeDecision> Decisions);
