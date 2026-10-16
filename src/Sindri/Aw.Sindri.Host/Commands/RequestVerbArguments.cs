using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the arguments of 'request', 'requests', 'decisions', 'decide' and 'needs-you' into one <see cref="Parsed"/>, or the usage error.</summary>
internal static class RequestVerbArguments
{
    internal const string Usage = "request <kind> [--item <item>] <text> [--json] [--no-start] | requests [--state <s>] [--item <item>] [--json] [--no-start] | decisions [--pending | --decided] [--item <item>] [--json] [--no-start] | decide <decisionId> <choice> [--note <note>] [--json] [--no-start] | needs-you [--json] [--no-start]";

    internal enum Verb
    {
        None,
        Request,
        Requests,
        Decisions,
        Decide,
        NeedsYou,
    }

    internal static Result<Parsed> Parse(string[] args)
    {
        var (verb, rest) = args switch
        {
            ["request", .. var r] => (Verb.Request, r),
            ["requests", .. var r] => (Verb.Requests, r),
            ["decisions", .. var r] => (Verb.Decisions, r),
            ["decide", .. var r] => (Verb.Decide, r),
            ["needs-you", .. var r] => (Verb.NeedsYou, r),
            _ => (Verb.None, args),
        };
        if (verb == Verb.None)
        {
            return SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a request command: {Usage}");
        }

        var parsed = new Parsed(verb);
        var words = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            var taken = Flag(parsed, rest, i, words);
            if (taken.IsError)
            {
                return taken.Error;
            }

            i += taken.Value;
        }

        var shape = Shape(parsed, words);
        return shape is null ? parsed : shape;
    }

    /// <summary>The wire name of <paramref name="kind"/> ('rescope', 'add-task', 'note', 'handback').</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    internal static string KindName(RequestKind kind) => kind switch
    {
        RequestKind.Rescope => "rescope",
        RequestKind.AddTask => "add-task",
        RequestKind.Note => "note",
        RequestKind.Handback => "handback",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a request kind"),
    };

    /// <summary>The wire name of <paramref name="state"/> ('open', 'accepted', 'declined', 'voided').</summary>
    /// <param name="state">The state.</param>
    /// <returns>The name.</returns>
    internal static string StateName(RequestState state) => state switch
    {
        RequestState.Open => "open",
        RequestState.Accepted => "accepted",
        RequestState.Declined => "declined",
        RequestState.Voided => "voided",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a request state"),
    };

    private static Result<int> Flag(Parsed parsed, string[] rest, int i, List<string> words)
    {
        var value = i + 1 < rest.Length ? rest[i + 1] : null;
        switch (rest[i])
        {
            case "--json":
                parsed.Json = true;
                return 0;
            case "--no-start":
                parsed.NoStart = true;
                return 0;
            case "--pending" or "--decided" when parsed.Verb == Verb.Decisions:
                return Pending(parsed, rest[i] == "--pending");
            case "--item" or "--state" or "--note" when value is not null && Takes(parsed.Verb, rest[i]):
                Valued(parsed, rest[i], value);
                return 1;
            case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                return SindriErrors.InvalidInput.With($"'{flag}' isn't a flag of this command (or it needs a value): {Usage}");
            case var word:
                words.Add(word);
                return 0;
        }
    }

    private static Result<int> Pending(Parsed parsed, bool pending)
    {
        if (parsed.Pending is { } earlier && earlier != pending)
        {
            return SindriErrors.InvalidInput.With($"--pending and --decided are alternatives; give one: {Usage}");
        }

        parsed.Pending = pending;
        return 0;
    }

    private static bool Takes(Verb verb, string flag) => flag switch
    {
        "--item" => verb is Verb.Request or Verb.Requests or Verb.Decisions,
        "--state" => verb == Verb.Requests,
        _ => verb == Verb.Decide,
    };

    private static void Valued(Parsed parsed, string flag, string value)
    {
        switch (flag)
        {
            case "--item":
                parsed.Item = value;
                break;
            case "--state":
                parsed.State = value;
                break;
            default:
                parsed.Note = value;
                break;
        }
    }

    private static Error? Shape(Parsed parsed, List<string> words)
    {
        switch (parsed.Verb)
        {
            case Verb.Request:
                return Request(parsed, words);
            case Verb.Decide:
                return Decide(parsed, words);
            case Verb.None or Verb.Requests or Verb.Decisions or Verb.NeedsYou:
            default:
                if (words.Count > 0)
                {
                    return SindriErrors.InvalidInput.With($"'{words[0]}' isn't an argument of this command: {Usage}");
                }

                return parsed.Item is { } item && !ItemRef.TryParse(item, out _) ? SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(item), 80)}' isn't an item id or tracker key") : null;
        }
    }

    private static Error? Request(Parsed parsed, List<string> words)
    {
        if (words.Count < 2)
        {
            return SindriErrors.InvalidInput.With("request needs a kind and a text: sindri request <kind> [--item <item>] <text>");
        }

        var kind = words[0] switch
        {
            "rescope" => RequestKind.Rescope,
            "add-task" => RequestKind.AddTask,
            "note" => RequestKind.Note,
            "handback" => RequestKind.Handback,
            _ => (RequestKind?)null,
        };
        if (kind is null)
        {
            return SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(words[0]), 80)}' isn't a request kind (rescope, add-task, note or handback)");
        }

        if (parsed.Item is { } item && !ItemRef.TryParse(item, out _))
        {
            return SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(item), 80)}' isn't an item id or tracker key");
        }

        parsed.Kind = kind.Value;
        parsed.Text = string.Join(' ', words.Skip(1));
        return null;
    }

    private static Error? Decide(Parsed parsed, List<string> words)
    {
        if (words.Count != 2)
        {
            return SindriErrors.InvalidInput.With("decide needs a decision and a choice: sindri decide <decisionId> <choice> [--note <note>]");
        }

        var decision = RequestRoute.Decision(words[0]);
        if (decision.IsError)
        {
            return decision.Error;
        }

        if (!DecisionOption.TryFrom(words[1], out var choice))
        {
            return SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(words[1]), 80)}' isn't a choice (lower-case letters and '-')");
        }

        parsed.Decision = decision.Value;
        parsed.Choice = choice;
        return null;
    }

    /// <summary>The arguments of one request verb.</summary>
    /// <param name="Verb">The verb.</param>
    internal sealed record Parsed(Verb Verb)
    {
        public bool Json { get; set; }

        public bool NoStart { get; set; }

        /// <summary>For 'decisions': true for --pending, false for --decided, null for neither.</summary>
        public bool? Pending { get; set; }

        public string? Item { get; set; }

        public string? State { get; set; }

        public string? Note { get; set; }

        public RequestKind Kind { get; set; }

        public string Text { get; set; } = string.Empty;

        public DecisionId? Decision { get; set; }

        public DecisionOption? Choice { get; set; }
    }
}
