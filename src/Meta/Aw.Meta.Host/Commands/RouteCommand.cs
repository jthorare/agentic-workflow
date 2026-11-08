using Aw.Meta.Features.Routing;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'route' command: which lanes should move to another provider. Exit 1 while a suggestion waits.</summary>
internal static class RouteCommand
{
    internal static CommandEntry Entry { get; } = new(
        "route",
        new CommandSpec("route [--lane <l>] [--apply [--force]] [--json]", [], ["lane"], ["apply", "force"]),
        NeedsConfig: true,
        static context => CommandOutput.Call<RouteRequest, RouteResponse>(context, new RouteRequest(context.Line.Has("apply"), context.Line["lane"], context.Line.Has("force")), Text, static r => r.Attention));

    private static string Text(RouteResponse response)
    {
        var lines = (response.Warnings ?? []).Concat(response.Suggestions.Select(static s => s.ToString()).Concat(response.Skipped.Select(static s => $"keep {s.Lane}: {s.Reason}"))).ToList();
        if (response.Applied)
        {
            lines.Add("written to routing.lanes in the team config; relaunch the lanes (aw-meta-launch or lane-recycle) to start them on the new provider");
        }

        return lines.Count == 0 ? "nothing to switch" : string.Join(Environment.NewLine, lines);
    }
}
