using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the skill card of the expanded state (skill flows spec §5.2, §5.4).</summary>
internal static partial class Labels
{
    /// <summary>The state words of a skill card.</summary>
    internal const string SkillWaitingToStart = "waiting to start", SkillRunning = "running", SkillWaitingOnYou = "waiting on you", SkillDone = "done", SkillFailed = "failed";

    /// <summary>The cost line when the launch reports none.</summary>
    internal const string SkillCostNotReported = "cost: not reported";

    /// <summary>The resting text of the card's live region: never empty.</summary>
    internal const string SkillCardResting = "Skill status";

    /// <summary>The accessible name of an output's Open.</summary>
    /// <param name="path">The output's path.</param>
    /// <returns>The name.</returns>
    internal static string SkillOutputOpenName(string path) => $"Open {path}";

    /// <summary>The accessible name of an output's Copy path.</summary>
    /// <param name="path">The output's path.</param>
    /// <returns>The name.</returns>
    internal static string SkillOutputCopyName(string path) => $"Copy path {path}";

    /// <summary>The Copy path button.</summary>
    internal const string SkillCopyPath = "Copy path";

    /// <summary>The closed details' heading.</summary>
    internal const string SkillDetails = "Details";

    /// <summary>The Open button of an output.</summary>
    internal const string SkillOpen = "Open";

    /// <summary>The card's heading for its outputs.</summary>
    internal const string SkillOutputsHeading = "Outputs";

    /// <summary>A state name that starts a sentence: the id as served ("mitigate") gets its first letter capitalised.</summary>
    private static string Sentence(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>The waiting row's text: the question.</summary>
    /// <param name="name">The state in the recipe's words.</param>
    /// <param name="question">The skill's question.</param>
    /// <returns>The sentence.</returns>
    internal static string SkillWaitingQuestion(string name, string question) => $"{Sentence(name)} is waiting on you: {question}";

    /// <summary>The waiting row's text when the session ended a turn without an answer.</summary>
    /// <param name="name">The state in the recipe's words.</param>
    /// <returns>The sentence.</returns>
    internal static string SkillNoResult(string name) => $"{Sentence(name)} stopped without giving an answer. Open the session to see why.";

    /// <summary>The waiting row's text when the answer file could not be read.</summary>
    /// <param name="name">The state in the recipe's words.</param>
    /// <returns>The sentence.</returns>
    internal static string SkillUnreadable(string name) => $"{Sentence(name)} finished, but its answer file is broken. Open the session and ask it to write the answer again.";

    /// <summary>The waiting row's text when the skill changed under a running block.</summary>
    /// <param name="skill">The skill in words.</param>
    /// <returns>The sentence.</returns>
    internal static string SkillUpdated(string skill) => $"The {skill} skill changed since this run started. Approve again to continue.";

    /// <summary>The loop round line.</summary>
    /// <param name="n">The round.</param>
    /// <param name="max">The cap.</param>
    /// <returns>The line.</returns>
    internal static string SkillRound(int n, int max) => string.Create(CultureInfo.InvariantCulture, $"round {n} of {max}");

    /// <summary>The cost line.</summary>
    /// <param name="dollars">The reported cost.</param>
    /// <returns>The line.</returns>
    internal static string SkillCost(decimal dollars) => string.Create(CultureInfo.InvariantCulture, $"cost: ${dollars:0.00}");
}
