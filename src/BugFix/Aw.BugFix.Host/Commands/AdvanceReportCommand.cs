using System.Text.Json;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Features.AdvanceReport;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'advance report' command: binds its arguments, calls AdvanceReport and maps the result to an exit code.</summary>
internal static class AdvanceReportCommand
{
    internal const string Name = "advance report";

    internal static readonly CommandSpec Spec = new("advance report --state <dir> (--candidate <id> --sweep <sweep.md> | --unresolved)", ["state"], ["candidate", "sweep"], ["unresolved"]);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        CandidateId? candidate = null;
        if (line["candidate"] is { } text)
        {
            if (!CandidateId.TryFrom(text, out var parsed))
            {
                return CommandOutput.Usage($"--candidate must be a candidate id like c1 (got \"{text}\")", Spec.Usage);
            }

            candidate = parsed;
        }

        return await CommandOutput.Call<AdvanceReportRequest, AdvanceReportResponse>(services, json, new AdvanceReportRequest(line.StateDirectory, candidate, line.Flags.Contains("unresolved"), line["sweep"] is { } sweep ? Path.GetFullPath(sweep) : null)).ConfigureAwait(false);
    }
}
