using System.Text.Json;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.StartAttempt;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'start-attempt' command: binds its arguments, calls StartAttempt and maps the result to an exit code.</summary>
internal static class StartAttemptCommand
{
    internal const string Name = "start-attempt";

    internal static readonly CommandSpec Spec = new("start-attempt --state <dir> --mode <A|B|C> [--abandon-judge] [--abandon-unrun]", ["state", "mode"], [], ["abandon-judge", "abandon-unrun"]);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        FixMode? parsed = line["mode"] switch { "A" => FixMode.A, "B" => FixMode.B, "C" => FixMode.C, _ => null };
        if (parsed is not { } mode)
        {
            return CommandOutput.Usage($"--mode must be A, B or C (got \"{line["mode"]}\")", Spec.Usage);
        }

        return await CommandOutput.Call<StartAttemptRequest, StartAttemptResponse>(services, json, new StartAttemptRequest(line.StateDirectory, mode, line.Flags.Contains("abandon-judge"), line.Flags.Contains("abandon-unrun"))).ConfigureAwait(false);
    }
}
