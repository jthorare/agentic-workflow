using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>The model names Codex lists for this box (~/.codex/models_cache.json).</summary>
public interface ICodexModels
{
    /// <summary>Reads the slugs Codex knows.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The slugs, or null when the file is absent or can't be read (then nothing is checked).</returns>
    public ValueTask<Result<IReadOnlySet<string>?>> Known(CancellationToken cancellationToken);
}
