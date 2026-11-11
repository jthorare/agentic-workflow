using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ResumeToolkit;

/// <summary>The toolkit after a resume.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="State">Where it stands now.</param>
internal sealed record ResumeToolkitResponse(string Name, ToolkitState State);
