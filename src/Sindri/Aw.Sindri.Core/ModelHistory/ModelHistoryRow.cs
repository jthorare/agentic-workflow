using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>
/// How one model has done at one state of one recipe version (model-pick spec §7.2): one row of the 'model-history' view per (recipe, version, state, model).
/// The counters are for the model's lifetime; <see cref="Recent"/> holds the last <see cref="Window"/> outcomes, oldest first, and is all the escalation figures read,
/// so an old failure fades.
/// </summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Version">The recipe's version.</param>
/// <param name="State">The state whose Step the model ran.</param>
/// <param name="Model">The model id.</param>
/// <param name="Runs">Runs that ended (completed, refused, failed its check or crashed).</param>
/// <param name="Refused">Runs whose report was refused.</param>
/// <param name="CheckFailed">Runs that failed the Step's check.</param>
/// <param name="Crashed">Runs that died of something other than the model.</param>
/// <param name="RedraftCaused">Completed runs the instance later went back on.</param>
/// <param name="LastRunAt">When the latest run ended; null when there is none.</param>
/// <param name="Recent">The last <see cref="Window"/> outcomes, oldest first.</param>
public sealed record ModelHistoryRow(
    RecipeId Recipe, int Version, string State, string Model, int Runs, int Refused, int CheckFailed, int Crashed, int RedraftCaused, DateTimeOffset? LastRunAt,
    IReadOnlyList<ModelOutcome> Recent)
{
    /// <summary>How many outcomes <see cref="Recent"/> keeps.</summary>
    public const int Window = 20;

    /// <summary>
    /// The share of recent runs that were refused or failed their check. A crash is infrastructure, not the model, so it is left out of the count and of the total; a
    /// redraft is not a failure of the run and is not in the numerator, though it stays in the total. 0 when there is nothing to count.
    /// </summary>
    [JsonIgnore]
    public double EscalationRate
    {
        get
        {
            var counted = Recent.Count(static o => o != ModelOutcome.Crashed);
            return counted == 0 ? 0 : Recent.Count(static o => o is ModelOutcome.Refused or ModelOutcome.CheckFailed) / (double)counted;
        }
    }

    /// <summary>True when more than half of the runs that count were refused or failed their check, with at least one counted run.</summary>
    [JsonIgnore]
    public bool Unusual => Recent.Any(static o => o != ModelOutcome.Crashed) && EscalationRate > 0.5;

    /// <summary>The row of a model that has no history yet.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="version">Its version.</param>
    /// <param name="state">The state.</param>
    /// <param name="model">The model id.</param>
    /// <returns>A row with no runs.</returns>
    public static ModelHistoryRow Empty(RecipeId recipe, int version, string state, string model) =>
        new(recipe, version, state, model, Runs: 0, Refused: 0, CheckFailed: 0, Crashed: 0, RedraftCaused: 0, LastRunAt: null, Recent: []);

    /// <summary>The row after a run ended with <paramref name="outcome"/>.</summary>
    /// <param name="outcome">How it ended; never <see cref="ModelOutcome.Redraft"/>, which is <see cref="Redrafted"/>.</param>
    /// <param name="at">When it ended.</param>
    /// <returns>The row.</returns>
    public ModelHistoryRow After(ModelOutcome outcome, DateTimeOffset at)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(outcome, ModelOutcome.Redraft);
        return this with
        {
            Runs = Runs + 1,
            Refused = Refused + (outcome == ModelOutcome.Refused ? 1 : 0),
            CheckFailed = CheckFailed + (outcome == ModelOutcome.CheckFailed ? 1 : 0),
            Crashed = Crashed + (outcome == ModelOutcome.Crashed ? 1 : 0),
            LastRunAt = at,
            Recent = [.. Recent.Append(outcome).TakeLast(Window)],
        };
    }

    /// <summary>The row after the instance went back on a completed run of this model: the count goes up, and the latest 'ok' in <see cref="Recent"/> reads 'redraft'.</summary>
    /// <returns>The row.</returns>
    public ModelHistoryRow Redrafted()
    {
        var recent = Recent.ToArray();
        var latest = Array.LastIndexOf(recent, ModelOutcome.Ok);
        if (latest >= 0)
        {
            recent[latest] = ModelOutcome.Redraft;
        }

        return this with { RedraftCaused = RedraftCaused + 1, Recent = recent };
    }
}
