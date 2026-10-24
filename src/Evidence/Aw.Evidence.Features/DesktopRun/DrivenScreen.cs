namespace Aw.Evidence.Features.DesktopRun;

/// <summary>How driving one screen at one size ended.</summary>
/// <param name="Display">How the app got its display.</param>
/// <param name="Problem">Why the script did not reach its screenshot (an element not found, a wait that timed out); null when it did.</param>
internal sealed record DrivenScreen(string Display, string? Problem);
