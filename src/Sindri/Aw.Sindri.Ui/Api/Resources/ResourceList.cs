namespace Aw.Sindri.Ui.Api.Resources;

/// <summary>GET /sindri/resources.</summary>
/// <param name="Resources">The resources.</param>
internal sealed record ResourceList(IReadOnlyList<ResourceView> Resources);
