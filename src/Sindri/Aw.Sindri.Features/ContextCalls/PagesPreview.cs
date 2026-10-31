namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The answer to a pasted page list: one block per page in the paste.</summary>
/// <param name="MapVersion">The map read ('v41'), or empty when none was.</param>
/// <param name="Blocks">The blocks in paste order.</param>
public sealed record PagesPreview(string MapVersion, IReadOnlyList<PagesPreviewBlock> Blocks);
