using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Core.Decisions;

/// <summary>The label a decision carries (UI spec E-7).</summary>
public static class DecisionLabels
{
    /// <summary>'decision · re-scope', 'decision · add task', 'decision · note' or 'decision · hand back'.</summary>
    /// <param name="kind">What the decision decides.</param>
    /// <param name="requestKind">The kind of its request.</param>
    /// <returns>The label.</returns>
    public static string For(DecisionKind kind, RequestKind requestKind) => kind switch
    {
        DecisionKind.AcceptRequest => "decision · " + RequestKindWords.Of(requestKind),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a decision kind"),
    };
}
