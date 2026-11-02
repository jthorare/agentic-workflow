using System.Globalization;
using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetLaunchPicks;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri launch picks [--item X] [--json]' (model-pick spec §6.1): a client of GET /sindri/launch/picks. Read-only, so it never
/// starts the control plane: when serve is down it exits 1 with SND-API-001, like 'sindri status'. One line per pick, oldest first;
/// '--json' prints the route's answer.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class LaunchPicksVerb(ControlPlaneClient client, Output output)
{
    internal const string Usage = "launch picks [--item X] [--json]";

    private const string Route = "/sindri/launch/picks";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (json, item) = (false, (string?)null);
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--item" when i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    item = args[++i];
                    break;
                default:
                    return await output.Error(SindriErrors.UnknownVerb.With(Usage)).ConfigureAwait(false);
            }
        }

        var path = item is null ? Route : Route + "?item=" + Uri.EscapeDataString(item);
        var answer = await client.Get<LaunchPicks>(path, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(Render(answer.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>The text form: one line per pick (item, step, agent, model, who picked it, why, tokens, cost, what Jev's pick cost).</summary>
    /// <param name="picks">The route's answer.</param>
    /// <returns>The lines, or 'no picks'.</returns>
    internal static string Render(LaunchPicks picks)
    {
        ArgumentNullException.ThrowIfNull(picks);
        if (picks.Picks.Count == 0)
        {
            return "no picks\n";
        }

        var text = new StringBuilder();
        foreach (var pick in picks.Picks)
        {
            _ = text.AppendJoin("  ", Columns(pick)).Append('\n');
        }

        foreach (var rate in picks.Escalation ?? [])
        {
            _ = text.Append(rate.State).Append("  ").Append(rate.Model).Append("  ").Append(rate.Sentence).Append('\n');
        }

        return text.ToString();
    }

    private static string[] Columns(LaunchPickView pick)
    {
        var step = pick.State + (pick.Escalation > 0 ? ".e" + pick.Escalation.ToString(CultureInfo.InvariantCulture) : string.Empty);
        return
        [
            pick.Item.Value, step, pick.Pick?.Target.Value ?? "-", pick.Pick is { } picked ? picked.Model ?? "(agent default)" : "choosing model…", pick.Pick?.By ?? "-", pick.Why,
            Tokens(pick.InputTokens + pick.OutputTokens), Cost(pick), Jev(pick.Pick),
        ];
    }

    private static string Tokens(long total) =>
        total >= 1_000 ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(total / 1_000.0, MidpointRounding.AwayFromZero)}k tokens") : string.Create(CultureInfo.InvariantCulture, $"{total} tokens");

    private static string Cost(LaunchPickView pick) =>
        pick.CostUsd is { } usd ? "$" + usd.ToString("0.00", CultureInfo.InvariantCulture) : pick.InputTokens + pick.OutputTokens > 0 ? "cost unknown" : "-";

    private static string Jev(Core.Events.ModelPicked? pick) => pick switch
    {
        null => "-",
        { JevCalls: 0 } => "jev not asked",
        _ => string.Create(CultureInfo.InvariantCulture, $"jev {pick.JevCalls} {(pick.JevCalls == 1 ? "call" : "calls")} {pick.JevMs / 1_000.0:0.0} s"),
    };
}
