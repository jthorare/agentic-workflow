using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The proof pack's rules (line spec §7.3, PRV-*): pure decisions over <see cref="ProofFacts"/>. A rule whose inputs the facts do not carry is listed in
/// <see cref="ProofReport.NotRun"/> (a warning: PRV-APP-001 until the evidence Step reads the evidence artifacts, PRV-HOLD-003 until the hold-out log records its canary scan), except
/// the rules in <see cref="Required"/> (PRV-ACC-001 and PRV-SHA-001), which have no structural cover in the recipe: without 'fact:proof' they are listed in
/// <see cref="ProofReport.Unrunnable"/> and no proof stands. The rest run on every call.
/// </summary>
public static partial class ProofChecks
{
    /// <summary>The rules that must run for a proof to stand (LN7): a proof that cannot run them is not a proof, whatever else holds.</summary>
    public static IReadOnlyList<string> Required { get; } = ["PRV-ACC-001", "PRV-SHA-001"];

    /// <summary>Runs every rule.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The failures and the rules that did not run.</returns>
    public static ProofReport Run(ProofFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var failures = new List<ProofFinding>();
        var notRun = new List<string>();
        var unrunnable = new List<string>();
        Artifacts(facts, failures, unrunnable);
        Application(facts, notRun);
        BeforeAfter(facts, failures);
        Red(facts, failures);
        Holdout(facts, failures, notRun);
        failures.AddRange(ProofPack.AcceptanceFlips(facts.Acceptance, ProofPack.Acceptance(facts)));
        return new ProofReport(failures, notRun, unrunnable);
    }

    /// <summary>The ids of the phase's items that still need proving: every item that is not superseded.</summary>
    /// <param name="acceptance">The phase packet's acceptance.json.</param>
    /// <returns>The ids.</returns>
    public static IReadOnlyList<string> LiveItems(JsonNode acceptance) =>
        [.. (acceptance?["items"] as JsonArray ?? []).OfType<JsonObject>().Where(static i => (string?)i["status"] != "superseded").Select(static i => (string?)i["id"] ?? string.Empty)];

    private static void Artifacts(ProofFacts facts, List<ProofFinding> failures, List<string> unrunnable)
    {
        if (facts.Proof is null)
        {
            unrunnable.AddRange(Required.Select(static rule => $"{rule}: fact:proof (the evidence artifacts) is not among the facts the evidence Step reads"));
            return;
        }

        foreach (var id in LiveItems(facts.Acceptance).Where(id => !ArtifactsOf(facts.Proof, id).Any(a => OnHead(a, facts.Head))))
        {
            failures.Add(new ProofFinding("PRV-ACC-001", id, $"{id}: no passing evidence ran on the head {Short(facts.Head)}", "run the evidence for this item on the current head"));
        }

        foreach (var (id, artifact) in AllArtifacts(facts.Proof).Where(a => (string?)a.Artifact["commit"] != facts.Head && (bool?)a.Artifact["superseded"] != true))
        {
            failures.Add(new ProofFinding("PRV-SHA-001", id, $"{id}: the artifact {(string?)artifact["path"]} ran on {Short((string?)artifact["commit"])}, not the head {Short(facts.Head)}, and is not marked superseded", "run it again on the head, or mark the older artifact superseded"));
        }
    }

    private static bool OnHead(JsonObject artifact, string head) =>
        (bool?)artifact["passing"] == true && (string?)artifact["commit"] == head && (bool?)artifact["superseded"] != true;

    private static IEnumerable<(string? Item, JsonObject Artifact)> AllArtifacts(JsonNode proof) =>
        (proof["items"] as JsonArray ?? []).OfType<JsonObject>().SelectMany(static i => (i["artifacts"] as JsonArray ?? []).OfType<JsonObject>().Select(a => ((string?)i["id"], a)));

    private static void Application(ProofFacts facts, List<string> notRun)
    {
        if (!Exempt(facts))
        {
            notRun.Add("PRV-APP-001: the hand test and verify results (fact:manualTest, fact:evidencePack) are not among the facts the evidence Step reads");
        }
    }

    private static void BeforeAfter(ProofFacts facts, List<ProofFinding> failures)
    {
        var results = (facts.BeforeAfter?["results"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        foreach (var (item, signal) in BaseAndHead(facts))
        {
            var result = results.Find(r => (string?)r["item"] == item && ((string?)r["signal"] is not { } s || string.Equals(s, signal, StringComparison.Ordinal)));
            if (BeforeAfterProblem(result) is { } problem)
            {
                failures.Add(new ProofFinding("PRV-BA-001", item, $"{item}: the {signal} check {problem}", "record the fix's check so it fails at the base and passes at the head, by the same script"));
            }
        }
    }

    private static string? BeforeAfterProblem(JsonObject? result)
    {
        if (result is null)
        {
            return "has no before/after result";
        }

        if ((string?)result["base"] != "fail")
        {
            return $"did not fail at the base ({(string?)result["base"] ?? "no result"})";
        }

        if ((string?)result["head"] != "pass")
        {
            return $"did not pass at the head ({(string?)result["head"] ?? "no result"})";
        }

        return result["baseScript"] is { } baseScript && result["headScript"] is { } headScript && (string?)baseScript != (string?)headScript ? "ran a different script at the head than at the base" : null;
    }

    private static IEnumerable<(string Item, string Signal)> BaseAndHead(ProofFacts facts) =>
        LiveItems(facts.Acceptance).SelectMany(id => PhaseSignals.Of(facts.Signals, id)
            .Where(static s => (string?)s["runsAt"] == "base+head").Select(s => (id, (string?)s["id"] ?? string.Empty)));

    private const string RedShape = "expected {\"tests\": [id or {id, path}], \"appOnly\": {signal}} or a list of test ids";

    // fact:redTests (spec A.7, §7.2): an object keyed by item id, each value a list of test ids or {tests?, appOnly?, commit?, output?}. BLD-RED-001 (the tests failed) is build's red-check.
    private static void Red(ProofFacts facts, List<ProofFinding> failures)
    {
        if (Exempt(facts))
        {
            return;
        }

        if (facts.Red is not JsonObject red)
        {
            failures.Add(new ProofFinding("PRV-RED-001", Item: null, $"red tests are recorded in the wrong shape: expected an object keyed by item id ({RedShape})", "record the failing tests per item"));
            return;
        }

        failures.AddRange(LiveItems(facts.Acceptance).Select(id => RedFinding(id, RedEntry(red[id]))).OfType<ProofFinding>());
    }

    private static ProofFinding? RedFinding(string id, RedEntryState state) => state switch
    {
        RedEntryState.Recorded => null,
        RedEntryState.Missing => new ProofFinding("PRV-RED-001", id, $"{id}: no red test or app-only entry was recorded for this item", "write the failing tests first and record them, or record the app-only signal"),
        RedEntryState.WrongShape => new ProofFinding("PRV-RED-001", id, $"{id}: the red tests are recorded in the wrong shape ({RedShape})", "record the item's red tests in the expected shape"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "an unknown red entry state"),
    };

    private static RedEntryState RedEntry(JsonNode? entry) => entry switch
    {
        null => RedEntryState.Missing,
        JsonArray list => TestIds(list) is { } count ? (count > 0 ? RedEntryState.Recorded : RedEntryState.Missing) : RedEntryState.WrongShape,
        JsonObject body => RedBody(body),
        _ => RedEntryState.WrongShape,
    };

    private static RedEntryState RedBody(JsonObject body)
    {
        var tests = body["tests"] is null ? 0 : body["tests"] is JsonArray list ? TestIds(list) : null;
        var appOnly = body["appOnly"];
        if (tests is null || appOnly is not (null or JsonObject))
        {
            return RedEntryState.WrongShape;
        }

        return tests > 0 || appOnly is not null ? RedEntryState.Recorded : RedEntryState.Missing;
    }

    // The number of tests a list names, or null when an element is neither an id nor an {id, path} object.
    private static int? TestIds(JsonArray list) =>
        list.All(static t => (t is JsonValue v && v.TryGetValue<string>(out _)) || (t is JsonObject o && o["id"] is JsonValue)) ? list.Count : null;

    private enum RedEntryState
    {
        Recorded,
        Missing,
        WrongShape,
    }

    private static IEnumerable<JsonObject> ArtifactsOf(JsonNode proof, string id) =>
        (proof["items"] as JsonArray ?? []).OfType<JsonObject>().Where(i => (string?)i["id"] == id).SelectMany(static i => (i["artifacts"] as JsonArray ?? []).OfType<JsonObject>());

    private static bool Exempt(ProofFacts facts) =>
        string.Equals(facts.PhaseKind, "docs", StringComparison.Ordinal) || string.Equals(facts.PhaseKind, "config", StringComparison.Ordinal);

    private static string Short(string? sha) => sha is { Length: > 12 } ? sha[..12] : sha ?? "none";
}
