using System.Globalization;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRun;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'gate-run' command: exit 0 when the gate passes, 1 when it is red or the head conflicts.</summary>
internal static class GateRunCommand
{
    internal static CommandEntry Entry { get; } = new(
        "gate-run",
        new CommandSpec("gate-run <pr> [scope...] [--json]", [], [], [], Positionals: 1, AnyMore: true),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context) =>
        PrArgument.TryParse(context.Line.Positional(0), out var pr)
            ? CommandOutput.Call<GateRunRequest, GateRunResponse>(context, new GateRunRequest(pr, [.. context.Line.Positionals.Skip(1)]), Text, static r => r.Gate.Verdict != GateVerdict.Pass)
            : Task.FromResult(PrArgument.Usage(context, Entry));

    private static string Text(GateRunResponse response) =>
        string.Join(Environment.NewLine, [.. Lines(response.Gate, stack: false), .. response.Heavy is { } heavy ? [heavy.Line()] : Array.Empty<string>(), Result(response.Gate)]);

    /// <summary>The gate's header and one line per step; a train gate's header also names the pull requests under it.</summary>
    internal static IEnumerable<string> Lines(GateRecord gate, bool stack)
    {
        var width = gate.Steps.Select(s => s.Name.Length).DefaultIfEmpty().Max();
        var under = stack && gate.Stack.Count > 0 ? " stacked on " + string.Join(", ", gate.Stack.Select(s => string.Create(CultureInfo.InvariantCulture, $"#{s.Pr}"))) : string.Empty;
        return
        [
            string.Create(CultureInfo.InvariantCulture, $"gate #{gate.Pr} run {gate.Run}: head {Short(gate.Head)}{(gate.Main.Length == 0 ? string.Empty : " on main " + Short(gate.Main))}{under}; scopes: {(gate.Scopes.Count == 0 ? "none" : string.Join(", ", gate.Scopes))}"),
            .. gate.Steps.Select(s => string.Concat("  ", s.Verdict == GateVerdict.Pass ? "PASS" : "FAIL", " ", s.Name.PadRight(width), "  ", s.Log)),
        ];
    }

    internal static string Result(GateRecord gate) => string.Create(CultureInfo.InvariantCulture, $"RESULT pr={gate.Pr} {gate.Verdict.ToString().ToUpperInvariant()}");

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
