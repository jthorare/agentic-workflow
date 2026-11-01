using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.SetPages;

/// <summary>
/// Set or remove pages in the profile (pages spec §3.1, POST /sindri/context/pages): a job that only writes 'index.pages' and returns the new profile hash. A set replaces the whole page with that id. It never approves;
/// the app or the owner approves the hash with ApproveProfile. Every caller kind may run it.
/// </summary>
/// <param name="Set">The pages to create or replace, in order.</param>
/// <param name="Remove">The ids of the pages to remove.</param>
/// <param name="IfHash">The live profile hash the caller read; when given and the file moved, the write is refused with SND-PROFILE-003. Null reads the live file and writes once.</param>
/// <param name="DryRun">Plans and checks the write and returns it, writing nothing.</param>
public sealed record SetPagesRequest(IReadOnlyList<PageSettings>? Set = null, IReadOnlyList<PagePinId>? Remove = null, ProfileHash? IfHash = null, bool DryRun = false) : IJob<SetPagesResult>;
