namespace Aw.Sindri.Features.SetPages;

/// <summary>The live profile's pins: their ids, and the id each name (ignoring case) stands for.</summary>
/// <param name="Ids">Every pin id.</param>
/// <param name="ByName">A pin's id by its name.</param>
internal sealed record PinNames(HashSet<string> Ids, Dictionary<string, string> ByName);
