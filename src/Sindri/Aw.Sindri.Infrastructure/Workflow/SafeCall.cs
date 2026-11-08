namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>One call a data block makes.</summary>
/// <param name="Method">GET or POST.</param>
/// <param name="Uri">Where to.</param>
/// <param name="JsonBody">The JSON body of a POST, or null.</param>
internal sealed record SafeCall(HttpMethod Method, Uri Uri, string? JsonBody = null);
