using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Ports;

/// <summary>The messages between the meta and the non-Claude lanes: sends waiting for a lane, and the lane's replies.</summary>
public interface ILaneMail
{
    /// <summary>Appends a message to a lane's inbox; it is owed a reply until the lane replies.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="text">The text.</param>
    /// <param name="at">When it was sent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The message number.</returns>
    public ValueTask<Result<long>> Send(string lane, string text, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>The messages the lane owes a reply to, oldest first (delivered or not).</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The owed messages.</returns>
    public ValueTask<Result<IReadOnlyList<LaneMessage>>> Owed(string lane, CancellationToken cancellationToken);

    /// <summary>Marks messages as shown to the lane.</summary>
    /// <param name="seqs">The message numbers.</param>
    /// <param name="at">When they were shown.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Deliver(IReadOnlyList<long> seqs, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Settles the given messages (notices, which owe no reply) without touching the lane's other mail; a message not yet shown is marked shown too.</summary>
    /// <param name="seqs">The message numbers.</param>
    /// <param name="at">When they were settled.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Settle(IReadOnlyList<long> seqs, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Records the lane's reply as an inbound message of the meta (id "lane:&lt;lane&gt;:&lt;n&gt;", from "&lt;team&gt;-&lt;lane&gt;", classified like any inbound message) and clears what the lane owed.</summary>
    /// <param name="identity">The lane.</param>
    /// <param name="text">The reply.</param>
    /// <param name="at">When it was made.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The recorded message id.</returns>
    public ValueTask<Result<MessageId>> Reply(LaneIdentity identity, string text, DateTimeOffset at, CancellationToken cancellationToken);
}
