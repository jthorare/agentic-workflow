using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SessionExit;

/// <summary>The SessionExit response.</summary>
/// <param name="Launch">The launch whose session ended.</param>
public sealed record SessionExitResponse(LaunchId Launch);
