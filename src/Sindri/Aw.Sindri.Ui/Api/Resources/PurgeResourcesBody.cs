namespace Aw.Sindri.Ui.Api.Resources;

/// <summary>The body of POST /sindri/resources/purge.</summary>
/// <param name="Owner">The owner whose resources go.</param>
internal sealed record PurgeResourcesBody(string Owner);
