using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Model picks table (model-pick spec §6.1).</summary>
internal static partial class Labels
{
    /// <summary>The table's heading.</summary>
    internal const string ModelPicksHeading = "Model picks";

    /// <summary>The Model cell while Jev is being asked.</summary>
    internal const string ChoosingModel = "choosing model…";

    /// <summary>The Why cell while Jev is being asked.</summary>
    internal const string AskingJev = "asking Jev";

    /// <summary>The Cost cell of a model with no price.</summary>
    internal const string CostUnknown = "cost unknown";

    /// <summary>The Model cell of a run on the agent's own default.</summary>
    internal const string AgentDefaultModel = "agent default";

    /// <summary>The column headers.</summary>
    internal static readonly string[] ModelPickColumns = ["Step", "Model", "Tokens", "Cost", "Why"];

    /// <summary>The Why panel's headings.</summary>
    internal const string WhyPanelTitle = "Why this model";

    /// <summary>The Why panel's candidates heading.</summary>
    internal const string WhyCandidates = "Models Jev was shown";

    /// <summary>The Why panel's files heading.</summary>
    internal const string WhyFiles = "Files Jev saw";

    /// <summary>The Why panel's close button.</summary>
    internal const string WhyClose = "Close";

    /// <summary>A model id as its short name: "claude-haiku-4-5" is "Haiku".</summary>
    /// <param name="model">The served id or alias; null for the agent's default.</param>
    /// <returns>The short name.</returns>
    internal static string ModelShortName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return AgentDefaultModel;
        }

        foreach (var family in new[] { "haiku", "sonnet", "opus" })
        {
            if (model.Contains(family, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(char.ToUpperInvariant(family[0]).ToString(), family.AsSpan(1));
            }
        }

        return ReadableWord(model);
    }

    /// <summary>The Model cell's accessible name.</summary>
    /// <param name="model">The short name.</param>
    /// <param name="by">jev, constraint, rules or default.</param>
    /// <returns>"Model: Haiku, picked by Jev".</returns>
    internal static string ModelCellName(string model, string by) => by switch
    {
        "jev" => $"Model: {model}, picked by Jev",
        "constraint" => $"Model: {model}, the only model allowed",
        "rules" => $"Model: {model}, picked by Sindri's rules",
        _ => $"Model: {model}",
    };

    /// <summary>A token count the way the table shows it.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>"950", "3.5k", "41k" or "1.5M".</returns>
    internal static string TokensShort(long tokens) => tokens switch
    {
        < 1_000 => tokens.ToString(CultureInfo.InvariantCulture),
        < 10_000 => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(tokens / 1000.0, 1, MidpointRounding.AwayFromZero):0.#}k"),
        < 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(tokens / 1000.0, 0, MidpointRounding.AwayFromZero):0}k"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(tokens / 1_000_000.0, 1, MidpointRounding.AwayFromZero):0.#}M"),
    };

    /// <summary>A price.</summary>
    /// <param name="usd">The cost.</param>
    /// <returns>"$0.18".</returns>
    internal static string Dollars(decimal usd) => string.Create(CultureInfo.InvariantCulture, $"${usd:0.00}");

    /// <summary>The Total line.</summary>
    /// <param name="runs">The runs counted.</param>
    /// <param name="tokens">Their tokens.</param>
    /// <param name="usd">The priced cost.</param>
    /// <param name="unpricedModels">How many models had no price.</param>
    /// <returns>"3 runs, 96k tokens, $0.61".</returns>
    internal static string PicksTotal(int runs, long tokens, decimal usd, int unpricedModels)
    {
        var head = string.Create(CultureInfo.InvariantCulture, $"{runs} {(runs == 1 ? "run" : "runs")}, {TokensShort(tokens)} tokens");
        return unpricedModels == 0
            ? $"{head}, {Dollars(usd)}"
            : string.Create(CultureInfo.InvariantCulture, $"{head}, cost partial: {unpricedModels} {(unpricedModels == 1 ? "model has" : "models have")} no price");
    }

    /// <summary>Why a file was left out of the pick.</summary>
    /// <param name="why">cap, denied or secret.</param>
    /// <returns>The words.</returns>
    internal static string OmittedWhy(string why) => why switch
    {
        "cap" => "over the size limit",
        "denied" => "a denied path",
        "secret" => "may hold a secret",
        _ => ReadableWord(why).ToLowerInvariant(),
    };
}
