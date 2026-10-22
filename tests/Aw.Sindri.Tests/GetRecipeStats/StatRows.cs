using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Tests.GetRecipeStats;

/// <summary>Workflow ledger rows as the recipe stats read them, placed by how long before <see cref="Now"/> they happened.</summary>
internal static class StatRows
{
    public const string ItemHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    public const string OtherHash = "sha256:2222222222222222222222222222222222222222222222222222222222222222";

    public static DateTimeOffset Now { get; } = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The time <paramref name="days"/> days and <paramref name="minutes"/> minutes before <see cref="Now"/>.</summary>
    public static DateTimeOffset Ago(double days, double minutes = 0) => Now.AddDays(-days).AddMinutes(-minutes);

    public static StatEvent Start(string address, DateTimeOffset at, string state = "produce", string recipe = "workflow.item", string hash = ItemHash) =>
        Row(address, at, string.Empty, state, recipe: recipe, hash: hash);

    public static StatEvent Move(string address, DateTimeOffset at, string from, string to, int? tokens = null, params StatGate[] gates) =>
        Row(address, at, from, to, tokens: tokens, gates: gates);

    public static StatEvent End(string address, DateTimeOffset at, string from, string to, string outcome) =>
        Row(address, at, from, to, outcome: outcome);

    public static StatEvent Park(string address, DateTimeOffset at, string state, ParkReason reason) =>
        Row(address, at, state, state, suspension: SuspensionKind.Park, park: reason);

    public static StatEvent Escalate(string address, DateTimeOffset at, string state) =>
        Row(address, at, state, state, suspension: SuspensionKind.Escalation, escalation: EscalationKind.Gate);

    public static StatEvent TakeOver(string address, DateTimeOffset at, string state) =>
        Row(address, at, state, state, suspension: SuspensionKind.Takeover);

    /// <summary>A human's answer (or a handback) that ends the suspension.</summary>
    public static StatEvent Resume(string address, DateTimeOffset at, string state, bool answered = true) =>
        Row(address, at, state, state, answered: answered);

    public static StatGate Gate(string gate, GateOutcome outcome, double? threshold = null) => new(gate, outcome, threshold);

    private static StatEvent Row(string address, DateTimeOffset at, string from, string to, string? recipe = null, string? hash = null, SuspensionKind? suspension = null, ParkReason? park = null,
        EscalationKind? escalation = null, bool answered = false, string? outcome = null, int? tokens = null, StatGate[]? gates = null) =>
        new(address, at, from, to, recipe is null ? null : RecipeId.From(recipe), hash, HasDetail: true, suspension, park, escalation, answered, outcome, tokens, gates ?? []);
}
