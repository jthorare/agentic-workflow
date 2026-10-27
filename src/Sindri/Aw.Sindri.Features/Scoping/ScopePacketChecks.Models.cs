using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    private const string DrafterStep = "step.scope.draft";
    private const string NoRecordFix = "Run records come from the ledger's launch and usage events; an instance that launched no agent has none.";

    // The agent CLIs a judge provider can be, and the vendor names a run record may give for the model behind each.
    private static readonly Dictionary<string, string[]> _providerVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-cli"] = ["anthropic", "claude"],
        ["codex-cli"] = ["openai", "codex"],
        ["cursor-cli"] = ["cursor"],
    };

    /// <summary>SCP-CHAL-002: the challenger's model differs from the drafter's; a run at the desk is a warning (its model is self-reported), never a refusal.</summary>
    private static void ChallengerDiffers(ScopeContext c, RuleSink s) =>
        Differs(c, s, "step.scope.challenge", "challenger", failOnMissingRecord: true);

    /// <summary>SCP-GAP-002: the gap searcher's model differs from the drafter's, and the model it reports is the one the record holds.</summary>
    private static void GapSearcherDiffers(ScopeContext c, RuleSink s)
    {
        Differs(c, s, "step.scope.gap-search", "gap searcher", failOnMissingRecord: false);
        if (LastRun(Runs(c), "step.scope.gap-search") is { } run && !IsDesk(run))
        {
            Reported(s, "step.scope.gap-search", "gap searcher", run, ScopeJson.Items(c.Facts["gapSearch"], "rounds").LastOrDefault());
        }
    }

    /// <summary>SCP-REL-002: the judge's provider is not an agent CLI of the relevance agent's vendor, and the model the agent reports is the one the record holds.</summary>
    private static void RelevanceJudgeDiffers(ScopeContext c, RuleSink s)
    {
        const string step = "step.scope.relevance-review";
        if (!s.Need("relevance"))
        {
            return;
        }

        var runs = Runs(c);
        if (runs.Count == 0 || LastRun(runs, step) is not { } run)
        {
            s.Warn(step, "no run record for the relevance agent, so its vendor was not compared with the judge's", NoRecordFix);
            return;
        }

        if (IsDesk(run))
        {
            s.Warn(step, "the relevance agent ran at the desk: model self-reported", "Run the relevance review as an agent session for a recorded model.");
            return;
        }

        var vendor = ScopeJson.Str(run, "vendor");
        foreach (var provider in ScopeJson.Strings(ScopeJson.Obj(c.Facts["relevance"], "judge"), "providers"))
        {
            if (!string.IsNullOrEmpty(vendor) && _providerVendors.TryGetValue(provider, out var vendors) && vendors.Contains(vendor, StringComparer.OrdinalIgnoreCase))
            {
                s.Warn(step, $"the judge provider {provider} is an agent CLI of the relevance agent's vendor ({vendor})", "Point workflow.judge.providers at a provider of another vendor for the relevance judge.");
            }
        }

        Reported(s, step, "relevance agent", run, c.Facts["relevance"]);
    }

    // Which run is compared with the drafter's: its Step, the name the messages call it, and whether it is the challenger (whose spent loop turns a failure into a warning).
    private sealed record ModelComparison(string Step, string Role, bool Challenger);

    private static void Differs(ScopeContext c, RuleSink s, string step, string role, bool failOnMissingRecord)
    {
        var runs = Runs(c);
        if (runs.Count == 0)
        {
            s.Warn(step, $"no run records were found, so the {role}'s model was not compared with the drafter's", NoRecordFix);
            return;
        }

        var drafter = LastRun(runs, DrafterStep);
        var other = LastRun(runs, step);
        if (drafter is null || other is null)
        {
            var who = drafter is null ? "the drafter" : $"the {role}";
            if (failOnMissingRecord)
            {
                FailUnlessSpent(c, s, item: null, $"no run record for {who}", "Both agent runs leave a runtime record in runs.json.");
            }
            else
            {
                s.Warn(step, $"no run record for {who}, so the {role}'s model was not compared with the drafter's", NoRecordFix);
            }

            return;
        }

        ModelsDiffer(drafter, other, new ModelComparison(step, role, Challenger: failOnMissingRecord), c, s);
    }

    // why: once the challenge loop is spent the challenger never reruns, so the drafter can't change which model ran; the owner reads it at the stop
    private static void FailUnlessSpent(ScopeContext c, RuleSink s, string? item, string message, string fix)
    {
        if (Spent(c))
        {
            s.Warn(item, message + " and the second opinion is spent", fix);
        }
        else
        {
            s.Fail(item, message, fix);
        }
    }

    private static void ModelsDiffer(JsonNode drafter, JsonNode other, ModelComparison compare, ScopeContext c, RuleSink s)
    {
        var (step, role, challenger) = compare;
        var model = ScopeJson.Str(other, "model");
        var drafterModel = ScopeJson.Str(drafter, "model");
        if (IsDesk(other) || IsDesk(drafter))
        {
            var self = string.IsNullOrEmpty(model) ? "model not recorded" : "model self-reported";
            s.Warn(step, $"{role} ran at the desk: {self}", $"Run the {role} on a second model for a recorded model.");
        }
        else if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(drafterModel))
        {
            s.Warn(step, $"the {role}'s or the drafter's model was not recorded, so they were not compared", NoRecordFix);
        }
        else if (string.Equals(model, drafterModel, StringComparison.Ordinal))
        {
            // why: a default install names no models, so equal models there are only a warning; a fail needs both runs to have asked for one
            if (!string.IsNullOrEmpty(ScopeJson.Str(drafter, "requested")) && !string.IsNullOrEmpty(ScopeJson.Str(other, "requested")))
            {
                if (challenger)
                {
                    FailUnlessSpent(c, s, step, $"the {role} ran on {model}, the drafter's model", $"Run the {role} on a different model.");
                }
                else
                {
                    s.Fail(step, $"the {role} ran on {model}, the drafter's model", $"Run the {role} on a different model.");
                }
            }
            else
            {
                s.Warn(step, $"the {role} ran on {model}, the drafter's model; no model was requested for one of them (set workflow.scoping.models.*)", "Set workflow.scoping.models.* in the profile so each agent runs on its own model.");
            }
        }
    }

    // The record wins over what the agent says about itself; a difference is shown at the stop.
    private static void Reported(RuleSink s, string step, string role, JsonNode run, JsonNode? holder)
    {
        var recorded = ScopeJson.Str(run, "model");
        var reported = ScopeJson.Str(ScopeJson.Obj(holder, "agent"), "reportedModel");
        if (!string.IsNullOrEmpty(recorded) && !string.IsNullOrEmpty(reported) && !string.Equals(recorded, reported, StringComparison.Ordinal) && !string.Equals(reported, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            s.Warn(step, $"the {role} reported the model {reported}, the run record says {recorded}", "The record wins; check which model ran.");
        }
    }

    private static JsonArray Runs(ScopeContext c) => (c.Facts["runs"] is JsonArray all ? all : ScopeJson.Arr(c.Facts["runs"], "runs")) ?? [];

    private static JsonNode? LastRun(JsonArray runs, string step) =>
        ScopeJson.Items(runs).LastOrDefault(r => string.Equals(ScopeJson.Str(r, "step"), step, StringComparison.Ordinal));

    private static bool IsDesk(JsonNode run) => string.Equals(ScopeJson.Str(run, "via"), "desk", StringComparison.Ordinal);
}
