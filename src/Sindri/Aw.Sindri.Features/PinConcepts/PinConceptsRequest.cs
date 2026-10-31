using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>
/// Pin, edit or unpin concepts in the profile (concept pins spec §3.6, POST /sindri/context/pins): a job that only writes 'index.concepts' and returns the new profile hash. It never approves; the app or the owner
/// approves the hash with ApproveProfile. Every caller kind may run it.
/// </summary>
/// <param name="Set">The pins to create or update, in order.</param>
/// <param name="Remove">The ids of the pins to unpin.</param>
/// <param name="IfHash">The live profile hash the caller read; when given and the file moved, the write is refused with SND-PROFILE-003. Null reads the live file and writes once.</param>
/// <param name="WithFolderRules">Lets a removal drop a pin that has 'paths', 'dependsOn' or 'owns' (else SND-CONTEXT-175).</param>
public sealed record PinConceptsRequest(IReadOnlyList<PinSet>? Set = null, IReadOnlyList<ConceptPinId>? Remove = null, ProfileHash? IfHash = null, bool WithFolderRules = false) : IJob<PinConceptsResult>;
