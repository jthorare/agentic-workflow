namespace Aw.Sindri.Features.Scoping;

/// <summary>The rule ids and the stage each first runs at (scoping spec §8.2). SCP-SCHEMA-001 is SC-K's own: the draft's facts against the schemas' fact form.</summary>
internal static class ScopeRules
{
    internal const string Schema = "SCP-SCHEMA-001";
    internal const string Cite1 = "SCP-CITE-001";
    internal const string Cite2 = "SCP-CITE-002";
    internal const string Cov1 = "SCP-COV-001";
    internal const string Cov2 = "SCP-COV-002";
    internal const string Cov3 = "SCP-COV-003";
    internal const string Cov4 = "SCP-COV-004";
    internal const string Cov5 = "SCP-COV-005";
    internal const string Lens1 = "SCP-LENS-001";
    internal const string Acc1 = "SCP-ACC-001";
    internal const string Acc2 = "SCP-ACC-002";
    internal const string Acc3 = "SCP-ACC-003";
    internal const string Acc4 = "SCP-ACC-004";
    internal const string Acc5 = "SCP-ACC-005";
    internal const string Ans1 = "SCP-ANS-001";
    internal const string Trust1 = "SCP-TRUST-001";
    internal const string Chal1 = "SCP-CHAL-001";
    internal const string Chal2 = "SCP-CHAL-002";
    internal const string Map1 = "SCP-MAP-001";
    internal const string Rel1 = "SCP-REL-001";
    internal const string Rel2 = "SCP-REL-002";
    internal const string Gap1 = "SCP-GAP-001";
    internal const string Gap2 = "SCP-GAP-002";

    /// <summary>Every rule in the order the checklist shows it, with the first stage it runs at (an evidence run runs all).</summary>
    internal static IReadOnlyList<(string Id, ScopeStage From)> All { get; } =
    [
        (Schema, ScopeStage.Draft), (Cite1, ScopeStage.Draft), (Cite2, ScopeStage.Draft),
        (Cov1, ScopeStage.Draft), (Cov2, ScopeStage.Draft), (Cov3, ScopeStage.Draft), (Cov4, ScopeStage.Evidence), (Cov5, ScopeStage.Evidence),
        (Lens1, ScopeStage.Evidence),
        (Acc1, ScopeStage.Draft), (Acc2, ScopeStage.Draft), (Acc3, ScopeStage.Evidence), (Acc4, ScopeStage.Evidence), (Acc5, ScopeStage.Draft),
        (Ans1, ScopeStage.Draft), (Trust1, ScopeStage.Draft), (Chal1, ScopeStage.Evidence), (Chal2, ScopeStage.Evidence), (Map1, ScopeStage.Draft),
        (Rel1, ScopeStage.Evidence), (Rel2, ScopeStage.Evidence), (Gap1, ScopeStage.Evidence), (Gap2, ScopeStage.Evidence),
    ];

    /// <summary>The recipe loop whose cap SCP-CHAL-001 reads (workflow.scoping loops.challenge): the last round found something new only warns once that many rounds ran.</summary>
    internal const string ChallengeLoop = "challenge";

    /// <summary>The shipped cap of <see cref="ChallengeLoop"/> (owner decision q40), used only where no recipe runs the check: the standalone 'scoping check' and 'scoping show --facts', and the stop's read model. A Step on the block runtime reads the running recipe's cap instead.</summary>
    internal const int DefaultChallengeRounds = 3;

    /// <summary>The recipe loop whose cap SCP-GAP-001 reads (workflow.scoping loops.gapsearch): a confirmed last round only warns once that many rounds ran.</summary>
    internal const string GapLoop = "gapsearch";

    /// <summary>The shipped cap of <see cref="GapLoop"/>, used only where no recipe runs the check. A Step on the block runtime reads the running recipe's cap instead.</summary>
    internal const int DefaultGapRounds = 3;

    /// <summary>The four lenses, in the order the coverage and the lens file list them.</summary>
    internal static IReadOnlyList<string> Lenses { get; } = ["code", "product", "related", "history"];

    /// <summary>The behaviour dimensions the challenger walks (scoping spec §7.1).</summary>
    internal static IReadOnlyList<string> Dimensions { get; } =
    [
        "who may use it", "success", "failure", "empty", "limit", "how long data lives", "what is notified", "what changes for existing data", "how it rolls out",
    ];
}
