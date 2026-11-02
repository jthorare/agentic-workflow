using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Features.LaneRecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Host.Commands;

/// <summary>
/// The 'lane-recycle' command: /clear a Claude lane that wrote a digest, then have it continue from the digest. A refusal (AW-META-021) or a
/// lane that never reported a new session (AW-META-022) exits 1 with the reason on stderr; other errors exit 2.
/// </summary>
internal static class LaneRecycleCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lane-recycle",
        new CommandSpec("lane-recycle <lane> --digest <path> [--dry-run] [--json]", ["digest"], [], ["dry-run"], Positionals: 1),
        NeedsConfig: true,
        Run);

    private static async Task<int> Run(CommandContext context)
    {
        var request = new LaneRecycleRequest(context.Line.Positional(0), Absolute(context.Line.Value("digest")), context.Line.Has("dry-run"));
        var result = await context.Services.GetRequiredService<IService<LaneRecycleRequest, LaneRecycleResponse>>().Handle(request, context.Cancellation).ConfigureAwait(false);
        if (result.IsError)
        {
            var code = CommandOutput.Fail(context, result.Error);
            return result.Error.Code == MetaErrors.RecycleRefused.Code || result.Error.Code == MetaErrors.RecycleTimedOut.Code ? CommandOutput.Attention : code;
        }

        return CommandOutput.Report(context, result, Text);
    }

    // The digest is typed into another session's pane, whose directory is not ours: relative paths are made absolute here.
    private static string Absolute(string path) => string.IsNullOrWhiteSpace(path) || path.Contains('\0', StringComparison.Ordinal) ? path : Path.GetFullPath(path);

    private static string Text(LaneRecycleResponse response) =>
        string.Join(
            Environment.NewLine,
            [
                (response.DryRun ? "dry run: would recycle lane " : "recycled lane ") + response.Lane + " (session " + response.OldSession + (response.NewSession is { } fresh ? " -> " + fresh : string.Empty) + ")",
                .. response.Steps.Select(static (s, i) => "  " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ". " + s),
                .. response.Warning is { } warning ? (string[])["warning: " + warning] : [],
            ]);
}
