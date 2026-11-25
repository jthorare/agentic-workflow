using System.Globalization;
using Aw.Meta.Features.ReportDefect;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'report-defect' command: exit 1 when the daily cap kept it from filing.</summary>
internal static class ReportDefectCommand
{
    internal static CommandEntry Entry { get; } = new(
        "report-defect",
        new CommandSpec("report-defect --code <code> --title <title> --what <what happened> --error <exact error line> [--path <code or doc path>] [--json]", ["code", "title", "what", "error"], ["path"], []),
        NeedsConfig: true,
        static context => CommandOutput.Call<ReportDefectRequest, ReportDefectResponse>(
            context,
            new ReportDefectRequest(context.Line.Value("code"), context.Line.Value("title"), context.Line.Value("what"), context.Line.Value("error"), context.Line["path"]),
            Text,
            static r => r.Outcome == "cap-hit"));

    private static string Text(ReportDefectResponse response) => response.Outcome switch
    {
        "filed" => "filed " + response.Url,
        "commented" => "commented on " + response.Url,
        _ => string.Create(CultureInfo.InvariantCulture, $"issue cap hit ({response.Skipped} skipped today): nothing filed"),
    };
}
