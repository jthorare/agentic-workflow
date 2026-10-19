using System.Collections.Concurrent;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>In-memory, per-launch values for <see cref="ILaunchSecrets"/>: the launcher puts them in before it wraps and forgets them when the launch ends. Nothing is written to disk by this class.</summary>
internal sealed class LaunchSecretRegistry : ILaunchSecrets
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _byLaunch = new(StringComparer.Ordinal);

    /// <summary>Records the launch's values.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="values">The values by variable name.</param>
    internal void Put(LaunchId launch, IReadOnlyDictionary<string, string> values) => _byLaunch[launch.Value] = values;

    /// <summary>Forgets the launch's values.</summary>
    /// <param name="launch">The launch.</param>
    internal void Forget(LaunchId launch) => _ = _byLaunch.TryRemove(launch.Value, out _);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> For(LaunchId launch) =>
        _byLaunch.TryGetValue(launch.Value, out var values) ? values : new Dictionary<string, string>(StringComparer.Ordinal);
}
