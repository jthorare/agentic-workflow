using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IMessageStore: the one fake, kept honest by MessageStoreContract.</summary>
public sealed class InMemoryMessageStore : IMessageStore
{
    private readonly Dictionary<string, long> _offsets = new(StringComparer.Ordinal);
    private readonly List<InboxMessage> _messages = [];
    private readonly List<Outbound> _sent = [];
    private MetaRole? _role;

    /// <summary>Whether <see cref="Resolve"/> fails (a store that cannot write).</summary>
    public bool FailResolves { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<long>> Offset(string transcript, CancellationToken cancellationToken) =>
        Done.Of(_offsets.GetValueOrDefault(transcript));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<MessageId>>> RecordInbound(string transcript, long offset, IReadOnlyList<InboxMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var added = new List<MessageId>();
        foreach (var message in messages.Where(m => !_messages.Exists(stored => stored.Id == m.Id)))
        {
            _messages.Add(message);
            added.Add(message.Id);
        }

        _offsets[transcript] = offset;
        return Done.Of<IReadOnlyList<MessageId>>(added);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<InboxMessage>>> Messages(CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<InboxMessage>>([.. _messages.OrderBy(m => m.At)]);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Resolve(MessageId id, MessageResolution resolution, CancellationToken cancellationToken)
    {
        if (FailResolves)
        {
            return Done.Failed<Unit>(MetaErrors.MessageNotFound.With("the message store cannot be written"));
        }

        var at = _messages.FindIndex(m => m.Id == id);
        if (at < 0)
        {
            return Done.Failed<Unit>(MetaErrors.MessageNotFound.With($"no message {id.Value}"));
        }

        _messages[at] = _messages[at] with { Resolution = resolution };
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkShown(IReadOnlyList<MessageId> ids, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var id in ids)
        {
            var index = _messages.FindIndex(m => m.Id == id);
            if (index >= 0)
            {
                _messages[index] = _messages[index] with { ShownAt = at };
            }
        }

        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordOutbound(Outbound sent, CancellationToken cancellationToken)
    {
        _sent.Add(sent);
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Outbound>>> Sent(CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<Outbound>>([.. _sent]);

    /// <inheritdoc />
    public ValueTask<Result<MetaRole?>> Role(CancellationToken cancellationToken) => Done.Of(_role);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> SetRole(MetaRole role, CancellationToken cancellationToken)
    {
        _role = role;
        return Done.Unit();
    }
}
