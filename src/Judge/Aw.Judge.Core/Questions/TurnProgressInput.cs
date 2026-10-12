namespace Aw.Judge.Core.Questions;

/// <summary>turn-progress's input: the problem and what one turn changed.</summary>
/// <param name="Problem">The problem being worked on (must not be blank).</param>
/// <param name="TurnDiff">The change made in this turn.</param>
/// <param name="AcceptanceCriteria">The acceptance criteria, when known.</param>
/// <param name="PriorDiffStat">The diff stat before this turn, when known.</param>
/// <param name="Signals">Other signals (test results, errors), when known.</param>
public sealed record TurnProgressInput(string Problem, string TurnDiff, string AcceptanceCriteria = "", string PriorDiffStat = "", string Signals = "");
