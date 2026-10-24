namespace Aw.Evidence.Features.DesktopRun;

/// <summary>What driving the app produced besides the frames.</summary>
/// <param name="Notes">How the app got its display, one line per way ('display: ...').</param>
/// <param name="Problems">Why a screen's script did not reach its screenshot, by <see cref="DesktopPlan.KeyOf"/>.</param>
internal sealed record DesktopCaptures(IReadOnlyList<string> Notes, IReadOnlyDictionary<string, string> Problems);
