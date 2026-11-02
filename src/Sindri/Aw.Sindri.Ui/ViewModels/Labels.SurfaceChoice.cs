namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of Settings' product-surface-choice line (model routing spec 8.5).</summary>
internal static partial class Labels
{
    /// <summary>The heading.</summary>
    internal const string SurfaceChoiceTitle = "Product surface choice";

    /// <summary>The product-surface pick is off: candidates enter the map as the build found them.</summary>
    internal const string SurfaceChoiceOff = "Product surface choice: off";

    /// <summary>The pick is on but Jev has no key, so nothing is decided (the doctor's wording).</summary>
    internal const string SurfaceChoiceNeedsKey = "Product surface choice: on, but needs an API key";

    /// <summary>The pick is in force.</summary>
    internal const string SurfaceChoiceOn = "Product surface choice: on";

    /// <summary>The sentence under the line: what Jev reads, where to change it, whose retention applies.</summary>
    internal const string SurfaceChoiceSentence = "Jev reads each candidate's name, file paths and edges to decide whether lookalikes are one surface. Change it in the profile; it takes effect once you approve. Retention is the provider's.";
}
