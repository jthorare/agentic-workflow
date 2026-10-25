namespace Aw.Context.Core.History;

/// <summary>The history lens's rules. It has no caps on what it lists (owner q89/q93); the git read just never stops short of the end.</summary>
public static class HistoryRules
{
    /// <summary>What one git read is asked for per repo: all of it (the read adds one to see whether more follow, so this stays below the int maximum).</summary>
    public const int ChangesPerRepo = int.MaxValue - 1;

    /// <summary>A change's subject is cut at this many characters.</summary>
    public const int SubjectChars = 200;
}
