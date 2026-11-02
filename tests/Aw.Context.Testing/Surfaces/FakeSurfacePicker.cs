using Aw.Context.Core.Surfaces;

namespace Aw.Context.Testing.Surfaces;

/// <summary>An <see cref="ISurfacePicker"/> that answers from a script by group key (anything unscripted is separate and undecided) and records every call.</summary>
public sealed class FakeSurfacePicker : ISurfacePicker
{
    private readonly Dictionary<string, Func<SurfaceGroup, SurfaceVerdict>> _script = new(StringComparer.Ordinal);

    /// <summary>The number of <see cref="Decide"/> calls.</summary>
    public int Calls { get; private set; }

    /// <summary>The groups each call was given.</summary>
    public IList<IReadOnlyList<SurfaceGroup>> Asked { get; } = [];

    /// <summary>The options each call was given.</summary>
    public IList<SurfacePickOptions> Options { get; } = [];

    /// <summary>When set, every call reports it as the reason Jev did not decide.</summary>
    public string? NotDecided { get; set; }

    /// <summary>Scripts the verdict for the group whose candidates hold the given node key.</summary>
    /// <param name="nodeKey">A candidate's node key.</param>
    /// <param name="verdict">Makes the verdict from the group as the build made it.</param>
    public void When(string nodeKey, Func<SurfaceGroup, SurfaceVerdict> verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        _script[nodeKey] = verdict;
    }

    /// <inheritdoc/>
    public ValueTask<SurfacePickResult> Decide(IReadOnlyList<SurfaceGroup> groups, SurfacePickOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        Calls++;
        Asked.Add(groups);
        Options.Add(options);
        var verdicts = groups.Select(g => _script.FirstOrDefault(s => g.Candidates.Any(c => string.Equals(c.NodeKey, s.Key, StringComparison.Ordinal))).Value is { } make ? make(g) : SurfaceVerdict.Separate(g.Key, decided: false)).ToList();
        return ValueTask.FromResult(new SurfacePickResult(verdicts, new SurfaceCounts(0, groups.Count, 0, 0), NotDecided));
    }
}
