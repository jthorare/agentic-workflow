using System.Globalization;
using Aw.Meta.Features.WorktreeSweep;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'worktree-sweep' command: removes the removable worktrees. Exit 0, or 1 when git refused a removal.</summary>
internal static class WorktreeSweepCommand
{
    internal static CommandEntry Entry { get; } = new(
        "worktree-sweep",
        new CommandSpec("worktree-sweep [--dry-run] [--include-no-pr] [--pr <n>] [--json]", [], ["pr"], ["dry-run", "include-no-pr"]),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        int? pr = null;
        if (context.Line["pr"] is { } text)
        {
            if (!PrArgument.TryParse(text, out var number))
            {
                return Task.FromResult(PrArgument.Usage(context, Entry));
            }

            pr = number;
        }

        return CommandOutput.Call<SweepWorktreesRequest, SweepWorktreesResponse>(context, new SweepWorktreesRequest(context.Line.Has("dry-run"), pr, context.Line.Has("include-no-pr")), Text, static r => r.Failed > 0);
    }

    private static string Text(SweepWorktreesResponse response)
    {
        var width = response.Removed.Concat(response.Kept).Select(static w => w.Path.Length).DefaultIfEmpty().Max();
        var done = response.DryRun ? "WOULD REMOVE" : "REMOVED";
        var counts = string.Create(CultureInfo.InvariantCulture, $"{(response.DryRun ? "would remove" : "removed")} {response.Removed.Count}, kept {response.Kept.Count}{(response.Failed > 0 ? $", git refused {response.Failed}" : string.Empty)}");
        return string.Join(
            Environment.NewLine,
            [
                .. response.Removed.SelectMany(w => WorktreeText.Entry(w, width, done)),
                .. response.Kept.SelectMany(w => WorktreeText.Entry(w, width, "KEPT")),
                "swept: " + counts,
            ]);
    }
}
