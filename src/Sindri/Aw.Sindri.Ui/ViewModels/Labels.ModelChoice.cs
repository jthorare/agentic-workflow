namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of Settings' model-choice line (model-pick spec 8).</summary>
internal static partial class Labels
{
    /// <summary>The heading and the Copy button's subject.</summary>
    internal const string ModelChoiceTitle = "Model choice";

    /// <summary>Model choice is in force.</summary>
    internal const string ModelChoiceOn = "Model choice: on";

    /// <summary>Model choice is off.</summary>
    internal const string ModelChoiceOff = "Model choice: off. Steps use the agent's default, with no Jev call.";

    /// <summary>The profile has a pending change, so which value is in force is not the question.</summary>
    internal const string ModelChoiceWaiting = "The profile has a change waiting for your approval";

    /// <summary>The sentence under the line: what Jev reads, where to change it, whose retention applies.</summary>
    internal const string ModelChoiceSentence = "Jev reads each step's brief and files to pick the cheapest model that works. Change it in the profile; it takes effect once you approve. Retention is the provider's.";

    /// <summary>The two commands Copy adds under the profile file's path.</summary>
    internal const string ModelChoiceValidate = "sindri profile validate";

    /// <summary>The approve command.</summary>
    internal const string ModelChoiceApprove = "sindri profile approve";
}
