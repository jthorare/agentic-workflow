namespace Aw.Sindri.Ui.Api.Context;

/// <summary>POST /sindri/context/pages/preview's answer: one block per page in the paste.</summary>
/// <param name="MapVersion">The map read ('v41'), or empty when none was.</param>
/// <param name="Blocks">The blocks in paste order.</param>
internal sealed record PagesPreview(string MapVersion, IReadOnlyList<PagesPreviewBlock> Blocks);
