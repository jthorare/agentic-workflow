namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// POST /sindri/context/pins' body (concept pins spec §3.6, PinConcepts, job): the pins to create, update or unpin in one profile write. A null field of a <see cref="PinSetBody"/> keeps the stored value; a list replaces it
/// and an empty list clears it.
/// </summary>
/// <param name="Set">The pins to create or update, in order.</param>
/// <param name="Remove">The ids of the pins to unpin.</param>
/// <param name="IfHash">The live profile hash the sheet read; the write is refused with SND-PROFILE-003 when the file moved. Null writes against the live file.</param>
/// <param name="WithFolderRules">Lets an unpin drop a pin that has folder rules set outside the app.</param>
internal sealed record PinConceptsBody(IReadOnlyList<PinSetBody>? Set = null, IReadOnlyList<string>? Remove = null, string? IfHash = null, bool WithFolderRules = false);
