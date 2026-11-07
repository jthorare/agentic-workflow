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
    /// <param name="githubRepos">'context.sources.github.repos' ('owner/name'): an issue's pull request attachment links to its PR record only for these.</param>
    /// <returns>The feed.</returns>
    public RecordFeed CreateFeed(LinearSettings settings, IReadOnlyList<string>? githubRepos = null) => new LinearFeed(new LinearClient(settings, parts), parts.CallDeadline, githubRepos ?? []);

    /// <summary>A linear project record feed (projects page spec §4.1).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The feed.</returns>
    public RecordFeed CreateProjectFeed(LinearSettings settings) => new LinearProjectFeed(new LinearClient(settings, parts), parts.CallDeadline);

    /// <summary>The viewer asking Linear who the key belongs to (projects page spec §4.2).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The viewer.</returns>
    public ISourceViewer CreateViewer(LinearSettings settings) => new LinearViewer(new LinearClient(settings, parts), parts.CallDeadline);

    /// <summary>The reader of the pickers' data: the profile's teams with their states, cycles, members and labels (linear hub spec 2026-11-04 §4.2).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The reader.</returns>
    public LinearLookupsReader CreateLookupsReader(LinearSettings settings) => new(new LinearClient(settings, parts), parts.CallDeadline, parts.Clock);

    /// <summary>The reader of one issue, as the feed reads it (linear hub spec 2026-11-04 D4).</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <param name="githubRepos">'context.sources.github.repos' ('owner/name').</param>
    /// <returns>The reader.</returns>
    public LinearIssueReader CreateIssueReader(LinearSettings settings, IReadOnlyList<string> githubRepos) => new(new LinearClient(settings, parts), parts.CallDeadline, githubRepos);

    /// <summary>How long every call to Linear gets to answer.</summary>
    /// <returns>The deadline.</returns>
    public TimeSpan Deadline() => parts.CallDeadline;

    /// <summary>A client for the profile's Linear (one key resolution per client), for a reader that sends its own operations.</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The client.</returns>
    public LinearClient CreateClient(LinearSettings settings) => new(settings, parts);
}
