namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'context.map.published': no repo names, heads or repo counts; the two counts become <paramref name="AnyStale"/>.</summary>
/// <param name="Version">The version now current.</param>
/// <param name="Reason">IndexReady, NoIndex, IndexBroken, FirstMap, WaitOff or TimedOut.</param>
/// <param name="Via">'refresh' or 'check'.</param>
/// <param name="WaitedSeconds">Seconds from the start of the wait to now.</param>
/// <param name="AnyStale">True when some repo's index was not at the map's commit at that moment.</param>
public sealed record ContextMapPublishedPublic(int Version, string Reason, string Via, int WaitedSeconds, bool AnyStale);
