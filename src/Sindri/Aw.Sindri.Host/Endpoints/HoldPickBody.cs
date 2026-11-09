using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.PickTodoHold;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/note/hold/apply and /restore: the note hash the row was drawn from.</summary>
/// <param name="NoteHash">The hash ('sha256:...').</param>
internal sealed record HoldPickBody(string? NoteHash = null)
{
    /// <summary>The request.</summary>
    /// <param name="restore">True for Put them back, false for Apply.</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request, or SND-CLI-003 when the hash is missing.</returns>
    internal Result<PickTodoHoldRequest> ToRequest(bool restore, bool piped) =>
        string.IsNullOrWhiteSpace(NoteHash) ? SindriErrors.InvalidInput.With("noteHash is the hash the row was drawn from (fix: {\"noteHash\":\"sha256:...\"})") : new PickTodoHoldRequest(restore, NoteHash, piped);
}
