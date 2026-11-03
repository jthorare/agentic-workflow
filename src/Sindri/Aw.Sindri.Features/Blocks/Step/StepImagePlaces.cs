namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>Whether an image's real path sits under one of the real directories a 'step.judge.choose' image may come from (the file system is the <see cref="Core.Judge.IStepImageFiles"/> port's).</summary>
internal static class StepImagePlaces
{
    /// <summary>Whether <paramref name="real"/> is inside one of <paramref name="roots"/>.</summary>
    /// <param name="real">The image's real path.</param>
    /// <param name="roots">The allowed directories' real paths.</param>
    /// <returns>True when it is inside one.</returns>
    public static bool Inside(string real, IReadOnlyList<string> roots) =>
        roots.Any(root => real.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
