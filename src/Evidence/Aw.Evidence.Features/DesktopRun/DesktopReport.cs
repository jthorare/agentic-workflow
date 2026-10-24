using System.Globalization;
using System.Text;
using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>The run's report.md: the verdict, then one row per screen and size with its diff and where its images are.</summary>
internal static class DesktopReport
{
    internal static string Markdown(DesktopSummary summary)
    {
        var text = new StringBuilder()
            .Append("# Desktop UI evidence: ").Append(summary.RunId.Value).AppendLine().AppendLine()
            .Append("Outcome: **").Append(summary.Outcome.ToString().ToUpperInvariant()).Append("** - ").AppendLine(summary.Headline).AppendLine()
            .AppendLine("| Screen | Size | Diff % | Limit % | Verdict | Side by side |")
            .AppendLine("|---|---|---|---|---|---|");
        foreach (var screen in summary.Screens)
        {
            _ = text.Append("| ").Append(screen.Name).Append(" | ").Append(screen.Size)
                .Append(" | ").Append(screen.DiffPercent.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(" | ").Append(screen.Threshold.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(" | ").Append(screen.Passed ? "pass" : "FAIL" + (screen.Problem is null ? string.Empty : ": " + screen.Problem))
                .Append(" | ").Append(screen.Files.SideBySide).AppendLine(" |");
        }

        return text.ToString();
    }
}
