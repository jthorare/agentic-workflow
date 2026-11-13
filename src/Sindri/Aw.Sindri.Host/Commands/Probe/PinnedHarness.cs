namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// One harness of the pinned probe (engine spec §6.2): what its golden report under tests/Aw.Sindri.Tests/Golden/Probe/
/// says, reduced to what the CLI reads. The golden test 'Agent_variables_match_the_probe' keeps the two equal.
/// </summary>
/// <param name="Harness">The harness, e.g. 'claude-code', 'codex:workspace-write', 'cursor-terminal'; its golden is the name with ':' as '-', plus '.json'.</param>
/// <param name="Family">The harness family the CLI can recognise without being told the mode: 'claude-code', 'codex', 'cursor', 'terminal'.</param>
/// <param name="Human">Whether this is a terminal a human uses (the plain terminal, Cursor's integrated terminal): what it sets is what humans also see, so no marker may be among it.</param>
/// <param name="Pending">Whether the probe is an owner step not run yet ('pending: owner probe'): the markers and processes are then API spec §6's provisional ones; 'doctor' shows 'pending owner probe: provisional markers' as ok, and warns only under '--dev'.</param>
/// <param name="VersionCommand">The command whose '--version' 'doctor' compares with <paramref name="Version"/>, or null.</param>
/// <param name="Version">The version the probe ran under, or null.</param>
/// <param name="Markers">The variables that mark a session of this harness: set by it, never in a human's terminal.</param>
/// <param name="Processes">The ancestry patterns that mark it.</param>
/// <param name="Excluded">The variables the harness sets that are not markers, each with the reason.</param>
internal sealed record PinnedHarness(
    string Harness,
    string Family,
    bool Human,
    bool Pending,
    string? VersionCommand,
    string? Version,
    IReadOnlyList<string> Markers,
    IReadOnlyList<HarnessProcess> Processes,
    IReadOnlyDictionary<string, string> Excluded)
{
    /// <summary>What a pending golden says.</summary>
    internal const string PendingNote = "owner probe";

    /// <summary>The golden file's name.</summary>
    internal string GoldenFile => Harness.Replace(':', '-') + ".json";
}
