namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of Settings' label-choice line (model routing spec 8.5).</summary>
internal static partial class Labels
{
    /// <summary>The heading.</summary>
    internal const string LabelChoiceTitle = "Label choice";

    /// <summary>The label pick is off: names are the model's first label.</summary>
    internal const string LabelChoiceOff = "Label choice: off";

    /// <summary>The label pick is on but Jev has no key, so the model's first label is used (the doctor's wording).</summary>
    internal const string LabelChoiceNeedsKey = "Label choice: on, but needs an API key";

    /// <summary>The label pick is in force.</summary>
    internal const string LabelChoiceOn = "Label choice: on";

    /// <summary>The sentence under the line: what Jev reads, where to change it, whose retention applies.</summary>
    internal const string LabelChoiceSentence = "Jev reads each feature's file paths and the model's candidate names to pick the name shown. Change it in the profile; it takes effect once you approve. Retention is the provider's.";
}
