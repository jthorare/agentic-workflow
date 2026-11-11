using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.PauseToolkit;

/// <summary>Pauses a running toolkit: its process stays up, its tools leave tools/list and calls are refused with SND-TK-005.</summary>
/// <param name="Name">The toolkit name.</param>
internal sealed record PauseToolkitRequest(string Name) : ICommand<PauseToolkitResponse>;
