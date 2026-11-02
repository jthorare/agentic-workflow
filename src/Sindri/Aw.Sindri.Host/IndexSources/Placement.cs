using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>Where a connector's record belongs, given by the source's own mapping (owned-index spec §4.4).</summary>
/// <param name="Repo">The profile repo, or null when the source has none (Slack).</param>
/// <param name="Audience">Who may see the record: 'repo:&lt;owner&gt;/&lt;name&gt;' for GitHub, 'slack:&lt;channelId&gt;' for Slack.</param>
/// <param name="SourceRepo">The connector's own repo name ('owner/name'), whose 'path:' links are rewritten onto <paramref name="Repo"/>; null for none.</param>
internal sealed record Placement(ContextRepoName? Repo, AudienceKey Audience, string? SourceRepo);
