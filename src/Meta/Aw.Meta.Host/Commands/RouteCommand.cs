namespace Aw.Meta.Host.Commands;

/// <summary>The 'route' command: which lanes should move to another provider. Exit 1 while a suggestion waits.</summary>
internal static class RouteCommand
{
    internal static CommandEntry Entry { get; } = new(
        "route",
        new CommandSpec("route [--lane <l>] [--apply] [--json]", [], ["lane"], ["apply"]),
        NeedsConfig: true,
        static context => throw new NotSupportedException());
}
