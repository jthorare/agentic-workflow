namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>One repo's index health (CI-10b): the overlay the server works out from the build jobs.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">'missing', 'damaged', 'ok', 'stale', 'building' or 'waiting'; 'stale', 'building' and 'waiting' override the file state.</param>
/// <param name="EmbeddingsPercent">How much of the embeddings layer is done, or null when it has no figure.</param>
/// <param name="Waiting">The server's words while waiting ("retrying in N min"), or null.</param>
internal sealed record CodeIndexHealthView(string Repo, string State, int? EmbeddingsPercent, string? Waiting);
