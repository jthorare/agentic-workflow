using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>Builds a needs-you wire row with deterministic defaults: the first decision as a pulsing, recorded-only 're-scope' decision row at the epoch.</summary>
public sealed class NeedsYouRowBuilder
{
    private NeedsYouRow _row = new(NeedsYouRowId.From("decision:" + A.FirstDecisionId.Value), NeedsYouKind.Decision, "decision · re-scope",
        new NeedsYouSubject(SubjectKind.Decision, SubjectId.From(A.FirstDecisionId.Value)), Item: null, "Split the work into two items.",
        "re-scope request from session:cli", Repo: null, Urgency.Pulse, KnownInstants.Epoch, RecordedOnly: true, EmptyText.RecordedOnly(),
        AnswerKind: null, ActionId.From("Decide"), [ActionId.From("Decide")], "sindri decide " + A.FirstDecisionId.Value + " accept",
        new NeedsYouOpen("decision/" + A.FirstDecisionId.Value, "Review and decide"));

    /// <summary>Sets the urgency and when the row started to ask.</summary>
    /// <param name="urgency">The urgency.</param>
    /// <param name="since">When.</param>
    /// <returns>This builder.</returns>
    public NeedsYouRowBuilder Urgent(Urgency urgency, DateTimeOffset since)
    {
        _row = _row with { Urgency = urgency, Since = since };
        return this;
    }

    /// <summary>Sets the repo.</summary>
    /// <param name="value">The repo, or null.</param>
    /// <returns>This builder.</returns>
    public NeedsYouRowBuilder InRepo(RepoName? value)
    {
        _row = _row with { Repo = value };
        return this;
    }

    /// <summary>Builds the row.</summary>
    /// <returns>The row.</returns>
    public NeedsYouRow Build() => _row;
}
