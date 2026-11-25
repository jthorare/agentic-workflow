using System.Globalization;
using Aw.Meta.Features.Delegated;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'delegated' command: one line per delegation, "#n lane state title".</summary>
internal static class DelegatedCommand
{
    internal static CommandEntry Entry { get; } = new(
        "delegated",
        new CommandSpec("delegated [--all] [--json]", [], [], ["all"]),
        NeedsConfig: true,
        static context => CommandOutput.Call<DelegatedRequest, DelegatedResponse>(
            context,
            new DelegatedRequest(context.Line.Has("all")),
            static r => string.Join(Environment.NewLine, r.Delegations.Select(static d => string.Create(CultureInfo.InvariantCulture, $"#{d.Issue} {d.Lane} {d.State} {d.Title}")))));
}
