using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>The design packet's rule ids and the stage each first runs at (line spec §4.3).</summary>
internal static class DesignRules
{
    internal const string Cov1 = "DSN-COV-001";
    internal const string Cov2 = "DSN-COV-002";
    internal const string Acc1 = "DSN-ACC-001";
    internal const string Acc2 = "DSN-ACC-002";
    internal const string Acc3 = "DSN-ACC-003";
    internal const string Dec1 = "DSN-DEC-001";
    internal const string Cite1 = "DSN-CITE-001";
    internal const string Xspec1 = "DSN-XSPEC-001";
    internal const string Xspec2 = "DSN-XSPEC-002";
    internal const string Xspec3 = "DSN-XSPEC-003";
    internal const string Style1 = "DSN-STYLE-001";
    internal const string Lens1 = "DSN-LENS-001";
    internal const string Lens2 = "DSN-LENS-002";
    internal const string Lens3 = "DSN-LENS-003";
    internal const string Esc1 = "DSN-ESC-001";

    /// <summary>Every rule in the order the checklist shows it.</summary>
    internal static IReadOnlyList<LineRule> All { get; } =
    [
        new(Cov1, LineStage.Draft, DesignPacketChecks.SurfacesCovered),
        new(Cov2, LineStage.Draft, DesignPacketChecks.QuestionsHandled),
        new(Acc1, LineStage.Draft, DesignPacketChecks.ItemsHaveSignals),
        new(Acc2, LineStage.Draft, DesignPacketChecks.SignalsMeasurable),
        new(Acc3, LineStage.Evidence, DesignPacketChecks.AcceptanceChain),
        new(Dec1, LineStage.Draft, DesignPacketChecks.DecisionsWellFormed),
        new(Cite1, LineStage.Draft, DesignPacketChecks.SectionsCite),
        new(Xspec1, LineStage.Draft, DesignPacketChecks.EditTargetsExist),
        new(Xspec2, LineStage.Draft, DesignPacketChecks.EditQuotesExist),
        new(Xspec3, LineStage.Draft, DesignPacketChecks.EditsFiledAsRows),
        new(Style1, LineStage.Draft, DesignPacketChecks.FollowsStylePack),
        new(Lens1, LineStage.Evidence, DesignPacketChecks.LensRoundsClean),
        new(Lens2, LineStage.Evidence, DesignPacketChecks.LensDiffersFromWriter),
        new(Lens3, LineStage.Evidence, DesignPacketChecks.LensFindingsResolved),
        new(Esc1, LineStage.Evidence, DesignPacketChecks.EscapesListed),
    ];
}
