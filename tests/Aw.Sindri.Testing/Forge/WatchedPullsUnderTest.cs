using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>A watched-pulls port ready for a contract run.</summary>
/// <param name="Pulls">The port.</param>
/// <param name="Watch">Makes an instance of an item wait on the pull request at a link: (link, item).</param>
/// <param name="Unwatch">Ends the wait on a link.</param>
public sealed record WatchedPullsUnderTest(IWatchedPulls Pulls, Action<string, string> Watch, Action<string> Unwatch);
