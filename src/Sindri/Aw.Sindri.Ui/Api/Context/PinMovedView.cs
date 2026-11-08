namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What the last refresh and relink moved in one pin.</summary>
/// <param name="FromVersion">The previous map version.</param>
/// <param name="ToVersion">The map version the refresh built.</param>
/// <param name="Gained">Features the pin gained.</param>
/// <param name="Lost">Features the pin lost.</param>
/// <param name="To">Where the lost features went.</param>
/// <param name="RecordsLinked">Net change in records linked to the pin (negative when fewer); null while the relink runs.</param>
/// <param name="RelinkFailed">True when the relink failed (the wire omits it otherwise).</param>
internal sealed record PinMovedView(string FromVersion, string ToVersion, int Gained, int Lost, IReadOnlyList<PinMovedTo> To, int? RecordsLinked, bool RelinkFailed = false);
