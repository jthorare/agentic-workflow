using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.AddConnection;

namespace Aw.Sindri.Features.RenameConnection;

/// <summary>
/// Changes a connection's label: 'slack:old' becomes 'slack:new', the service part stays. The keychain entry is keyed by the name, so the tokens are read and stored again
/// under the new name first, then the new row is inserted (a plain INSERT: a name taken meanwhile is refused) and the old row and its entry are removed. A step that fails before
/// the old row is gone undoes what it did, so the old connection keeps working. An unknown name is SND-CONN-001, a taken new name SND-CONN-010 naming that connection, a bad
/// label SND-CONN-008, a source or target name of slack:messages SND-CONN-017, the same label SND-CLI-003, and a connection an active workflow's connection_event trigger watches SND-CONN-015 naming those workflows (the
/// trigger holds the name as text, so after a rename it would watch nothing and the workflow would look active and never fire). A connection that needs a sign-in keeps its Needs-you row, moved to the new name. An error shows its code and fixed title
/// only (<see cref="ConnectionFailures"/>).
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Moves the tokens to the new name's entry.</param>
/// <param name="events">Moves the Needs-you row and holds the clock.</param>
/// <param name="workflows">The workflows, read for active connection_event triggers that name the connection.</param>
internal sealed class RenameConnectionService(IConnectionStore store, IConnectionSecrets secrets, ConnectionEvents events, IWorkflowStore workflows) : IService<RenameConnectionRequest, RenameConnectionResponse>
{
    private readonly IWorkflowStore _workflows = workflows;
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly ConnectionEvents _events = events;

    public async ValueTask<Result<RenameConnectionResponse>> Handle(RenameConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = await Plan(request, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var (from, to, found) = plan.Value;
        var watched = await Watched(from, cancellationToken).ConfigureAwait(false);
        if (watched.IsError)
        {
            return watched.Error;
        }

        var moved = Move(found, to);
        if (moved.IsError)
        {
            return moved.Error;
        }

        var swapped = await Swap(found, to, moved.Value, cancellationToken).ConfigureAwait(false);
        if (swapped.IsError)
        {
            return swapped.Error;
        }

        if (found.SecretPointer is { } old && moved.Value is not null)
        {
            _ = _secrets.Delete(old);   // why: the row already points at the new entry; a leftover old entry is only an orphan, so a failed delete does not undo the rename
        }

        if (found.Status == ConnectionStatus.NeedsSignIn)
        {
            await _events.SignedIn(from, cancellationToken).ConfigureAwait(false);   // why: the Needs-you row is keyed by name; the old one would point at nothing
            await _events.NeedsSignIn(to, found.StatusReason ?? "sign in again", grant: null, cancellationToken).ConfigureAwait(false);
        }

        return new RenameConnectionResponse(to.ToString(), from.ToString(), SecretMoved: moved.Value is not null);
    }

    // The two names and the stored row, or the refusal: a bad name, the same label, an unknown connection, a new name that is taken.
    private async ValueTask<Result<(ConnectionName From, ConnectionName To, Connection Found)>> Plan(RenameConnectionRequest request, CancellationToken cancellationToken)
    {
        var from = ReservedConnectionNames.Parse(request.Name);
        if (from.IsError)
        {
            return from.Error;
        }

        var to = ReservedConnectionNames.Parse(string.Concat(from.Value.Service, ":", request.Label));
        if (to.IsError)
        {
            return to.Error;
        }

        if (to.Value == from.Value)
        {
            return SindriErrors.InvalidInput.With($"{from.Value} already has that label (fix: give a different label)");
        }

        var found = await _store.Get(from.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return ConnectionFailures.Safe(found.Error);
        }

        var taken = await _store.Get(to.Value, cancellationToken).ConfigureAwait(false);
        if (taken.IsSuccess)
        {
            return SindriErrors.ConnectionExists.With($"a connection named {to.Value} already exists (fix: pick another label, or sindri connection remove {to.Value} first)");
        }

        if (taken.Error.Code != SindriErrors.ConnectionNotFound.Code)
        {
            return ConnectionFailures.Safe(taken.Error);
        }

        return (from.Value, to.Value, found.Value);
    }

    // Refuses when an active workflow's trigger names the connection: the trigger keeps the old text, so the rename would leave it watching nothing.
    private async ValueTask<Result<Unit>> Watched(ConnectionName name, CancellationToken cancellationToken)
    {
        var listed = await _workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var slugs = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var record in listed.Value.Where(static r => r.ActiveRevision is not null))
        {
            var revision = await _workflows.GetRevision(record.Id, record.ActiveRevision, cancellationToken).ConfigureAwait(false);
            if (revision.IsError)
            {
                return revision.Error;   // why: a store error must not read as "nothing watches it"
            }

            // why: the trigger type is the poller slice's TriggerMatch.Kind; slices are isolated, so the text is repeated here
            if (revision.Value.Spec.Nodes.Any(n => string.Equals(n.Type, "connection_event", StringComparison.Ordinal) && Names(n.Config, name)))
            {
                _ = slugs.Add(record.Slug.Value);
            }
        }

        return slugs.Count == 0
            ? Unit.Value
            : SindriErrors.ConnectionWatched.With($"{name} is watched by the active workflow(s) {string.Join(", ", slugs)} (fix: sindri wf pause each one, rename, point its trigger at the new name, then activate it again)");
    }

    // True when a trigger's config names the connection (the same text the poller parses).
    private static bool Names(JsonObject config, ConnectionName name)
    {
        if (config["connection"] is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return false;
        }

        var parsed = ConnectionName.Parse(text);
        return parsed.IsSuccess && parsed.Value == name;
    }

    // Inserts the row under the new name, then removes the old one; a failure undoes the new row and the new entry, so the old connection keeps working.
    private async ValueTask<Result<Unit>> Swap(Connection found, ConnectionName to, SecretPointer? pointer, CancellationToken cancellationToken)
    {
        var inserted = await _store.Insert(found with { Name = to, SecretPointer = pointer, UpdatedAt = _events.Now }, cancellationToken).ConfigureAwait(false);
        if (inserted.IsError)
        {
            Undo(pointer, found);
            return ConnectionFailures.Safe(inserted.Error);
        }

        var removed = await _store.Remove(found.Name, cancellationToken).ConfigureAwait(false);
        if (removed.IsError)
        {
            _ = await _store.Remove(to, cancellationToken).ConfigureAwait(false);
            Undo(pointer, found);
            return ConnectionFailures.Safe(removed.Error);
        }

        return Unit.Value;
    }

    // Stores the tokens under the new name and returns the new pointer, or null when the row has no secret yet.
    private Result<SecretPointer?> Move(Connection connection, ConnectionName to)
    {
        if (connection.SecretPointer is not { } pointer)
        {
            return Results.Success<SecretPointer?>(value: null);
        }

        var tokens = _secrets.Read(pointer);
        if (tokens.IsError)
        {
            return ConnectionFailures.Safe(tokens.Error);
        }

        var stored = _secrets.Store(to, tokens.Value);
        return stored.IsError ? ConnectionFailures.Safe(stored.Error) : stored.Value;
    }

    private void Undo(SecretPointer? created, Connection original)
    {
        if (created is not null && created != original.SecretPointer)
        {
            _ = _secrets.Delete(created);
        }
    }
}
