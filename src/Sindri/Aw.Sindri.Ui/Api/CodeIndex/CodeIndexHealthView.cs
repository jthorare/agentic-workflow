namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>One repo's index health (CI-10b): the overlay the server works out from the build jobs.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">'missing', 'damaged', 'ok', 'stale', 'building' or 'waiting'; 'stale', 'building' and 'waiting' override the file state.</param>
/// <param name="EmbeddingsPercent">How much of the embeddings layer is done, or null when it has no figure.</param>
/// <param name="Waiting">The server's words while waiting ("waiting for a free build slot", "retrying in N min"), or null.</param>
/// <param name="NeedsLook">The server's verdict from doctor's rule (#3430): true when the repo needs a look. Null from a server that predates it; the card then judges the state words itself.</param>
internal sealed record CodeIndexHealthView(string Repo, string State, int? EmbeddingsPercent, string? Waiting, bool? NeedsLook = null);
