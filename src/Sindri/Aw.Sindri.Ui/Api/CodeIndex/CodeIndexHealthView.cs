namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>One repo's index health (CI-10b): the overlay the server works out from the build jobs.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">'missing', 'damaged', 'ok', 'stale', 'building', 'queued' or 'waiting'; 'stale', 'building', 'queued' and 'waiting' override the file state.</param>
/// <param name="EmbeddingsPercent">How much of the embeddings layer is done, or null when it has no figure.</param>
/// <param name="Waiting">The server's words while waiting ("retrying in N min"), or null.</param>
/// <param name="NeedsLook">True when the server has a problem row for the repo (doctor's rule, #3430: a first build queued over 30 min counts); false from a server that predates it.</param>
/// <param name="Paused">True when code-index-main-move is paused and no build is coming for the repo (#3561); false from a server that predates it.</param>
/// <param name="LookWords">The server's own sentence for why the repo needs a look, repo name first (#3515); null when it does not, or from a server that predates it.</param>
internal sealed record CodeIndexHealthView(string Repo, string State, int? EmbeddingsPercent, string? Waiting, bool NeedsLook = false, bool Paused = false, string? LookWords = null);
