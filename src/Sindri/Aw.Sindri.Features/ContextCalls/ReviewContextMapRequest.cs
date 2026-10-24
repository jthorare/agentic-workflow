using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One review action on the draft map (context spec §8.7), a classified command whose variant is the verb (README X63): accept,
/// reject, rename, move, accept-deterministic, undo, drop, repoint, apply-file, merge, add and delete each write reversible override rows
/// (with Undo); a human or a session may send any of them, with no typed challenge (removed per owner q93, 2026-10-23).
/// </summary>
/// <param name="Verb">The variant: 'accept', 'reject', 'rename', 'move', 'accept-deterministic', 'undo', 'drop', 'repoint', 'apply-file', 'merge', 'add' or 'delete'.</param>
/// <param name="Node">The node acted on (accept, reject, rename, move, merge, add, delete; the new target of repoint), or null.</param>
/// <param name="Link">The suggested file link decided (accept, reject), or null.</param>
/// <param name="Override">The override undone, dropped or repointed ('ovr_…'), or null.</param>
/// <param name="Label">The new label (rename, add), or null.</param>
/// <param name="Other">The other node: the new parent of a move, the node merged into, the parent of an add, or null.</param>
/// <param name="Path">The review file (apply-file), or null.</param>
/// <param name="Overrides">Several overrides undone together (undo only; 1 to 1000 ids, all or none), or null; not together with <paramref name="Override"/>.</param>
public sealed record ReviewContextMapRequest(string Verb, string? Node = null, string? Link = null, string? Override = null, string? Label = null,
    string? Other = null, string? Path = null, IReadOnlyList<string>? Overrides = null) : ICommand<ReviewContextMapResponse>, IClassifiedCommand
{
    /// <inheritdoc/>
    [JsonIgnore]
    public string Variant => Verb;

    /// <inheritdoc/>
    [JsonIgnore]
    public bool CarriesProtectedFields => false;
}
