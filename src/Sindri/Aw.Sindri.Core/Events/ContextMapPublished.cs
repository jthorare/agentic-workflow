namespace Aw.Sindri.Core.Events;

/// <summary>A map went ahead ('context.map.published', code index spec 6.4); one per map version, after its 'context.map.promoted'. Key 'context.map.published:&lt;repo&gt;:v&lt;version&gt;'.</summary>
/// <param name="Repo">The version's own repo.</param>
/// <param name="Version">The version now current.</param>
/// <param name="Reason">IndexReady, NoIndex, IndexBroken, FirstMap, WaitOff or TimedOut.</param>
/// <param name="Via">'refresh' when the refresh job promoted at once, 'check' when the publish job did after a wait.</param>
/// <param name="Repos">How many repos the map covers.</param>
/// <param name="StaleRepos">How many were not built at that moment.</param>
/// <param name="WaitedSeconds">Seconds from the start of the wait to now (0 when there was none).</param>
/// <param name="Heads">Each mapped repo's head, by repo name; the public form leaves it out.</param>
public sealed record ContextMapPublished(string Repo, int Version, string Reason, string Via, int Repos, int StaleRepos, int WaitedSeconds, IReadOnlyDictionary<string, string> Heads)
{
    /// <summary>True when some repo was not built at that moment: the one field the public form keeps of the two counts (it reads the stored payload).</summary>
    public bool AnyStale => StaleRepos > 0;
}
