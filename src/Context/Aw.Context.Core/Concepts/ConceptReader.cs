using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The map a concepts read is built from: the map the caller may read (a session's graph is cut to its repo, concepts included), the repo the answer is for and the caller's text policy
/// (every label cleaned like every answer's). The concepts and members reads share it.
/// </summary>
/// <param name="reader">Reads the map the caller may see.</param>
/// <param name="scrubbers">Builds the scrubbers of the profile's patterns.</param>
public sealed class ConceptReader(MapReader reader, IScrubberFactory scrubbers)
{
    /// <summary>The graph, basis and text of one read.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="read">Which map.</param>
    /// <param name="repo">A human caller's repo when the profile has several, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What the builder needs, or SND-CONTEXT-001/003/004/020/030/044/045/120.</returns>
    public async ValueTask<Result<ConceptRead>> Read(ContextScope scope, MapRead read, string? repo, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var addressed = RepoAddressing.Addressed(settings.Value, scope, repo);
        if (addressed.IsError)
        {
            return addressed.Error;
        }

        var visible = await reader.ReadViewAt(scope, read, cancellationToken).ConfigureAwait(false);
        if (visible.IsError)
        {
            return visible.Error;
        }

        var graph = addressed.Value.Cut is { } cut ? visible.Value.Graph.ScopedTo(cut) : visible.Value.Graph;
        return new ConceptRead(new ConceptsBasis(visible.Value.Snapshot.Tag, addressed.Value.Name), graph, new MapText(built.Value, FencePolicies.For(scope.Caller)));
    }
}
