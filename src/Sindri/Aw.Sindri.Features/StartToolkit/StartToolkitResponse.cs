using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StartToolkit;

/// <summary>The toolkit after a start.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="State">Where it stands now.</param>
public sealed record StartToolkitResponse(string Name, ToolkitState State);
