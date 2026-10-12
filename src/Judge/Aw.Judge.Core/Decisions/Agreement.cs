namespace Aw.Judge.Core.Decisions;

/// <summary>How the fallback rules' opinion compared with the decision: stored as 'agreed', 'overrode' or 'undecided'.</summary>
public enum Agreement
{
    /// <summary>The rules would have decided the same.</summary>
    Agreed = 1,

    /// <summary>The provider decided differently from the rules.</summary>
    Overrode = 2,

    /// <summary>No provider decided; the rules settled it.</summary>
    Undecided = 3,
}
