using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>One design doc by its id (GET /sindri/library/{doc}; PR review page spec §6.1, release 1).</summary>
/// <param name="Doc">The doc's front-matter id. When two scopes share it, the doc at the lowest path answers.</param>
public sealed record GetLibraryDocRequest(string Doc) : IQuery<GetLibraryDocResponse>;
