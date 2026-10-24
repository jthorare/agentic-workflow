using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>What a desktop-run produced: the summary as written and the exact JSON text of the file (what stdout carries).</summary>
/// <param name="Summary">The summary, read back from <paramref name="Document"/>.</param>
/// <param name="Document">The summary JSON, byte for byte what was written to summary.json.</param>
public sealed record DesktopRunResponse(DesktopSummary Summary, string Document);
