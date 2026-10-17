using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="ILaunchFiles"/> in memory: a launch is open from <see cref="Open"/> until <see cref="Forget"/>, and the test reads what was forgotten.</summary>
public sealed class FakeLaunchFiles : ILaunchFiles
{
    /// <summary>The proof the request builders carry by default.</summary>
    public const string DefaultProof = "launch-proof-for-tests";

    private readonly Dictionary<LaunchId, string> _open = [];
    private readonly List<LaunchId> _forgotten = [];
    private readonly List<LaunchId> _swept = [];

    /// <summary>The launches whose files were deleted, in order.</summary>
    public IReadOnlyList<LaunchId> Forgotten => _forgotten;

    /// <summary>The launches the start-up sweep removed.</summary>
    public IReadOnlyList<LaunchId> Swept => _swept;

    /// <summary>Makes <paramref name="launch"/> open with <paramref name="proof"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The proof it accepts.</param>
    public void Open(LaunchId launch, string proof = DefaultProof) => _open[launch] = proof;

    /// <inheritdoc/>
    public Result<Unit> Verify(LaunchId launch, LaunchSecret secret) =>
        _open.TryGetValue(launch, out var proof) && string.Equals(proof, secret.Value, StringComparison.Ordinal)
            ? Unit.Value
            : SindriErrors.AuthLaunchSecret.With("the launch secret doesn't match an open launch");

    /// <inheritdoc/>
    public void Forget(LaunchId launch)
    {
        _ = _open.Remove(launch);
        _forgotten.Add(launch);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LaunchId> Launches() => [.. _open.Keys];

    /// <inheritdoc/>
    public int SweepStale(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        var stale = _open.Keys.Where(launch => !live(launch)).ToList();
        foreach (var launch in stale)
        {
            _ = _open.Remove(launch);
            _swept.Add(launch);
        }

        return stale.Count;
    }
}
