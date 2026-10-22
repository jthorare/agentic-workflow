using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Features.Blocks.Evidence;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Features.Blocks.Step;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The blocks and events this build ships (spec §12): pure blocks with AddGate/AddStep/…, the P6 effects with
/// AddEffect, the desk Steps as descriptors only (AddBlockDescriptor), and the events with AddEvent.
/// </summary>
internal static class BlockRegistrations
{
    /// <summary>Registers the shipped blocks and events.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecipeBlocks(this IServiceCollection services) => services
        .AddSingleton<IBlockSentences, ShippedBlockSentences>()
        .AddBlockDescriptor(DeskSteps.Produce)
        .AddBlockDescriptor(DeskSteps.Checks)
        .AddBlockDescriptor(DeskSteps.Verify)
        .AddBlockDescriptor(ChoiceSteps.JudgeChoose)
        .AddBlockDescriptor(ChoiceSteps.DeskChoose)
        .AddBlockDescriptor(BugfixSteps.IntakeTicket)
        .AddBlockDescriptor(BugfixSteps.RootcauseInvestigate)
        .AddBlockDescriptor(BugfixSteps.ReproduceUiCheck)
        .AddBlockDescriptor(BugfixSteps.ReproduceTestCheck)
        .AddBlockDescriptor(BugfixSteps.StartAttempt)
        .AddBlockDescriptor(BugfixSteps.RecordCandidate)
        .AddBlockDescriptor(BugfixSteps.Recheck)
        .AddBlockDescriptor(BugfixSteps.Judge)
        .AddBlockDescriptor(BugfixSteps.ReportWrite)
        .AddBlockDescriptor(ProofSteps.HandtestRun)
        .AddBlockDescriptor(ProofSteps.VerifyJourney)
        .AddBlockDescriptor(ProofSteps.VerifyDesign)
        .AddBlockDescriptor(EvidenceSteps.UiPlan)
        .AddBlockDescriptor(EvidenceSteps.ApiPlan)
        .AddBlockDescriptor(EvidenceSteps.DbPlan)
        .AddBlockDescriptor(EvidenceSteps.UiRun)
        .AddBlockDescriptor(EvidenceSteps.ApiRun)
        .AddBlockDescriptor(EvidenceSteps.DbRun)
        .AddBlockDescriptor(EvidenceSteps.None)
        .AddBlockDescriptor(EvidenceSteps.Combine)
        .AddBlockDescriptor(EvidenceSteps.Desk)
        .AddReviewBlocks()
        .AddBlockDescriptor(OwnerSteps.Decide)
        .AddScopingBlocks()
        .AddLineBlocks().AddLineDesignBlocks()
        .AddBlockDescriptor(EvidenceProviders.Check)
        .AddGate<BudgetAvailableGate>()
        .AddGate<RouteStartableGate>()     // engine P7 unit U1: a stub over the profile's workflow.route
        .AddEffect<ClaimRefEffect>()      // engine P6 unit (d)
        .AddEffect<NotifyDeskEffect>()    // engine P6 unit (c)
        .AddEvent(RecipeEvents.ItemRouted)
        .AddEvent(RecipeEvents.ClaimCreated)
        .AddEvent(RecipeEvents.NotifyDelivered)
        .AddPrEvents();

    /// <summary>Registers the PR babysitter's and docs sync's events (PB spec §6.1, W25).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddPrEvents(this IServiceCollection services) => services
        .AddEvent(RecipeEvents.PrChanged)
        .AddEvent(RecipeEvents.PrPushed)
        .AddEvent(RecipeEvents.PrRestacked)
        .AddEvent(RecipeEvents.PrMergeResult)
        .AddEvent(RecipeEvents.PrDocsMerged)
        .AddEvent(RecipeEvents.PrStacked);

    /// <summary>Registers the line's blocks (the build and prove stations' and the per-phase child's).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddLineBlocks(this IServiceCollection services) => services.AddBuildProveBlocks().AddShipDocsBlocks().AddPhaseBlocks();

    /// <summary>Registers the per-phase child's Steps, the babysitter stub's Step and the line report effect (line spec §7, App. C.6), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddPhaseBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(PhaseSteps.Seed)
        .AddBlockDescriptor(PhaseSteps.Drift)
        .AddBlockDescriptor(PhaseSteps.Decide)
        .AddBlockDescriptor(BabysitSteps.Watch)
        .AddBlockDescriptor(LineEffects.Report);

    /// <summary>Registers the line's build and prove stations' Steps (line spec §7.2, §7.3), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddBuildProveBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(BuildSteps.Prepare)
        .AddBlockDescriptor(BuildSteps.Red)
        .AddBlockDescriptor(BuildSteps.RedCheck)
        .AddBlockDescriptor(BuildSteps.Green)
        .AddBlockDescriptor(BuildSteps.Checks)
        .AddBlockDescriptor(BuildSteps.ScopeDesk)
        .AddBlockDescriptor(BuildSteps.Amend)
        .AddBlockDescriptor(ProveSteps.Intake)
        .AddBlockDescriptor(ProveSteps.BeforeAfter)
        .AddBlockDescriptor(ProveSteps.Holdout)
        .AddBlockDescriptor(ProveSteps.HoldoutLog)
        .AddBlockDescriptor(ProveSteps.Evidence)
        .AddBlockDescriptor(ProveSteps.FailLog);

    /// <summary>Registers the review loop's Steps and the lens frame it shares (line spec DTC4), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddReviewBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(ReviewSteps.Round)
        .AddBlockDescriptor(LensSteps.Run)
        .AddBlockDescriptor(LensSteps.Ask)
        .AddBlockDescriptor(ReviewSteps.Log)
        .AddBlockDescriptor(ReviewSteps.Record)
        .AddBlockDescriptor(ReviewSteps.Fix)
        .AddBlockDescriptor(ReviewSteps.ApplyAnswer)
        .AddBlockDescriptor(ReviewSteps.Recheck);

    /// <summary>Registers the design and plan stations' Steps and the lens round's Steps they share (line spec LN-Y1), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddLineDesignBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(DesignSteps.Intake)
        .AddBlockDescriptor(DesignSteps.Draft)
        .AddBlockDescriptor(DesignSteps.Apply)
        .AddBlockDescriptor(DesignSteps.Check)
        .AddBlockDescriptor(DesignSteps.Revise)
        .AddBlockDescriptor(DesignSteps.LensDesk)
        .AddBlockDescriptor(DesignSteps.Evidence)
        .AddBlockDescriptor(LensRoundSteps.Round)
        .AddBlockDescriptor(LensRoundSteps.Log)
        .AddBlockDescriptor(PlanSteps.Intake)
        .AddBlockDescriptor(PlanSteps.Graph)
        .AddBlockDescriptor(PlanSteps.Check)
        .AddBlockDescriptor(PlanSteps.ExecReview)
        .AddBlockDescriptor(PlanSteps.Holdout)
        .AddBlockDescriptor(PlanSteps.Evidence)
        .AddBlockDescriptor(PlanSteps.Questions)
        .AddBlockDescriptor(PlanSteps.Settle)
        .AddBlockDescriptor(PlanSteps.Review)
        .AddBlockDescriptor(PlanSteps.Freeze);

    /// <summary>Registers the scoping station's Steps and the context Step inputs it names (scoping spec §9), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddScopingBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(ScopingSteps.Intake)
        .AddBlockDescriptor(ScopingSteps.ReadBrief)
        .AddBlockDescriptor(ScopingSteps.ReadTracker)
        .AddBlockDescriptor(ScopingSteps.ReadChange)
        .AddBlockDescriptor(ScopingSteps.BriefSeed)
        .AddBlockDescriptor(ScopingSteps.BriefQuestions)
        .AddBlockDescriptor(ScopingSteps.BriefWrite)
        .AddBlockDescriptor(ScopingSteps.Anchor)
        .AddBlockDescriptor(ScopingSteps.Gather)
        .AddBlockDescriptor(ScopingSteps.Draft)
        .AddBlockDescriptor(ScopingSteps.Check)
        .AddBlockDescriptor(ScopingSteps.InterviewQuestions)
        .AddBlockDescriptor(ScopingSteps.InterviewAnswer)
        .AddBlockDescriptor(ScopingSteps.InterviewRecord)
        .AddBlockDescriptor(ScopingSteps.InterviewApply)
        .AddBlockDescriptor(ScopingSteps.Reanchor)
        .AddBlockDescriptor(ScopingSteps.Challenge)
        .AddBlockDescriptor(ScopingSteps.ChallengeLog)
        .AddBlockDescriptor(ScopingSteps.Propose)
        .AddBlockDescriptor(ScopingSteps.Evidence)
        .AddBlockDescriptor(ScopingSteps.Review)
        .AddBlockDescriptor(ScopingSteps.Freeze)
        .AddBlockDescriptor(ScopingSteps.Withdraw);

    /// <summary>Registers the line stations' Steps (ship, docs sync), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddShipDocsBlocks(this IServiceCollection services) => services.AddShipBlocks().AddDocsBlocks();

    private static IServiceCollection AddShipBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(ShipSteps.Green)
        .AddBlockDescriptor(ShipSteps.Pack)
        .AddBlockDescriptor(ShipSteps.Freeze)
        .AddBlockDescriptor(ShipSteps.Open)
        .AddBlockDescriptor(ShipSteps.Return);

    /// <summary>Registers the docs-sync station's Steps (PB spec §7), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddDocsBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(DocsSteps.Scope)
        .AddBlockDescriptor(DocsSteps.Update)
        .AddBlockDescriptor(DocsSteps.Check)
        .AddPrBlocks();

    /// <summary>Registers the PR babysitter's Steps, gates, effects and Step inputs, and the re-proof child's Steps (PB spec §10.1), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddPrBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(PrSteps.Intake)
        .AddBlockDescriptor(PrSteps.Snapshot)
        .AddBlockDescriptor(PrSteps.ReadyCheck)
        .AddBlockDescriptor(PrSteps.NextComment)
        .AddBlockDescriptor(PrSteps.HeldComment)
        .AddBlockDescriptor(PrSteps.CiFix)
        .AddBlockDescriptor(PrSteps.FixComment)
        .AddBlockDescriptor(PrSteps.ReplyDraft)
        .AddBlockDescriptor(PrSteps.OwnerComment)
        .AddBlockDescriptor(PrSteps.ReworkNotes)
        .AddBlockDescriptor(PrSteps.DocsDecide)
        .AddBlockDescriptor(PrSteps.DocsMergeDecide)
        .AddBlockDescriptor(RecheckSteps.Plan)
        .AddBlockDescriptor(RecheckSteps.RerunScripts)
        .AddBlockDescriptor(RecheckSteps.Log)
        .AddBlockDescriptor(PrGates.RevisionBound)
        .AddBlockDescriptor(PrGates.BaseIsDefault)
        .AddBlockDescriptor(PrGates.AutofixCap)
        .AddGate<PrDiffScopeGate>()
        .AddGate<PrSecretScanGate>()
        .AddBlockDescriptor(PrEffects.Push)
        .AddBlockDescriptor(PrEffects.Reply)
        .AddBlockDescriptor(PrEffects.Record)
        .AddBlockDescriptor(PrEffects.RerunChecks)
        .AddBlockDescriptor(PrEffects.Restack)
        .AddBlockDescriptor(PrEffects.CreateStacked)
        .AddBlockDescriptor(PrEffects.Merge)
        .AddBlockDescriptor(PrEffects.MergeDocs)
        .AddBlockDescriptor(PrInputs.Context)
        .AddStepInput<PhasePacketInput>()
        .AddStepInput<HandoffNoteInput>();
}
