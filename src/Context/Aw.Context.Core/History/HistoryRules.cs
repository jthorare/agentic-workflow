namespace Aw.Context.Core.History;

/// <summary>The fixed caps of the history lens (S-H decision 16; no profile key).</summary>
public static class HistoryRules
{
    /// <summary>The implementing files searched per anchor set.</summary>
    public const int MaxFiles = 200;

    /// <summary>The most changes one git read returns per repo; a read that reaches it is flagged capped (the lists themselves are not cut: owner q93, 2026-10-23).</summary>
    public const int ChangesPerRepo = 10_000;

    /// <summary>A change's subject is cut at this many characters.</summary>
    public const int SubjectChars = 200;
}
