using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aw.Meta.Core.Events;

/// <summary>The events aw-meta writes, one factory per kind, so every kind and its fields are in one place (docs/csharp/meta.md "Events"). Each event is an edge, never a state.</summary>
public static class MetaEvents
{
    /// <summary>A lane's reply to a message became owed.</summary>
    /// <param name="at">When.</param>
    /// <param name="obligationKey">The obligation's id (the message id).</param>
    /// <param name="from">Who is waiting for the reply.</param>
    /// <param name="lane">The lane that is waiting.</param>
    /// <returns>The event.</returns>
    public static MetaEvent ObligationOpened(DateTimeOffset at, string obligationKey, string from, string lane) => Obligation("obligation.opened", at, obligationKey, from, lane);

    /// <summary>An owed reply was sent or dismissed.</summary>
    /// <param name="at">When.</param>
    /// <param name="obligationKey">The obligation's id (the message id).</param>
    /// <param name="from">Who was waiting for the reply.</param>
    /// <param name="lane">The lane that was waiting.</param>
    /// <returns>The event.</returns>
    public static MetaEvent ObligationClosed(DateTimeOffset at, string obligationKey, string from, string lane) => Obligation("obligation.closed", at, obligationKey, from, lane);

    /// <summary>A lane crossed its recycle mark.</summary>
    /// <param name="at">When.</param>
    /// <param name="lane">The lane.</param>
    /// <param name="tokens">Its context size in tokens.</param>
    /// <returns>The event.</returns>
    public static MetaEvent ContextHigh(DateTimeOffset at, string lane, long tokens) => Context("context.high", at, lane, tokens, recycle: true);

    /// <summary>A lane dropped back below its recycle mark.</summary>
    /// <param name="at">When.</param>
    /// <param name="lane">The lane.</param>
    /// <param name="tokens">Its context size in tokens.</param>
    /// <returns>The event.</returns>
    public static MetaEvent ContextOk(DateTimeOffset at, string lane, long tokens) => Context("context.ok", at, lane, tokens, recycle: false);

    /// <summary>A lane that had a live session lost it.</summary>
    /// <param name="at">When.</param>
    /// <param name="lane">The lane.</param>
    /// <param name="reason">Why, in a few words.</param>
    /// <returns>The event.</returns>
    public static MetaEvent LaneDown(DateTimeOffset at, string lane, string reason) => Lane("lane.down", at, lane, reason);

    /// <summary>A lane reported down has a live session again.</summary>
    /// <param name="at">When.</param>
    /// <param name="lane">The lane.</param>
    /// <param name="reason">Why, in a few words.</param>
    /// <returns>The event.</returns>
    public static MetaEvent LaneUp(DateTimeOffset at, string lane, string reason) => Lane("lane.up", at, lane, reason);

    /// <summary>A lane (or the meta) hit a usage limit.</summary>
    /// <param name="at">When.</param>
    /// <param name="lane">The lane, or "meta".</param>
    /// <param name="provider">The provider whose limit it is.</param>
    /// <param name="until">When the limit resets; null when the stop gave no time.</param>
    /// <returns>The event.</returns>
    public static MetaEvent UsageLimited(DateTimeOffset at, string lane, string provider, DateTimeOffset? until) => Usage("usage.limited", at, lane, provider, until);

    /// <summary>A limited lane (or the meta) can work again.</summary>
    /// <param name="at">When.</param>
    /// <param name="lane">The lane, or "meta".</param>
    /// <param name="provider">The provider whose limit it was.</param>
    /// <param name="until">When the limit said it resets; null when it gave no time.</param>
    /// <returns>The event.</returns>
    public static MetaEvent UsageOk(DateTimeOffset at, string lane, string provider, DateTimeOffset? until) => Usage("usage.ok", at, lane, provider, until);

    /// <summary>aw-meta merged a pull request.</summary>
    /// <param name="at">When.</param>
    /// <param name="pr">The pull request number.</param>
    /// <param name="sha">The merge commit, or "unknown".</param>
    /// <param name="lane">The lane that sent the ready notice, or an empty text when none is known.</param>
    /// <returns>The event.</returns>
    public static MetaEvent MergeRecorded(DateTimeOffset at, int pr, string sha, string lane) =>
        new(0, at, "merge.recorded", lane.Length == 0 ? null : lane, Fields(w =>
        {
            w.WriteNumber("pr", pr);
            w.WriteString("sha", sha);
            w.WriteString("lane", lane);
        }));

    private static MetaEvent Obligation(string kind, DateTimeOffset at, string obligationKey, string from, string lane) =>
        new(0, at, kind, lane, Fields(w =>
        {
            w.WriteString("id", obligationKey);
            w.WriteString("from", from);
            w.WriteString("lane", lane);
        }));

    private static MetaEvent Context(string kind, DateTimeOffset at, string lane, long tokens, bool recycle) =>
        new(0, at, kind, lane, Fields(w =>
        {
            w.WriteString("lane", lane);
            w.WriteNumber("tokens_k", tokens / 1000);
            w.WriteBoolean("recycle", recycle);
        }));

    private static MetaEvent Lane(string kind, DateTimeOffset at, string lane, string reason) =>
        new(0, at, kind, lane, Fields(w =>
        {
            w.WriteString("lane", lane);
            w.WriteString("reason", reason);
        }));

    private static MetaEvent Usage(string kind, DateTimeOffset at, string lane, string provider, DateTimeOffset? until) =>
        new(0, at, kind, lane, Fields(w =>
        {
            w.WriteString("lane", lane);
            w.WriteString("provider", provider);
            if (until is { } reset)
            {
                w.WriteString("until", reset.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
            }
        }));

    private static string Fields(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
