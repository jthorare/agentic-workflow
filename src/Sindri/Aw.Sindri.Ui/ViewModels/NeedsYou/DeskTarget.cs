using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>An open desk run as the instance serves it: where a report goes, and the command the step serves for completing it.</summary>
/// <param name="InstanceId">The instance's id.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Complete">The step's served complete command; null when none is served.</param>
/// <param name="Why">The desk state's served why; null or empty when the recipe serves none (or could not be read).</param>
/// <param name="IfApprove">What approving does, as the state's approve outcome says; null when not served.</param>
/// <param name="IfSendBack">What sending it back does, as the state's first revise outcome says; null when not served.</param>
/// <param name="Recommended">The outcome the step before the stop recommends, with its why; null when none is served.</param>
/// <param name="Questions">The ask step's questions, read from the escalation fact; null for every other desk.</param>
/// <param name="MergeCard">The pull request card, when the instance lists the fact "fact:prStatus" and it read; null otherwise.</param>
internal sealed record DeskTarget(string InstanceId, string Run, StatusCommand? Complete, string? Why = null, string? IfApprove = null, string? IfSendBack = null, DeskRecommendation? Recommended = null, MergeCardViewModel? MergeCard = null, IReadOnlyList<DeskQuestion>? Questions = null)
{
    /// <summary>The recipe verbs the instance accepts now, in the order it serves them; empty when it serves none.</summary>
    internal IReadOnlyList<DeskVerb> Verbs { get; init; } = [];

    /// <summary>True when the step serves its complete command as protected: only the user's terminal may send it (a merge), so the app shows the served command and sends nothing.</summary>
    internal bool IsTerminalOnly => Complete is { } command && string.Equals(command.Class, "protected", StringComparison.Ordinal);
}
