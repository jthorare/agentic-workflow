using System.Text;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>The text and exit code of 'sindri skill run --follow' (skill flows spec §7): exit 0 on the success outcome, 3 on any other mapped outcome, 4 on step.failed.</summary>
internal static class SkillRunText
{
    /// <summary>The text: the manifest hash at the top, the outcome, each fact, the result path, and the SND line at the end.</summary>
    /// <param name="report">How the run ended.</param>
    /// <returns>The text.</returns>
    internal static string Render(SkillRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder();
        _ = text.Append(report.Skill).Append("  manifest sha256:").AppendLine(report.ManifestHash)
            .AppendLine(report.Outcome is { } outcome ? "outcome: " + outcome : "step failed");
        foreach (var fact in report.Facts)
        {
            _ = text.Append(fact.Key).Append(" = ").AppendLine(fact.Value);
        }

        if (report.ResultPath.Length > 0)
        {
            _ = text.Append("result: ").AppendLine(report.ResultPath);
        }

        if (report.ErrorLine is { } error)
        {
            _ = text.AppendLine(error);
        }

        return text.ToString();
    }

    /// <summary>The exit code.</summary>
    /// <param name="report">How the run ended.</param>
    /// <returns>0, 3 or 4.</returns>
    internal static int ExitCode(SkillRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.ErrorLine is not null || report.Outcome is null ? 4 : report.Succeeded ? 0 : 3;
    }

    /// <summary>The line '--follow' prints when the run parks: why, and the command that answers it; null when the run is not parked.</summary>
    /// <param name="owned">The instance's engine-owned badge.</param>
    /// <param name="status">The instance's status fields.</param>
    /// <returns>The line, or null.</returns>
    internal static string? Parked(EngineOwnedBadge? owned, StatusFields status) => throw new NotSupportedException();
}
