using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StartSkillRun;

/// <summary>The trial that started.</summary>
/// <param name="Instance">The instance the trial runs as.</param>
/// <param name="Skill">The block's skill (its folder name).</param>
/// <param name="ManifestHash">The canonical hash of the manifest entry the run uses, which the CLI prints first.</param>
/// <param name="Outputs">The fact keys the block writes, so a followed run knows which facts to print.</param>
/// <param name="Success">The block's success outcome, which decides the CLI's exit code.</param>
internal sealed record StartSkillRunResponse(InstanceView Instance, string Skill, string ManifestHash, IReadOnlyList<string> Outputs, string Success);
