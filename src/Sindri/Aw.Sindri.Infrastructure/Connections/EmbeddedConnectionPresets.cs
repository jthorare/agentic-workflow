using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IConnectionPresetCatalog over the presets embedded from config/connections/presets/*.json (resource names 'connection-presets/&lt;id&gt;.json').</summary>
internal sealed class EmbeddedConnectionPresets : IConnectionPresetCatalog
{
    private const string Prefix = "connection-presets/";

    public Result<IReadOnlyList<ConnectionPreset>> List()
    {
        var assembly = typeof(EmbeddedConnectionPresets).Assembly;
        var presets = new List<ConnectionPreset>();
        foreach (var name in assembly.GetManifestResourceNames().Where(static n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                return SindriErrors.InvalidInput.With("connection preset " + name + " is not embedded");
            }

            using var reader = new StreamReader(stream);
            var parsed = ConnectionPresets.Parse(reader.ReadToEnd());
            if (parsed.IsError)
            {
                return SindriErrors.InvalidInput.With(name + ": " + parsed.Error.Message);
            }

            presets.Add(parsed.Value);
        }

        presets.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return presets;
    }

    public ConnectionPreset? Find(string key)
    {
        var all = List();
        return all.IsSuccess ? all.Value.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal)) : null;
    }
}
