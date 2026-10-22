using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// The review-comment queue the snapshot writes into 'fact:prState' (babysitter spec §5.2), and the only place that reads its forge-shaped keys. Each entry of
/// 'prState.comments' is a forge thread as camelCase JSON: key, kind (name or number), author, association, isBot, resolved, outdated, path, body, markers;
/// plus the optional line, editedAt, url, thread (the earlier comments) and mark ({ outcome, reason, editedAt, at }, the latest row of the mark table, §5.4).
/// 'prState.self' (Sindri's forge login), 'prState.workspaceAhead' (workspace commits ahead of the PR head) and 'prState.walkStartedAt' are optional.
/// </summary>
internal sealed class PrCommentQueue
{
    private const int TextCap = 8 * 1024;
    private static readonly string[] _trusted = ["OWNER", "MEMBER", "COLLABORATOR"];
    private static readonly string[] _kinds = ["thread", "review", "comment"];

    private readonly List<JsonObject> _entries;
    private readonly string? _self;
    private readonly DateTimeOffset? _walkStart;

    private PrCommentQueue(List<JsonObject> entries, string? self, bool workspaceAhead, DateTimeOffset? walkStart)
    {
        _entries = entries;
        _self = self;
        WorkspaceAhead = workspaceAhead;
        _walkStart = walkStart;
    }

    /// <summary>Whether the workspace holds commits the PR head lacks, so a drained queue still needs a push.</summary>
    public bool WorkspaceAhead { get; }

    /// <summary>Reads the queue out of 'fact:prState'; null when the fact is not an object. No 'comments' key is an empty queue.</summary>
    /// <param name="prState">The fact.</param>
    /// <returns>The queue.</returns>
    public static PrCommentQueue? Parse(JsonNode? prState)
    {
        if (prState is not JsonObject state)
        {
            return null;
        }

        var entries = (state["comments"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var walk = state["walkStartedAt"] is JsonValue w && w.TryGetValue<string>(out var text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : (DateTimeOffset?)null;
        return new PrCommentQueue(entries, Str(state["self"]), state["workspaceAhead"] is JsonValue a && a.TryGetValue<bool>(out var ahead) && ahead, walk);
    }

    /// <summary>
    /// The queue with the current marks laid over the snapshot's: a comment handled since the snapshot (the mark table is newer than 'fact:prState') carries its mark here, so it isn't taken again.
    /// For an edited comment only a mark of that edit counts; otherwise the latest mark by time.
    /// </summary>
    /// <param name="prState">The fact.</param>
    /// <param name="marks">The pull request's latest marks (one per comment and edit).</param>
    /// <returns>The queue; null when the fact is not an object.</returns>
    public static PrCommentQueue? Parse(JsonNode? prState, IReadOnlyList<PrCommentMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        var queue = Parse(prState);
        if (queue is null)
        {
            return null;
        }

        foreach (var entry in queue._entries)
        {
            var edited = Str(entry["editedAt"]);
            var current = marks
                .Where(m => string.Equals(m.CommentId.Value, Str(entry["key"]), StringComparison.Ordinal) && (edited is null || string.Equals(m.EditedAt, edited, StringComparison.Ordinal)))
                .OrderBy(static m => m.At)
                .LastOrDefault();
            if (current is not null)
            {
                entry["mark"] = new JsonObject
                {
                    ["outcome"] = current.Outcome,
                    ["reason"] = current.Reason,
                    ["commit"] = current.Commit,
                    ["editedAt"] = current.EditedAt,
                    ["at"] = current.At.ToString("O", CultureInfo.InvariantCulture),
                };
            }
        }

        return queue;
    }

    /// <summary>The marks of the pull request the Step is about ('fact:pr', else the snapshot's 'url' or 'link', else the 'pr' argument); none when no link is held.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="marks">The comment marks.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The marks, or the error.</returns>
    public static async ValueTask<Result<IReadOnlyList<PrCommentMark>>> CurrentMarks(StepContext context, IPrCommentMarks marks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(marks);
        return Ref(context) is { } pr ? await marks.Latest(pr, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<PrCommentMark>>([]);
    }

    private static PrRef? Ref(StepContext context)
    {
        var fact = StepFacts.Read(context, "fact:pr");
        var state = StepFacts.Read(context, "fact:prState") as JsonObject;
        var link = fact switch
        {
            JsonValue value => Str(value),
            JsonObject obj => Str(obj["url"]) ?? Str(obj["link"]),
            _ => null,
        } ?? Str(state?["url"]) ?? Str(state?["link"]) ?? (context.Arguments.TryGetValue("pr", out var argument) && argument.ValueKind == System.Text.Json.JsonValueKind.String ? argument.GetString() : null);
        if (!Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 && parts[^2] == "pull" && int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? new PrRef(uri.Host, $"{parts[^4]}/{parts[^3]}", number)
            : null;
    }

    /// <summary>The 'bots' argument (an array of logins, or one login) as a set.</summary>
    /// <param name="bots">The argument's JSON.</param>
    /// <returns>The allowlisted bot logins.</returns>
    public static HashSet<string> Bots(JsonNode? bots)
    {
        var logins = bots switch
        {
            JsonArray array => array.Select(Str).OfType<string>(),
            JsonValue => Str(bots) is { } one ? [one] : [],
            _ => [],
        };
        return new HashSet<string>(logins, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The oldest comment nobody has handled: not resolved, not Sindri's own, and without a current mark.</summary>
    /// <returns>The entry, or null when the queue is empty.</returns>
    public JsonObject? NextUnhandled() => _entries.Find(e => !Resolved(e) && !Own(e) && Mark(e) is null);

    /// <summary>The oldest comment held for the owner, before the walk started when it records a start.</summary>
    /// <returns>The entry, or null when none is held.</returns>
    public JsonObject? NextHeld() => _entries.Find(e => !Resolved(e) && !Own(e) && Mark(e) is { } mark && string.Equals(Str(mark["outcome"]), "held", StringComparison.Ordinal) && BeforeWalk(mark));

    /// <summary>Authorship first (§5.2): 'untrusted', then 'bot', else 'comment'. A bot outside the allowlist is untrusted.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="bots">The allowlisted bot logins.</param>
    /// <returns>The outcome name.</returns>
    public static string Classify(JsonObject entry, HashSet<string> bots)
    {
        var login = Str(entry["author"]) ?? string.Empty;
        if (entry["isBot"] is JsonValue b && b.TryGetValue<bool>(out var isBot) && isBot)
        {
            return bots.Contains(login) ? "bot" : "untrusted";
        }

        return _trusted.Contains(Str(entry["association"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase) ? "comment" : "untrusted";
    }

    /// <summary>The entry as 'fact:comment' (§5.5): the body and the thread capped at 8 KiB each.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The fact.</returns>
    public static JsonObject ToFact(JsonObject entry)
    {
        var key = Str(entry["key"]) ?? string.Empty;
        var fact = new JsonObject
        {
            ["id"] = key,
            ["threadId"] = key,
            ["kind"] = Kind(entry["kind"]),
            ["author"] = new JsonObject { ["login"] = Str(entry["author"]), ["association"] = Str(entry["association"]), ["bot"] = string.Equals(Str(entry["isBot"]), "true", StringComparison.Ordinal) },
            ["body"] = Cap(Str(entry["body"]) ?? string.Empty),
            ["thread"] = Thread(entry["thread"]),
            ["editedAt"] = Str(entry["editedAt"]),
            ["url"] = Str(entry["url"]),
        };
        if (Str(entry["path"]) is { } path)
        {
            fact["path"] = path;
        }

        if (entry["line"] is JsonValue line && line.TryGetValue<int>(out var number))
        {
            fact["line"] = number;
        }

        return fact;
    }

    private static string Kind(JsonNode? kind) => Str(kind) is { } text
        ? (int.TryParse(text, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 3 ? _kinds[n - 1] : text.ToLowerInvariant())
        : "comment";

    private static JsonArray Thread(JsonNode? thread)
    {
        var kept = new JsonArray();
        var size = 0;
        foreach (var item in thread as JsonArray ?? [])
        {
            size += item?.ToJsonString().Length ?? 0;
            if (size > TextCap)
            {
                break;
            }

            kept.Add(item?.DeepClone());
        }

        return kept;
    }

    private static string Cap(string text) => text.Length <= TextCap ? text : text[..TextCap];

    private static bool Resolved(JsonObject entry) => entry["resolved"] is JsonValue v && v.TryGetValue<bool>(out var resolved) && resolved;

    // Sindri's replies never come back as work: its forge account, or a marker it wrote.
    private bool Own(JsonObject entry) =>
        (_self is not null && string.Equals(Str(entry["author"]), _self, StringComparison.OrdinalIgnoreCase))
        || (entry["markers"] as JsonArray ?? []).Select(Str).Any(static m => m is not null && m.StartsWith("sindri", StringComparison.Ordinal));

    // The mark that still counts: an edited comment is unhandled again (§5.4); a thread without editedAt (the forge shape today) matches any mark.
    private static JsonObject? Mark(JsonObject entry) =>
        entry["mark"] is JsonObject mark && (Str(entry["editedAt"]) is not { } edited || string.Equals(Str(mark["editedAt"]), edited, StringComparison.Ordinal)) ? mark : null;

    // A comment re-held after the walk started is left for the next walk, so the walk does not loop on it.
    private bool BeforeWalk(JsonObject mark) =>
        _walkStart is not { } start || Str(mark["at"]) is not { } text || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) || at <= start;

    private static string? Str(JsonNode? node) => node is JsonValue value ? (value.TryGetValue<string>(out var text) ? text : value.ToJsonString()) : null;
}
