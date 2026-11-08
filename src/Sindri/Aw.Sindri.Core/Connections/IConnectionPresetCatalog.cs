using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>The shipped presets (github, linear, slack, google, spotify).</summary>
public interface IConnectionPresetCatalog
{
    /// <summary>Every shipped preset, sorted by id.</summary>
    /// <returns>The presets, or the error of the first one that does not parse.</returns>
    public Result<IReadOnlyList<ConnectionPreset>> List();

    /// <summary>One preset.</summary>
    /// <param name="key">The preset key.</param>
    /// <returns>The preset, or null when none is shipped under that key.</returns>
    public ConnectionPreset? Find(string key);
}
