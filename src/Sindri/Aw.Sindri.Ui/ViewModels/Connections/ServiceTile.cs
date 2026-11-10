namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>A service the Connections page offers a tile for: one with a real sign-in path.</summary>
/// <param name="Id">The preset id ('github'); the connection is named '&lt;id&gt;:work'.</param>
/// <param name="Title">The service in words.</param>
/// <param name="Blurb">What the tile says before it is connected.</param>
/// <param name="Mode">What the tile asks for.</param>
internal sealed record ServiceTile(string Id, string Title, string Blurb, TileMode Mode);
