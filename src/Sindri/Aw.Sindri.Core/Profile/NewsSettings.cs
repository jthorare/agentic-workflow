using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.news:' block: the hosts the 'news' block may read.</summary>
/// <param name="Hosts">Plain host names. Left out: only hacker-news.firebaseio.com, so an RSS feed needs its host listed here.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NewsSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Hosts = null);
