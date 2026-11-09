using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILaneMail over the in-memory message store: the one fake, kept honest by LaneMailContract.</summary>
/// <param name="messages">Where a reply lands as an inbound message.</param>
public sealed class InMemoryLaneMail(InMemoryMessageStore messages) : ILaneMail
{
    private readonly List<(LaneMessage Message, bool Settled)> _sent = [];
    private readonly Dictionary<string, int> _replies = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<long>> Send(string lane, string text, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var seq = _sent.Count + 1L;
        _sent.Add((new LaneMessage(seq, lane, text, at, Delivered: false), Settled: false));
        return Done.Of(seq);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LaneMessage>>> Owed(string lane, CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<LaneMessage>>([.. _sent.Where(s => !s.Settled && s.Message.Lane == lane).Select(s => s.Message)]);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Deliver(IReadOnlyList<long> seqs, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seqs);
        for (var i = 0; i < _sent.Count; i++)
        {
            if (seqs.Contains(_sent[i].Message.Seq))
            {
                _sent[i] = (_sent[i].Message with { Delivered = true }, _sent[i].Settled);
            }
        }

        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Settle(IReadOnlyList<long> seqs, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seqs);
        for (var i = 0; i < _sent.Count; i++)
        {
            if (seqs.Contains(_sent[i].Message.Seq))
            {
                _sent[i] = (_sent[i].Message with { Delivered = true }, Settled: true);
            }
        }

        return Done.Unit();
    }

    /// <inheritdoc />
    public async ValueTask<Result<MessageId>> Reply(LaneIdentity identity, string text, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var n = _replies.GetValueOrDefault(identity.Lane) + 1;
        _replies[identity.Lane] = n;
        var id = MessageId.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"lane:{identity.Lane}:{n}"));
        var message = new InboxMessage(id, identity.SessionName, identity.SessionName, text, at, MessageClassifier.Classify(text));
        var stored = await messages.RecordInbound("lane:" + identity.Lane, 0, [message], cancellationToken);
        for (var i = 0; i < _sent.Count; i++)
        {
            if (_sent[i].Message.Lane == identity.Lane && _sent[i].Message.Delivered)
            {
                _sent[i] = (_sent[i].Message, Settled: true);
            }
        }

        return stored.IsError ? stored.Error : id;
    }
}
