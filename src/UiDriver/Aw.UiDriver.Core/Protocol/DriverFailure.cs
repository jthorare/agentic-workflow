namespace Aw.UiDriver.Core.Protocol;

/// <summary>Why a call failed; the body of every non-200 answer.</summary>
/// <param name="Code">The AW-UIDRIVER code.</param>
/// <param name="Message">What went wrong, one line.</param>
internal sealed record DriverFailure(string Code, string Message);
