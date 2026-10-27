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
    private readonly Dictionary<LaunchId, DateTimeOffset> _turns = [];
    private readonly Dictionary<LaunchId, DateTimeOffset> _failed = [];
    private readonly Dictionary<LaunchId, DateTimeOffset> _nudged = [];
    private readonly HashSet<LaunchId> _tools = [];
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

    /// <summary>Says <paramref name="launch"/>'s turn ended at <paramref name="at"/> (the Stop hook touched its file).</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="at">When.</param>
    public void EndTurn(LaunchId launch, DateTimeOffset at) => _turns[launch] = at;

    /// <summary>Says an API error ended <paramref name="launch"/>'s turn at <paramref name="at"/> (the StopFailure hook touched its file).</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="at">When.</param>
    public void FailTurn(LaunchId launch, DateTimeOffset at) => _failed[launch] = at;

    /// <summary>Says a tool call started (<paramref name="running"/> true) or returned (false) on <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="running">Whether a tool call is in flight.</param>
    public void SetToolRunning(LaunchId launch, bool running) => _ = running ? _tools.Add(launch) : _tools.Remove(launch);

    /// <summary>Says a new turn started on <paramref name="launch"/> (the prompt hook deleted the files).</summary>
    /// <param name="launch">The launch.</param>
    public void StartTurn(LaunchId launch)
    {
        _ = _turns.Remove(launch);
        _ = _failed.Remove(launch);
    }

    /// <inheritdoc/>
    public DateTimeOffset? TurnEndedAt(LaunchId launch) => _turns.TryGetValue(launch, out var at) ? at : null;

    /// <inheritdoc/>
    public DateTimeOffset? TurnFailedAt(LaunchId launch) => _failed.TryGetValue(launch, out var at) ? at : null;

    /// <inheritdoc/>
    public bool ToolRunning(LaunchId launch) => _tools.Contains(launch);

    /// <inheritdoc/>
    public DateTimeOffset? NudgedAt(LaunchId launch) => _nudged.TryGetValue(launch, out var at) ? at : null;

    /// <inheritdoc/>
    public void MarkNudged(LaunchId launch, DateTimeOffset at) => _nudged[launch] = at;

    /// <inheritdoc/>
    public Result<Unit> Verify(LaunchId launch, LaunchSecret secret) =>
        _open.TryGetValue(launch, out var proof) && string.Equals(proof, secret.Value, StringComparison.Ordinal)
            ? Unit.Value
            : SindriErrors.AuthLaunchSecret.With("the launch secret doesn't match an open launch");

    /// <inheritdoc/>
    public void Forget(LaunchId launch)
    {
        _ = _open.Remove(launch);
        _ = _turns.Remove(launch);
        _ = _failed.Remove(launch);
        _ = _nudged.Remove(launch);
        _ = _tools.Remove(launch);
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
