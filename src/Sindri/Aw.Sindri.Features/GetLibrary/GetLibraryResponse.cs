using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetLibrary;

/// <summary>The design library's state and its errors (PR review page spec §6.1, §6.3).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Docs">The design docs stored.</param>
/// <param name="WithErrors">The docs stored with an error, however many are listed.</param>
/// <param name="Broken">The first <see cref="LibraryPage.Cap"/> docs with errors.</param>
/// <param name="NearMisses">The first <see cref="LibraryPage.Cap"/> files that almost read as docs.</param>
/// <param name="UnreadableRoots">The folders that couldn't be read.</param>
/// <param name="CappedText">Sindri's words when a list was cut; null otherwise.</param>
/// <param name="Roots">Every root with its state and counts (Settings → Library); empty for a session.</param>
public sealed record GetLibraryResponse(AsOf AsOf, int Docs, int WithErrors, IReadOnlyList<LibraryBrokenDoc> Broken, IReadOnlyList<LibraryNearMiss> NearMisses, IReadOnlyList<ReviewUnreadableRoot> UnreadableRoots, string? CappedText, IReadOnlyList<LibraryRootRow> Roots);
