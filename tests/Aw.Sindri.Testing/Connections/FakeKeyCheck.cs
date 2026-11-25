using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IKeyCheck that records the presets it was asked about and takes, refuses or cannot reach (#4357). It never calls a network.</summary>
public sealed class FakeKeyCheck : IKeyCheck
{
    /// <summary>Gets the checks made: the preset and the key that was sent (tests may compare it; nothing prints it).</summary>
    public IList<(string Preset, string Key)> Calls { get; } = [];

    /// <summary>Gets or sets a value indicating whether the service refuses the key (SND-CONN-018).</summary>
    public bool Refuses { get; set; }

    /// <summary>Gets or sets a value indicating whether the service cannot be reached (SND-CONN-019).</summary>
    public bool Unreachable { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Check(ConnectionPreset preset, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Calls.Add((preset.Key, key));
        return ValueTask.FromResult<Result<Unit>>(Refuses ? SindriErrors.ConnectionKeyRefused.With(preset.DisplayName + " refused the key")
            : Unreachable ? SindriErrors.ConnectionKeyUnchecked.With("could not reach " + preset.DisplayName)
            : Unit.Value);
    }
}
