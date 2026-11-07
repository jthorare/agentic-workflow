using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetLibrary;

/// <summary>The library page's read (GET /sindri/library; PR review page spec §6.1).</summary>
public sealed record GetLibraryRequest() : IQuery<GetLibraryResponse>;
