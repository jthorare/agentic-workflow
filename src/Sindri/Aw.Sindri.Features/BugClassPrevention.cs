namespace Aw.Sindri.Features;

// why: the root namespace, not BugFix: the Pr and Review slices tell their repair Steps the same rule and slices never name each other (AW1003)
/// <summary>The prevent-the-class rule every bug-fix and repair Step is told (skills/_shared/prevent-the-class.md, #1682): the question word for word and the lines that follow it. Plain text, no fact content.</summary>
internal static class BugClassPrevention
{
    /// <summary>The owner's question, the shared file's quoted line word for word (a test holds the two equal).</summary>
    internal const string Question = "How can we prevent this class of bugs from happening again—not just fix this instance? Consider enforceable constraints such as lint rules, analyzers, type/API design, shared primitives, state-machine or architecture changes, and checks that catch other instances of the same failure.";

    /// <summary>The lines every bug-fix Step gets, in order: ask once the cause is confirmed and before choosing a fix.</summary>
    internal static IReadOnlyList<string> SharedLines { get; } =
    [
        "Prevent the class, not just the instance: once the cause is confirmed and before you choose a fix, ask yourself: " + Question,
        "Name the failure class and the invariant that should hold, one sentence each, from the code and evidence you have. Don't invent defects you haven't seen.",
        "Search for other instances of the same pattern and list them with file:line, or say how you searched and found none.",
        "Pick the smallest mechanism that holds the invariant: an existing guard that already covers it (say which), a lint or analyzer rule, a type or API constraint, one shared primitive, a data constraint, a state-machine change, or a behavioural check run over every instance. Say why it covers the class. A regression test for one fixture is not class-wide prevention.",
        "Keep mitigation and prevention apart: a timeout, a retry or a guard at one call site is mitigation. When the prevention can't be built inside the allowed scope, say what remains and name a concrete follow-up (the class, the instances, the proposed mechanism).",
        "Never claim the class is gone without the mechanism's own proof; say what it covers and what it doesn't. The full rule and three worked examples: $HOME/.agentic-workflow/toolkit/skills/_shared/prevent-the-class.md.",
    ];
}
