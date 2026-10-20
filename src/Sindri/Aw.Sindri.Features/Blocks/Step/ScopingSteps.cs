using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The scoping station's Steps (scoping spec §9): descriptors only, desk-capable. A block Step is the deterministic one
/// the block runtime runs (SC-R), an agent Step runs in a launched session, and the two desk Steps are human-only. An agent
/// Step declares <see cref="Capability.Tools"/>; the read-only rule is W9's, enforced at launch (L-17), and is not a descriptor field yet. Fact and outcome names are camelCase.
/// </summary>
internal static class ScopingSteps
{
    private static readonly string[] _fourReads = ["brief", "tracker", "idea", "change"];

    /// <summary>'step.scope.intake': what to scope, sorted into one of four readers; every outcome carries the eight round-1 seeds.</summary>
    public static BlockDescriptor Intake { get; } = BlockStep("step.scope.intake", Capability.None)
        .Reads("fact:scopeInput").Reports(_fourReads)
        .Writes(Seeds());

    /// <summary>'step.scope.read-brief': reads the brief file (or its named section); 'read' with the brief and its anchors, or 'empty'.</summary>
    public static BlockDescriptor ReadBrief { get; } = BlockStep("step.scope.read-brief", Capability.Tools)
        .Reads("fact:scopeInput").Reports("read", "empty").Writes(On("fact:brief", "read"), On("fact:anchors", "read"));

    /// <summary>'step.scope.read-tracker': reads the tracker project or issue through the tracker port; 'read' or 'empty'.</summary>
    public static BlockDescriptor ReadTracker { get; } = BlockStep("step.scope.read-tracker", Capability.Tools | Capability.Network)
        .Reads("fact:scopeInput").Reports("read", "empty").Writes(On("fact:brief", "read"), On("fact:anchors", "read"));

    /// <summary>'step.scope.read-change': reads the change's diff, commits and PR body; 'read' or 'empty'.</summary>
    public static BlockDescriptor ReadChange { get; } = BlockStep("step.scope.read-change", Capability.Tools)
        .Reads("fact:scopeInput").Reports("read", "empty").Writes(On("fact:brief", "read"), On("fact:anchors", "read"));

    /// <summary>'step.brief.seed' (idea-to-brief): the empty round-1 answers and log, so the log is an output and never also an input.</summary>
    public static BlockDescriptor BriefSeed { get; } = BlockStep("step.brief.seed", Capability.None)
        .Reports("seeded").Writes(On("fact:answers", "seeded"), On("fact:answerLog", "seeded"));

    /// <summary>'step.brief.questions': what the idea leaves open; 'asked' with up to five questions, or 'ready'.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor BriefQuestions { get; } = AgentStep("step.brief.questions", Capability.Tools)
        .Reads("fact:idea", "fact:answers", "fact:answerLog").Reports("asked", "ready")
        .Writes(On("fact:questions", "asked"), On("fact:interviewClosed", "ready"));

    /// <summary>'step.brief.write': the cited brief from the idea and the answers; 'written' or 'empty'.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor BriefWrite { get; } = AgentStep("step.brief.write", Capability.Tools)
        .Reads("fact:idea", "fact:answers", "fact:answerLog").Reports("written", "empty").Writes(On("fact:brief", "written"));

    /// <summary>'step.scope.anchor': the brief's anchors from the context engine's text shape; 'anchored'.</summary>
    public static BlockDescriptor Anchor { get; } = BlockStep("step.scope.anchor", Capability.Tools)
        .Reads("fact:brief").Reports("anchored").Writes(On("fact:anchors", "anchored"));

    /// <summary>'step.scope.gather': the four lenses over the anchors, assembled from its Step inputs; 'gathered'.</summary>
    public static BlockDescriptor Gather { get; } = BlockStep("step.scope.gather", Capability.None)
        .Reads("fact:anchors").Reports("gathered").Writes(On("fact:lenses", "gathered"), On("fact:coverage", "gathered"));

    /// <summary>'step.scope.draft': the scope map and the acceptance list from the lenses; 'drafted'.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Draft { get; } = AgentStep("step.scope.draft", Capability.Tools)
        .Reads("fact:brief", "fact:lenses", "fact:coverage", "fact:scopeMap", "fact:acceptance", "fact:findings", "fact:answerLog", "fact:challenge")
        .Reports("drafted").Writes(On("fact:scopeMap", "drafted"), On("fact:acceptance", "drafted"));

    /// <summary>'step.scope.check': the draft-stage checks; 'pass', or findings on 'gaps' (the drafter can fix) and 'regather' (a lens must run again).</summary>
    public static BlockDescriptor Check { get; } = BlockStep("step.scope.check", Capability.Tools)
        .Reads("fact:brief", "fact:lenses", "fact:coverage", "fact:scopeMap", "fact:acceptance", "fact:answers", "fact:answerLog")
        .Reports("pass", "gaps", "regather").Writes(On("fact:findings", "gaps", "regather"));

    /// <summary>'step.interview.questions': the behaviour gaps as up to five questions; 'asked' or 'none'.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor InterviewQuestions { get; } = AgentStep("step.interview.questions", Capability.Tools)
        .Reads("fact:scopeMap", "fact:acceptance", "fact:lenses", "fact:answerLog", "fact:challenge", "fact:openQuestions")
        .Reports("asked", "none").Writes(On("fact:questions", "asked"), On("fact:openQuestions", "asked", "none"));

    /// <summary>'step.interview.answer': the owner's answers to the questions (the form is built from 'fact:questions'); human-only. 'doneAsking' also closes the interview.</summary>
    public static BlockDescriptor InterviewAnswer { get; } = HumanDeskStep("step.interview.answer", Capability.None)
        .Reads("fact:questions", "fact:answerLog").Reports("answered", "doneAsking")
        .Writes(On("fact:answers", "answered", "doneAsking"), On("fact:interviewClosed", "doneAsking"));

    /// <summary>'step.interview.record': appends the latest batch to the answer log once, by its digest; 'recorded'.</summary>
    public static BlockDescriptor InterviewRecord { get; } = BlockStep("step.interview.record", Capability.None)
        .Reads("fact:answers", "fact:answerLog").Reports("recorded").Writes(On("fact:answerLog", "recorded"));

    /// <summary>'step.interview.apply': folds the answers into the map, list and open questions; 'updated', or 'rescope' when an answer changes the surfaces.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor InterviewApply { get; } = AgentStep("step.interview.apply", Capability.Tools)
        .Reads("fact:brief", "fact:lenses", "fact:coverage", "fact:questions", "fact:answers", "fact:answerLog", "fact:scopeMap", "fact:acceptance", "fact:openQuestions", "fact:anchors")
        .Reports("updated", "rescope")
        .Writes(On("fact:scopeMap", "updated", "rescope"), On("fact:acceptance", "updated", "rescope"), On("fact:openQuestions", "updated", "rescope"));

    /// <summary>'step.scope.reanchor': anchors the changed map from the context engine's text shape; 'anchored'.</summary>
    public static BlockDescriptor Reanchor { get; } = BlockStep("step.scope.reanchor", Capability.Tools)
        .Reads("fact:scopeMap", "fact:anchors", "fact:lenses").Reports("anchored").Writes(On("fact:anchors", "anchored"));

    /// <summary>'step.scope.challenge': a second model's look for missed surfaces and behaviours; reports only its own round.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Challenge { get; } = AgentStep("step.scope.challenge", Capability.Tools)
        .Reads("fact:brief", "fact:lenses", "fact:coverage", "fact:scopeMap", "fact:acceptance", "fact:answerLog", "fact:challenge")
        .Reports("nothingNew", "found").Writes(Every("fact:challengeRound"));

    /// <summary>'step.scope.challenge-log': appends the round to the challenge record and re-reports its verdict; a verdict that disagrees with its items is a failed Step.</summary>
    public static BlockDescriptor ChallengeLog { get; } = BlockStep("step.scope.challenge-log", Capability.None)
        .Reads("fact:challenge", "fact:challengeRound").Reports("nothingNew", "found")
        .Writes(Every("fact:challenge"), On("fact:findings", "found"));

    /// <summary>'step.scope.propose': submits the map's new surfaces as draft proposals and records each surface's graph status; 'proposed' or 'skipped'.</summary>
    public static BlockDescriptor Propose { get; } = BlockStep("step.scope.propose", Capability.Tools)
        .Reads("fact:scopeMap").Reports("proposed", "skipped").Writes(Every("fact:scopeMap"));

    /// <summary>'step.scope.evidence': every check, the packet assembled in the run area; 'complete' re-reports five facts, 'incomplete' and 'regather' carry findings.</summary>
    public static BlockDescriptor Evidence { get; } = BlockStep("step.scope.evidence", Capability.Tools)
        .Reads("fact:brief", "fact:lenses", "fact:coverage", "fact:scopeMap", "fact:acceptance", "fact:answers", "fact:answerLog", "fact:challenge", "fact:openQuestions", "fact:decision", "fact:anchors", "fact:questions")
        .Reports("complete", "incomplete", "regather")
        .Writes(On("fact:scopeMap", "complete"), On("fact:acceptance", "complete"), On("fact:coverage", "complete"), On("fact:openQuestions", "complete"), On("fact:scopePacket", "complete"), On("fact:findings", "incomplete", "regather"));

    /// <summary>'step.scope.review': the owner's stop, human-only: approve, revise (notes become answers) or drop.</summary>
    public static BlockDescriptor Review { get; } = HumanDeskStep("step.scope.review", Capability.None)
        .Reads("fact:scopePacket").Reports("approve", "revise", "drop").Writes(On("fact:answers", "revise"));

    /// <summary>'step.scope.freeze': re-runs the evidence checks, confirms the packet's hash and freezes it through aw-packets; 'frozen' re-records fact:scopePacket as {hash, dir}, 'mismatch' carries findings.</summary>
    public static BlockDescriptor Freeze { get; } = BlockStep("step.scope.freeze", Capability.Tools)
        .Reads("fact:scopePacket", "fact:brief", "fact:lenses", "fact:coverage", "fact:scopeMap", "fact:acceptance", "fact:answers", "fact:answerLog", "fact:challenge", "fact:openQuestions", "fact:decision", "fact:anchors", "fact:questions")
        .Reports("frozen", "mismatch").Writes(On("fact:scopePacket", "frozen"), On("fact:findings", "mismatch"));

    /// <summary>'step.scope.withdraw': withdraws the run's undecided map proposals; 'withdrawn'.</summary>
    public static BlockDescriptor Withdraw { get; } = BlockStep("step.scope.withdraw", Capability.Tools).Reports("withdrawn").Writes();

    private static OutputSpec[] Seeds() =>
    [
        On("fact:scopeMap", _fourReads), On("fact:acceptance", _fourReads), On("fact:findings", _fourReads), On("fact:answers", _fourReads),
        On("fact:answerLog", _fourReads), On("fact:challenge", _fourReads), On("fact:questions", _fourReads), On("fact:openQuestions", _fourReads),
    ];
}
