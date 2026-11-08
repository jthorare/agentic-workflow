using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IConnectionPresetCatalog over the presets embedded from config/connections/presets/*.json (resource names 'connection-presets/&lt;id&gt;.json').</summary>
internal sealed class EmbeddedConnectionPresets : IConnectionPresetCatalog
{
    public Result<IReadOnlyList<ConnectionPreset>> List() => throw new NotSupportedException();

    public ConnectionPreset? Find(string id) => throw new NotSupportedException();
}
