using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The build station's Steps (line spec §7.2, Appendix C.3): descriptors only, desk-capable, so each parks with SND-WF-010 at the desk until
/// its runtime ships. 'step.build.red' and 'step.build.green' are agent Steps (the real ones need the builder model and the 'build'
/// capability, LN-WF6); the scope desk is human-only. Fact and outcome names are camelCase.
/// </summary>
internal static class BuildSteps
{
    /// <summary>'step.build.prepare': checks the phase worktree is clean at its recorded head and seeds the facts every later wait reads; 'ready'.</summary>
    public static BlockDescriptor Prepare { get; } = BlockStep("step.build.prepare", Capability.Tools)
        .Reads("fact:phasePacket", "fact:workspace", "fact:buildNotes", "fact:lineContext").Reports("ready")
        .Writes(On("fact:activePacket", "ready"), On("fact:redTests", "ready"), On("fact:built", "ready"), On("fact:change", "ready"), On("fact:findings", "ready"), On("fact:answers", "ready"));

    /// <summary>'step.build.red': one failing test per visible acceptance item, committed as 'test: …'; 'written' with the red tests.</summary>
    public static BlockDescriptor Red { get; } = AgentStep("step.build.red", Capability.Tools | Capability.WriteScope)
        .Reads("fact:activePacket", "fact:buildNotes", "fact:redTests", "fact:findings").Reports("written").Writes(On("fact:redTests", "written"));

    /// <summary>'step.build.red-check': runs the repo's commands on the red commit; 'red' when at least one fails, 'vacuous' (with a finding) when every one passes.</summary>
    public static BlockDescriptor RedCheck { get; } = BlockStep("step.build.red-check", Capability.Tools)
        .Reads("fact:redTests", "fact:activePacket").Reports("red", "vacuous").Long(CommandStepLimits.BuildMaxDuration).Writes(On("fact:findings", "vacuous"));

    /// <summary>'step.build.green': implements in atomic commits inside the allowed files; 'built', or 'blocked' with a request.</summary>
    public static BlockDescriptor Green { get; } = AgentStep("step.build.green", Capability.Tools | Capability.WriteScope)
        .Reads("fact:activePacket", "fact:redTests", "fact:buildNotes", "fact:findings", "fact:answers").Reports("built", "blocked")
        .Writes(On("fact:built", "built"), On("fact:findings", "blocked"));

    /// <summary>'step.build.checks': the scope gate, the build rules and every one of the repo's commands (all must pass); 'pass' with the built head and the change, 'failed' or 'outOfScope' with findings.</summary>
    public static BlockDescriptor Checks { get; } = BlockStep("step.build.checks", Capability.Tools)
        .Reads("fact:activePacket", "fact:built", "fact:workspace").Reports("pass", "failed", "outOfScope").Long(CommandStepLimits.BuildMaxDuration)
        .Writes(On("fact:built", "pass"), On("fact:change", "pass"), On("fact:findings", "failed", "outOfScope"));

    /// <summary>'step.build.scope-desk': the owner's call on a file outside the allowed ones, human-only: allow, revert or stop.</summary>
    public static BlockDescriptor ScopeDesk { get; } = HumanDeskStep("step.build.scope-desk", Capability.None)
        .Reads("fact:findings", "fact:activePacket").Reports("allow", "revert", "stop").Writes(On("fact:answers", "allow", "revert"));

    /// <summary>'step.build.amend': writes the owner-frozen child phase packet with the granted paths; 'amended' with it as the packet in force.</summary>
    public static BlockDescriptor Amend { get; } = BlockStep("step.build.amend", Capability.Tools)
        .Reads("fact:activePacket", "fact:answers").Reports("amended").Writes(On("fact:activePacket", "amended"));
}
