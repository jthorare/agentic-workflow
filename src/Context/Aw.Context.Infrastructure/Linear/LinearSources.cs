using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Builds a linear source for the profile's linear settings (registered by AddContextInfrastructure). A source keeps only its resolved key.
/// </summary>
/// <param name="parts">What every linear source shares.</param>
internal sealed class LinearSources(LinearSourceParts parts)
{
    /// <summary>A linear source.</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The source.</returns>
    public IContextSource Create(LinearSettings settings) => new LinearSource(settings, parts);

    /// <summary>A linear record feed (spec 2026-10-27 sindri-owned index §5.2).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The feed.</returns>
    public RecordFeed CreateFeed(LinearSettings settings) => new LinearFeed(new LinearClient(settings, parts), parts.CallDeadline);

    /// <summary>A linear project record feed (projects page spec §4.1).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The feed.</returns>
    public RecordFeed CreateProjectFeed(LinearSettings settings) => new LinearProjectFeed(new LinearClient(settings, parts), parts.CallDeadline);

    /// <summary>The viewer asking Linear who the key belongs to (projects page spec §4.2).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The viewer.</returns>
    public ISourceViewer CreateViewer(LinearSettings settings) => new LinearViewer(new LinearClient(settings, parts), parts.CallDeadline);
}
