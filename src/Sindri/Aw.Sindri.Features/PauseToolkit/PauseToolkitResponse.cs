using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.PauseToolkit;

/// <summary>The toolkit after a pause.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="State">Where it stands now.</param>
internal sealed record PauseToolkitResponse(string Name, ToolkitState State);
