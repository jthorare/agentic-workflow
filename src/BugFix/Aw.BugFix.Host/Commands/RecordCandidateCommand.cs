using System.Globalization;
using System.Text.Json;
using Aw.BugFix.Features.RecordCandidate;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'record-candidate' command: binds its arguments, calls RecordCandidate and maps the result to an exit code.</summary>
internal static class RecordCandidateCommand
{
    internal const string Name = "record-candidate";

    internal static readonly CommandSpec Spec = new("record-candidate --state <dir> --branch <branch> [--cwd <dir>] [--hypothesis <n>] [--allow-test-changes [--reason <why>]]", ["state", "branch"], ["cwd", "hypothesis", "reason"], ["allow-test-changes"]);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        int? hypothesis = null;
        if (line["hypothesis"] is { } text)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            {
                return CommandOutput.Usage($"--hypothesis must be a hypothesis number (got \"{text}\")", Spec.Usage);
            }

            hypothesis = number;
        }

        return await CommandOutput.Call<RecordCandidateRequest, RecordCandidateResponse>(services, json, new RecordCandidateRequest(line.StateDirectory, line.Values["branch"], line.WorkingDirectory, hypothesis, line.Flags.Contains("allow-test-changes"), line["reason"])).ConfigureAwait(false);
    }
}
