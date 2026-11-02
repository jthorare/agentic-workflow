namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the ProjectsPull operation.</summary>
/// <param name="Projects">The Projects Linear serves.</param>
internal sealed record LinearProjectsPullData(LinearConnection<LinearProjectNode> Projects);
