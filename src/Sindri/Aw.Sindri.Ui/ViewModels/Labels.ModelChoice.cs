namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of Settings' model-choice line (model-pick spec 8).</summary>
internal static partial class Labels
{
    /// <summary>The heading and the Copy button's subject.</summary>
    internal const string ModelChoiceTitle = "Model choice";

    /// <summary>Model choice is in force.</summary>
    internal const string ModelChoiceOn = "Model choice: on";

    /// <summary>Model choice is off.</summary>
    internal const string ModelChoiceOff = "Model choice: off. Steps use the agent's default model; nothing picks one per step.";

    /// <summary>The sentence under the line: what Jev reads, where to change it, whose retention applies.</summary>
    internal const string ModelChoiceSentence = "A model reads each step's brief and files to pick the cheapest model that works. Change it in the profile; it takes effect when you save. Retention is the provider's.";

    /// <summary>The command Copy adds under the profile file's path.</summary>
    internal const string ModelChoiceValidate = "sindri profile validate";
}
