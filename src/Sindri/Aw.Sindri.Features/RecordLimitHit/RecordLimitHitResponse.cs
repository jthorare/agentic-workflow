using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.RecordLimitHit;

/// <summary>The RecordLimitHit response.</summary>
/// <param name="Launch">The launch whose limit was hit.</param>
public sealed record RecordLimitHitResponse(LaunchId Launch);
