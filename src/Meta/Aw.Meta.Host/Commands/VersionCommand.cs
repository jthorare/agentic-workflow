using Aw.Meta.Core.Ports;
using Aw.Meta.Features.ShowVersion;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'version' command.</summary>
internal static class VersionCommand
{
    internal static CommandEntry Entry { get; } = new(
        "version",
        CommandSpec.None("version [--json]"),
        NeedsConfig: false,
        static context => CommandOutput.Call<ShowVersionRequest, BuildInfo>(context, new ShowVersionRequest(), Text));

    private static string Text(BuildInfo build) => $"aw-meta commit {build.Commit}, built {build.BuiltAt}, {build.Os} {build.Rid}";
}
