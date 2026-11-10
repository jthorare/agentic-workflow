namespace Aw.Sindri.Features.PurgeResources;

/// <summary>The owner's resources are gone.</summary>
/// <param name="Owner">The owner.</param>
/// <param name="Removed">How many resources were removed (0 when the owner had none).</param>
internal sealed record PurgeResourcesResponse(string Owner, int Removed);
