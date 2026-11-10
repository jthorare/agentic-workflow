using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StopToolkit;

/// <summary>The toolkit after a stop.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="State">Where it stands now.</param>
public sealed record StopToolkitResponse(string Name, ToolkitState State);
