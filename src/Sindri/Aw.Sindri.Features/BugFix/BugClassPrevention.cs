namespace Aw.Sindri.Features.BugFix;

/// <summary>The prevent-the-class rule every bug-fix and repair Step is told (skills/_shared/prevent-the-class.md, #1682): the question word for word and the lines that follow it. Plain text, no fact content.</summary>
internal static class BugClassPrevention
{
    /// <summary>The owner's question, the shared file's quoted line word for word (a test holds the two equal).</summary>
    internal const string Question = "How can we prevent this class of bugs from happening again—not just fix this instance? Consider enforceable constraints such as lint rules, analyzers, type/API design, shared primitives, state-machine or architecture changes, and checks that catch other instances of the same failure.";

    /// <summary>The lines every bug-fix Step gets, in order: ask once the cause is confirmed and before choosing a fix.</summary>
    internal static IReadOnlyList<string> SharedLines => throw new NotSupportedException();
}
