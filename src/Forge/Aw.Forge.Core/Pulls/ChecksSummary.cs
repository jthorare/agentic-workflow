namespace Aw.Forge.Core.Pulls;

/// <summary>The checks on the head commit.</summary>
/// <param name="State">The overall state.</param>
/// <param name="Sha">The commit the checks ran on.</param>
/// <param name="Failing">The names of the failing checks.</param>
public sealed record ChecksSummary(ChecksState State, string Sha, IReadOnlyList<string> Failing);
