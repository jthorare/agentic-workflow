using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SkipSetupStep;

/// <summary>What the skip did.</summary>
/// <param name="Step">The step.</param>
/// <param name="Skipped">Whether the step is skipped now.</param>
/// <param name="Changed">False when the profile already held that answer and was left alone.</param>
internal sealed record SkipSetupStepResponse(SetupStepId Step, bool Skipped, bool Changed);
