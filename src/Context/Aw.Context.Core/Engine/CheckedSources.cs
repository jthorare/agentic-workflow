using Aw.Context.Core.Linear;

namespace Aw.Context.Core.Engine;

/// <summary>The checked entries of 'context.sources' (see <see cref="SourcesReader.Check"/>).</summary>
internal sealed record CheckedSources(LinearSettings? Linear, IReadOnlyList<string> Teams, GitHubSource? GitHub, SlackSource? Slack, LibrarySource? Library);
