using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Aw.Sindri.Ui.Theme;

/// <summary>
/// Lets a key-frame animation move a whole RenderTransform (#2535): a row that slides in animates "translateY(-12px)" to
/// "translateY(0px)" and sets no transform of its own, so the clickable press scale applies to it like any other row.
/// Avalonia 12 animates only the double properties of a Transform in key frames (its own transform-operations animator is
/// internal), so without this every slide-in throws "No animator registered for the property RenderTransform".
/// </summary>
internal sealed class TransformKeyFrames : InterpolatingAnimator<ITransform>
{
    private static int _registered;

    /// <summary>Adds the animator to Avalonia's list once per process; the app's Initialize calls it before the theme loads.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            Animation.RegisterCustomAnimator<ITransform, TransformKeyFrames>();
        }
    }

    /// <inheritdoc />
    public override ITransform Interpolate(double progress, ITransform oldValue, ITransform newValue) =>
        TransformOperations.Interpolate(Operations(oldValue), Operations(newValue), progress);

    // A "none" or a Transform object is read as no movement, as Avalonia's own transition does.
    private static TransformOperations Operations(ITransform value) => value as TransformOperations ?? TransformOperations.Identity;
}
