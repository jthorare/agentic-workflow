using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.CreateToolkit;

/// <summary>The toolkit the generator made.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="Dir">The toolkit's folder.</param>
/// <param name="State">Where it stands: off, until a build.</param>
internal sealed record CreateToolkitResult(string Name, string Dir, ToolkitState State);
