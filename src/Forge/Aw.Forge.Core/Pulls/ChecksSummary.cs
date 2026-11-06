namespace Aw.Forge.Core.Pulls;

/// <summary>The checks on the head commit.</summary>
/// <param name="State">The overall state.</param>
/// <param name="Sha">The commit the checks ran on.</param>
/// <param name="Failing">The names of the failing checks.</param>
/// <param name="Passed">How many checks passed (success, neutral or skipped).</param>
/// <param name="Pending">How many checks have not finished (queued, in progress or pending).</param>
/// <param name="Runs">Every check run (every page) and commit status on the commit, one entry each; null where the adapter did not read them.</param>
public sealed record ChecksSummary(ChecksState State, string Sha, IReadOnlyList<string> Failing, int Passed, int Pending, IReadOnlyList<CheckRun>? Runs = null);
