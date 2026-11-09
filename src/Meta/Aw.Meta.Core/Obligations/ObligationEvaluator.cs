using System.Globalization;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Obligations;

/// <summary>Derives the open obligations from the recorded facts. Nothing is stored: sending a message closes the obligation it answers. A new lane map version and a merge owe the lanes nothing: they read both from the board (aw-meta board-line).</summary>
public static class ObligationEvaluator
{
    private const int BodyPreview = 80;

    /// <summary>Evaluates the open set.</summary>
    /// <param name="facts">The recorded facts.</param>
    /// <returns>The open obligations: replies in arrival order, then owner relay and watch.</returns>
    public static IReadOnlyList<Obligation> Evaluate(ObligationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var open = new List<Obligation>();
        open.AddRange(Replies(facts));
        open.AddRange(Relays(facts));
        open.AddRange(Rearm(facts));
        return open;
    }

    private static IEnumerable<Obligation> Replies(ObligationFacts facts) =>
        facts.Messages
            .Where(m => !IsSetAside(m, facts.Now) && !facts.Outbound.Any(o => o.At > m.At && Recipient.Matches(o.To, m.FromName, m.From)))
            .OrderBy(m => m.At)
            .Select(Reply);

    private static bool IsSetAside(InboxMessage message, DateTimeOffset now) => message.Resolution switch
    {
        { Kind: MessageResolutionKind.Dismissed } => true,
        { Kind: MessageResolutionKind.Deferred, Until: { } until } => until > now,
        _ => false,
    };

    private static Obligation Reply(InboxMessage message)
    {
        var who = message.FromName.Length > 0 ? message.FromName : message.From;
        var body = message.Body.Length > BodyPreview ? message.Body[..BodyPreview] + "..." : message.Body;
        var id = message.Id.Value;
        var send = LaneSendCommand(id) is { } lane ? $"aw-meta lane-send --lane {lane} --text \"<reply>\"" : $"SendMessage to {who}";
        return new Obligation(
            ObligationKind.Reply,
            id,
            $"reply to {who} ({id}): {body}",
            $"{send}; or aw-meta inbox-dismiss {id} --reason \"<why>\"; or aw-meta inbox-defer {id} --reason \"<why>\" --until +30m");
    }

    // A reply a non-Claude lane left through lane-reply has the id "lane:<lane>:<n>"; it is answered with lane-send, not SendMessage.
    private static string? LaneSendCommand(string id)
    {
        const string prefix = "lane:";
        var end = id.LastIndexOf(':');
        return id.StartsWith(prefix, StringComparison.Ordinal) && end > prefix.Length ? id[prefix.Length..end] : null;
    }

    private static IEnumerable<Obligation> Relays(ObligationFacts facts) =>
        facts.Answers
            .Where(a => !facts.Outbound.Any(o =>
                o.At >= a.AnsweredAt
                && Recipient.Matches(o.To, a.AskedBy)
                && Recipient.Contains(o.Message, string.Create(CultureInfo.InvariantCulture, $"OWNER ANSWER q{a.Question}"))))
            .Select(a => new Obligation(
                ObligationKind.RelayOwner,
                string.Create(CultureInfo.InvariantCulture, $"q{a.Question}"),
                string.Create(CultureInfo.InvariantCulture, $"owner answer q{a.Question} not relayed to {a.AskedBy}"),
                string.Create(CultureInfo.InvariantCulture, $"SendMessage \"OWNER ANSWER q{a.Question}: {a.Text}\" to {a.AskedBy}")));

    /// <summary>The rearm hint per provider (#2370): a Claude meta keeps a plain watch running under Monitor, because --until-mail exits on its first
    /// poll whenever mail waits; a Codex or Cursor meta runs --until-mail in the foreground and rearms after each exit.</summary>
    internal const string WatchCommand = "Claude: aw-meta watch --for 29m (with Monitor, persistent); Codex/Cursor: aw-meta watch --until-mail --for 29m in the foreground, rearm after each exit";

    private static IEnumerable<Obligation> Rearm(ObligationFacts facts)
    {
        if (facts.Heartbeat is { } beat && facts.Now - beat > facts.WatchInterval * 2)
        {
            yield return new Obligation(
                ObligationKind.RearmWatch,
                "watch",
                string.Create(CultureInfo.InvariantCulture, $"the watch last beat at {beat:HH:mm} UTC, over two intervals ago"),
                WatchCommand);
        }
        else if (facts.Heartbeat is null && Replies(facts).Count() is var waiting and > 0)
        {
            yield return new Obligation(
                ObligationKind.RearmWatch,
                "watch",
                string.Create(CultureInfo.InvariantCulture, $"no active coordinator consumer: watch last beat never, {waiting} waiting"),
                WatchCommand);
        }
    }
}
