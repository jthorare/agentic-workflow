using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// POST /sindri/index/purge: what one record source holds, and with <paramref name="Confirm"/> its deletion (owned-index spec §5.8, §10). A desk command, so a session is refused
/// (SND-AUTH-002): a purge cannot be undone for anything the source has since deleted. Without <paramref name="Confirm"/> nothing is deleted; the answer is the preview.
/// </summary>
/// <param name="Source">The source id, e.g. 'github'.</param>
/// <param name="Confirm">True to delete; false to count only.</param>
public sealed record PurgeIndexSourceRequest(string Source, bool Confirm = false) : ICommand<IndexPurgeView>;
