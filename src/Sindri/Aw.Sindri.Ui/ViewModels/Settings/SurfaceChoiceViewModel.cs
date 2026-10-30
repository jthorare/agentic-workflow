using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>
/// Settings' read-only product-surface-choice line (model routing spec 8.5): off, on but needing a Jev key, or on, from what Sindri serves in the status (the app derives none of it), and the sentence under it.
/// There is no edit verb: the profile changes it, and an approval puts it in force.
/// </summary>
internal sealed class SurfaceChoiceViewModel : ObservableObject
{
    /// <summary>The heading.</summary>
#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    public string Title => Labels.SurfaceChoiceTitle;

    /// <summary>The sentence under the line.</summary>
    public string Sentence => Labels.SurfaceChoiceSentence;
#pragma warning restore CA1822, MA0041

    /// <summary>The line in words; the off words until the first status.</summary>
    public string Line { get; private set => Set(ref field, value); } = Labels.SurfaceChoiceOff;

    /// <summary>A status arrived: the line follows its profile part.</summary>
    /// <param name="profile">The status's profile part.</param>
    internal void Show(ProfileView profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Line = profile.SurfacePick switch
        {
            "on" => Labels.SurfaceChoiceOn,
            "needs-key" => Labels.SurfaceChoiceNeedsKey,
            _ => Labels.SurfaceChoiceOff,
        };
    }
}
