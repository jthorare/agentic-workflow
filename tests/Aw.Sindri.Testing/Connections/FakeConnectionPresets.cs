using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IConnectionPresetCatalog over a fixed list.</summary>
/// <param name="presets">The presets.</param>
public sealed class FakeConnectionPresets(params ConnectionPreset[] presets) : IConnectionPresetCatalog
{
    private readonly ConnectionPreset[] _presets = presets;

    /// <inheritdoc />
    public Result<IReadOnlyList<ConnectionPreset>> List() => _presets.OrderBy(static p => p.Key, StringComparer.Ordinal).ToList();

    /// <inheritdoc />
    public ConnectionPreset? Find(string key) => _presets.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
}
