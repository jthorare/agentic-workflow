namespace Aw.Meta.Core.Merge;

/// <summary>One "N passed, M skipped, K failed" line of a ready message.</summary>
/// <param name="Passed">The tests that passed.</param>
/// <param name="Skipped">The tests that were skipped.</param>
/// <param name="Failed">The tests that failed.</param>
public sealed record CountLine(int Passed, int Skipped, int Failed);
