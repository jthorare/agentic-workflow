namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Where a node came from: the ticket, decision, doc, file or commit.</summary>
/// <param name="Kind">What sort of thing it is (ticket, decision, doc, file, commit), as served.</param>
/// <param name="Ref">Its reference (an id, a path, a sha), as served.</param>
/// <param name="Title">Its title, when served.</param>
/// <param name="Url">A link to it, when served.</param>
/// <param name="Path">The file on this machine, when served (a file source and a human caller).</param>
internal sealed record NodeSource(string Kind, string Ref, string? Title = null, string? Url = null, string? Path = null);
