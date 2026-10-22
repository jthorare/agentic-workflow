using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Where the run canvas looks (F3 spec 3 §2, "Deterministic layout", step 5). Pure: sizes in, a view out.</summary>
internal static class RunViewport
{
    /// <summary>The smallest scale the run canvas allows.</summary>
    internal const double MinZoom = 0.30;

    /// <summary>The largest scale the run canvas allows.</summary>
    internal const double MaxZoom = 1.50;

    /// <summary>The least space above the active stage.</summary>
    private const double TopMargin = 84;

    /// <summary>The room kept round the world when it is fitted.</summary>
    private const double FitMargin = 60;

    /// <summary>The active stage at 100 %: centred across the visible width (the inspector's width taken off), its top at 84 px or centred when it is shorter than the stage.</summary>
    /// <param name="active">The active stage's rectangle in the world.</param>
    /// <param name="width">The stage's width.</param>
    /// <param name="height">The stage's height.</param>
    /// <param name="rightInset">The width of the panel over the stage's right edge.</param>
    /// <returns>The view.</returns>
    internal static RunFocus FocusActive(MapRect active, double width, double height, double rightInset)
    {
        var visible = Math.Max(0, width - rightInset);
        var top = Math.Max(TopMargin, (height - active.Height) / 2);
        return new RunFocus(1, (visible / 2) - (active.X + (active.Width / 2)), top - active.Y);
    }

    /// <summary>The whole world on the stage: the scale is the smaller of the two fits (60 px kept round), held between the zoom limits and 100 %, and the world is centred.</summary>
    /// <param name="world">The world's rectangle.</param>
    /// <param name="width">The stage's width.</param>
    /// <param name="height">The stage's height.</param>
    /// <returns>The view.</returns>
    internal static RunFocus Fit(MapRect world, double width, double height)
    {
        if (world.Width <= 0 || world.Height <= 0 || width <= 0 || height <= 0)
        {
            return new RunFocus(1, 0, 0);
        }

        var scale = Math.Clamp(Math.Min((width - FitMargin) / world.Width, (height - FitMargin) / world.Height), MinZoom, 1.0);
        return new RunFocus(scale, ((width - (world.Width * scale)) / 2) - (world.X * scale), ((height - (world.Height * scale)) / 2) - (world.Y * scale));
    }
}
