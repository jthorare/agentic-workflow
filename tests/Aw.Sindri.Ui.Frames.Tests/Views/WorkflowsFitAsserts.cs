using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Theme;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Shared checks of the #3683 frames: text at a larger scale and a long name must not push content out of its row.</summary>
internal static class WorkflowsFitAsserts
{
    /// <summary>Runs <paramref name="body"/> with the app's text scale set to <paramref name="scale"/>, and puts it back to 1.0 after.</summary>
    /// <param name="scale">The text scale, such as 1.3.</param>
    /// <param name="body">What to check.</param>
    /// <returns>A task that completes when the scale is back.</returns>
    internal static async Task AtScale(double scale, Func<Task> body)
    {
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            await body().ConfigureAwait(true);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    }

    /// <summary>Fails when a visible control inside <paramref name="container"/> reaches above or below it: the container clips it.</summary>
    /// <param name="container">A row or a cell.</param>
    /// <param name="what">What it is, for the message.</param>
    internal static void AssertNothingClips(Control container, string what)
    {
        foreach (var control in container.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Height > 0))
        {
            if (control.TranslatePoint(default, container) is not { } top)
            {
                continue;
            }

            var bottom = top.Y + control.Bounds.Height;
            Assert.True(top.Y >= -0.5 && bottom <= container.Bounds.Height + 0.5, string.Create(CultureInfo.InvariantCulture, $"{what}: {control.GetType().Name} '{control.Name}' spans y {top.Y}..{bottom} in a {container.Bounds.Height} px row, so the row clips it"));
        }
    }
}
