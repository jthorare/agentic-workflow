using System.Diagnostics;
using System.Diagnostics.Metrics;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The command chain's spans and instruments (API spec §9 Telemetry), named 'Aw.Sindri' under the default 'Aw.*' listen
/// pattern: a 'sindri.command &lt;action&gt;' span and the 'sindri.commands' counter per attempt, a 'sindri.query &lt;name&gt;'
/// span per query, and the time a command waited at the gate. Tags are small fixed sets: class, caller actor, outcome.
/// </summary>
internal static class CommandTelemetry
{
    internal const string ClassTag = "sindri.command.class";
    internal const string CallerTag = "sindri.command.caller";
    internal const string OutcomeTag = "sindri.command.outcome";

    private const string Name = "Aw.Sindri";

    private static readonly Meter _meter = new(Name);

    private static readonly Dictionary<ActionClass, string> _classes = new()
    {
        [ActionClass.Request] = "request",
        [ActionClass.StepReport] = "step-report",
        [ActionClass.Pause] = "pause",
        [ActionClass.Job] = "job",
        [ActionClass.Decision] = "decision",
        [ActionClass.Desk] = "desk",
        [ActionClass.Local] = "local",
        [ActionClass.Issue] = "issue",
    };

    private static readonly Dictionary<CommandOutcome, string> _outcomes = new()
    {
        [CommandOutcome.Accepted] = "accepted",
        [CommandOutcome.Refused] = "refused",
        [CommandOutcome.Invalid] = "invalid",
        [CommandOutcome.Failed] = "failed",
    };

    internal static ActivitySource Source { get; } = new(Name);

    internal static Counter<long> Commands { get; } = _meter.CreateCounter<long>("sindri.commands", description: "Command attempts that reached the chain");

    internal static Histogram<double> GateWait { get; } =
        _meter.CreateHistogram<double>("sindri.command.gate_wait", unit: "s", description: "How long a command waited for the command gate");

    /// <summary>Tags <paramref name="span"/> and counts the attempt.</summary>
    internal static void Attempt(Activity? span, ActionClass actionClass, string caller, CommandOutcome outcome)
    {
        var tags = new TagList { { ClassTag, _classes[actionClass] }, { CallerTag, caller }, { OutcomeTag, _outcomes[outcome] } };
        foreach (var tag in tags)
        {
            _ = span?.SetTag(tag.Key, tag.Value);
        }

        Commands.Add(1, tags);
    }
}
