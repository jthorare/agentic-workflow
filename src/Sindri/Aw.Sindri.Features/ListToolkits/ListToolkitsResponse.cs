using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkits;

/// <summary>The toolkits, sorted by name.</summary>
/// <param name="Toolkits">The toolkits.</param>
public sealed record ListToolkitsResponse(IReadOnlyList<Toolkit> Toolkits);
