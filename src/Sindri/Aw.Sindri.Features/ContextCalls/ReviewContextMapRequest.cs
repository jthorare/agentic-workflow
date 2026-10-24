using System.Text.Json.Serialization;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One review action on the draft map (context spec §8.7), a classified command whose variant is the verb (README X63): accept,
/// reject, rename, move, accept-deterministic, undo, drop and repoint are desk actions (a reversible override row, with Undo);
/// apply-file, merge, add and delete are protected (the typed confirmation of README X30): without an answer they show what they
/// would do and issue the challenge, with one they redeem it and act.
/// </summary>
/// <param name="Verb">The variant: 'accept', 'reject', 'rename', 'move', 'accept-deterministic', 'undo', 'drop', 'repoint', 'apply-file', 'merge', 'add' or 'delete'.</param>
/// <param name="Node">The node acted on (accept, reject, rename, move, merge, add, delete; the new target of repoint), or null.</param>
/// <param name="Link">The suggested file link decided (accept, reject), or null.</param>
/// <param name="Override">The override undone, dropped or repointed ('ovr_…'), or null.</param>
/// <param name="Label">The new label (rename, add), or null.</param>
/// <param name="Other">The other node: the new parent of a move, the node merged into, the parent of an add, or null.</param>
/// <param name="Path">The review file (apply-file), or null.</param>
/// <param name="Answer">The challenge and the text the human typed (protected variants), or null.</param>
/// <param name="Overrides">Several overrides undone together (undo only; 1 to 10000 ids, all or none), or null; not together with <paramref name="Override"/>.</param>
public sealed record ReviewContextMapRequest(string Verb, string? Node = null, string? Link = null, string? Override = null, string? Label = null,
    string? Other = null, string? Path = null, ChallengeAnswer? Answer = null, IReadOnlyList<string>? Overrides = null) : ICommand<ReviewContextMapResponse>, IClassifiedCommand
{
    /// <inheritdoc/>
    [JsonIgnore]
    public string Variant => Verb;

    /// <inheritdoc/>
    [JsonIgnore]
    public bool CarriesProtectedFields => false;
}
