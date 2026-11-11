using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.StartToolkit;

/// <summary>Starts a built toolkit. An unbuilt toolkit, or one whose source changed since its last good build, is SND-TK-001.</summary>
/// <param name="Name">The toolkit name.</param>
public sealed record StartToolkitRequest(string Name) : ICommand<StartToolkitResponse>;
