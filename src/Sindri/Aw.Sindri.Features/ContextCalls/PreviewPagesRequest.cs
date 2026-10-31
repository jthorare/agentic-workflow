using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// POST /sindri/context/pages/preview: reads a pasted list of pages (one block per page: its name, then one action per line, 'Name' or 'Name -> concept, concept') against the map's pages and the concepts,
/// and says per block which page it names, whether the save would add it and the page it would save. Read-only: nothing is written or queued; saving is the existing POST /sindri/context/pages.
/// </summary>
/// <param name="Text">The paste (1 to 65,536 UTF-8 bytes).</param>
/// <param name="Repo">Reads the pages of this repo only, and fills it into the pages to save; null for every repo the caller may read.</param>
public sealed record PreviewPagesRequest(string Text, string? Repo = null) : ICommand<PagesPreview>;
