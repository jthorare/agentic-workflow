namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>A service the Connections page offers a tile for: one with a real sign-in path.</summary>
/// <param name="Id">The preset id ('github'); the connection is named '&lt;id&gt;:work'.</param>
/// <param name="Title">The service in words.</param>
/// <param name="Blurb">What the tile says before it is connected.</param>
/// <param name="Mode">What the browser sign-in asks for (Token: there is none).</param>
/// <param name="TakesKey">True when the service takes a pasted token, which the tile offers first (#4357).</param>
internal sealed record ServiceTile(string Id, string Title, string Blurb, ServiceTileMode Mode, bool TakesKey = false);
