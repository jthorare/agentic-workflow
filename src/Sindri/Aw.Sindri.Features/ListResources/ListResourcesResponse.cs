namespace Aw.Sindri.Features.ListResources;

/// <summary>The resources, by owner, kind and name.</summary>
/// <param name="Resources">One row per resource; none when nothing was provisioned.</param>
internal sealed record ListResourcesResponse(IReadOnlyList<ResourceView> Resources);
