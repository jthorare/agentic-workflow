using System.Globalization;
using Aw.Meta.Features.MergeRun;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'merge-run' command: exit 0 when merged or queued in GitHub's merge queue, 1 when a check refused it.</summary>
internal static class MergeRunCommand
{
    internal static CommandEntry Entry { get; } = new(
        "merge-run",
        new CommandSpec("merge-run <pr> [--json]", [], [], [], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        PrArgument.TryParse(context.Line.Positional(0), out var pr)
            ? CommandOutput.Call<MergeRunRequest, MergeRunResponse>(context, new MergeRunRequest(pr), Text, static r => !r.Merged && !r.Queued)
            : Task.FromResult(PrArgument.Usage(context, Entry));

    private static string Text(MergeRunResponse response) =>
        response.Queued
            ? string.Create(CultureInfo.InvariantCulture, $"QUEUED #{response.Pr} in GitHub's merge queue{(response.Position is { } place ? $" (position {place})" : string.Empty)}; the watch records the merge when it lands")
            : response.Merged
            ? string.Create(CultureInfo.InvariantCulture, $"MERGED #{response.Pr}: main is now {response.Sha}{Environment.NewLine}{response.Announce}") + (response.Worktrees is { } worktrees ? Environment.NewLine + worktrees : string.Empty)
            : string.Join(Environment.NewLine, [.. MergeCheckCommand.Lines(response.Checks), string.Create(CultureInfo.InvariantCulture, $"NOT MERGED #{response.Pr}")]);
}
