namespace Aw.Sindri.Features.Scoping;

/// <summary>The facts a scoping instance holds that the checks and the stop view read, by key without the 'fact:' prefix.</summary>
public static class ScopingFactKeys
{
    /// <summary>The keys, in the order the view's sources list them.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "brief", "lenses", "coverage", "scopeMap", "acceptance", "answers", "answerLog", "challenge", "openQuestions", "questions",
        "decision", "anchors", "runs", "rounds", "scopePacket", "relevance", "gapSearch",
    ];
}
