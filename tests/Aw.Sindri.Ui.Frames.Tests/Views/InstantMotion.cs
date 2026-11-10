using Avalonia;
using Aw.Sindri.Ui.Theme;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Zero Motion.* durations in the app's resources for one test, then the token values back (#2535). The fake reduce-motion
/// setting reaches only the view models; the clickable transitions read Motion.Hover from the resources, which the theme-only
/// test app never rewrites, so without this a hover colour is still easing when the test reads it.
/// </summary>
internal sealed class InstantMotion : IDisposable
{
    private InstantMotion()
    {
        Motion.Apply(Application.Current!.Resources, reduce: true);
    }

    /// <summary>Zeroes the durations until the result is disposed.</summary>
    /// <returns>The scope.</returns>
    public static InstantMotion Start() => new();

    /// <summary>Puts the token durations back.</summary>
    public void Dispose() => Motion.Apply(Application.Current!.Resources, reduce: false);
}
