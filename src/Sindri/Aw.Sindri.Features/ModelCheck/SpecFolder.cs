namespace Aw.Sindri.Features.ModelCheck;

/// <summary>A spec folder of the workspace (model-check design §3): <c language="text">specs/tla/&lt;Name&gt;/</c>.</summary>
/// <param name="Path">The folder, relative to the workspace, without a trailing slash.</param>
/// <param name="Files">Every entry in it, 'gaps/' included.</param>
internal sealed record SpecFolder(string Path, IReadOnlyList<SpecFile> Files);
