namespace Aw.Context.Core.Walks;

/// <summary>
/// How many cores the context engine's read-only walks (the per-anchor cascades and coverage counts, the per-repo git reads) may use at once. The degree changes how long a fill takes, never what it answers:
/// every walk hands its per-item results back in input order and the merge stays serial.
/// </summary>
public sealed class WalkOptions
{
    private WalkOptions(int degree)
    {
        Degree = degree;
    }

    /// <summary>One item at a time, on the calling thread.</summary>
    public static WalkOptions Serial { get; } = new(1);

    /// <summary>The most items in flight at once; 1 is serial.</summary>
    public int Degree { get; }

    /// <summary>The options for <paramref name="degree"/> items at once.</summary>
    /// <param name="degree">The most items in flight; below 2 is <see cref="Serial"/>.</param>
    /// <returns>The options.</returns>
    public static WalkOptions Of(int degree) => degree < 2 ? Serial : new WalkOptions(degree);
}
