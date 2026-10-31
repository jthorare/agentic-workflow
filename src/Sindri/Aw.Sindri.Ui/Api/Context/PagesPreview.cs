namespace Aw.Sindri.Ui.Api.Context;

/// <summary>QUERY /sindri/context/pages/preview's answer: one block per page in the paste.</summary>
/// <param name="MapVersion">The map read ('v41'), or empty when none was.</param>
/// <param name="ProfileHash">The hash of the live profile the preview read: send it as ifHash when saving.</param>
/// <param name="Blocks">The blocks in paste order.</param>
internal sealed record PagesPreview(string MapVersion, string ProfileHash, IReadOnlyList<PagesPreviewBlock> Blocks);
