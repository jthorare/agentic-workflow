using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>What <see cref="IPageWrites.Preview"/> read: the preview, or the server's refusal.</summary>
/// <param name="Preview">The preview; null when the read failed.</param>
/// <param name="Code">The error id when it failed (SND-CONTEXT-259 for an empty or too long paste), else null.</param>
/// <param name="Message">The server's sentence when it failed, else null.</param>
internal sealed record PagesPreviewRead(PagesPreview? Preview, string? Code = null, string? Message = null);
