namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>An edge of the map as the path and the highlight see it: plain keys, no geometry.</summary>
/// <param name="Key">The edge's key.</param>
/// <param name="From">The node key at one end.</param>
/// <param name="To">The node key at the other end.</param>
/// <param name="Type">The served edge type.</param>
internal readonly record struct MapLink(string Key, string From, string To, string Type);
