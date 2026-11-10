namespace Aw.Sindri.Ui.Api.Resources;

/// <summary>POST /sindri/resources/purge.</summary>
/// <param name="Owner">The owner purged.</param>
/// <param name="Removed">How many resources were removed.</param>
internal sealed record PurgeResourcesResult(string Owner, int Removed);
