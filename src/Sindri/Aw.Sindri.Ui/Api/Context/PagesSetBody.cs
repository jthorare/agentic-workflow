namespace Aw.Sindri.Ui.Api.Context;

/// <summary>POST /sindri/context/pages' body (pages spec §3.1, SetPages, job): the pages to set (each replaces the page with its id) and the page ids to remove, in one profile write.</summary>
/// <param name="Set">The pages to set, in order.</param>
/// <param name="Remove">The ids of the pages to remove.</param>
/// <param name="IfHash">The profile file's hash when the sheet opened (#1826); the write is refused with SND-PROFILE-003 when the file moved. Null writes against the live file.</param>
/// <param name="DryRun">Plans and checks the write, writing nothing.</param>
internal sealed record PagesSetBody(IReadOnlyList<PageSetBody>? Set = null, IReadOnlyList<string>? Remove = null, string? IfHash = null, bool DryRun = false);
