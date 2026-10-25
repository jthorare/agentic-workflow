using System.Globalization;
using System.Text;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Checks a question before any source sees it (spec §8.1, §8.3 caps): the text and node shapes; item and symbol anchors arrive in later phases.</summary>
public static partial class QueryRules
{
    /// <summary>The most search terms a question keeps (raised from 16 per owner q93, 2026-10-23).</summary>
    public const int MaxTerms = 64;

    /// <summary>The longest node, item or symbol anchor; a question's text has no length limit (owner q93, 2026-10-23).</summary>
    public const int MaxTextLength = 2_000;

    /// <summary>The deepest cascade a request gets: a deeper ask is read as this depth (spec §8.3; raised from 4 and clamped per owner q93, 2026-10-23).</summary>
    public const int MaxDepth = 8;

    /// <summary>The longest value, percent-encoded, a query URL carries: under Kestrel's 8 KB request line, with room for the rest of the URL. The CLI and probe files both refuse a longer one with a clear code.</summary>
    public const int MaxEncodedText = 6_000;

    /// <summary>What a bad depth's 006 starts with (a transport adds its own hint after it).</summary>
    public const string DepthMessage = "depth must be a whole number of 0 or more (deeper than 8 is read as 8)";

    /// <summary>What a bad page's 006 says.</summary>
    public const string PageMessage = "limit must be a whole number of 1 or more, and offset a whole number of 0 or more";

    /// <summary>What the 006 for two or no question shapes says (a transport adds its own spelling of the shapes after it).</summary>
    public const string ShapeMessage = "ask exactly one of: text, node, item or symbol";

    /// <summary>What the 006 for a bad node anchor says (2000 is <see cref="MaxTextLength"/>; a transport adds its own spelling after it).</summary>
    public const string NodeMessage = "a node anchor names a node key, alias or name of 1 to 2000 characters";

    /// <summary>The 006 message for an empty or overlong item anchor.</summary>
    public const string ItemMessage = "an item anchor names a tracker key or record key of 1 to 2000 characters";

    /// <summary>The 006 message for an empty or overlong symbol anchor.</summary>
    public const string SymbolMessage = "a symbol anchor is a file path of 1 to 2000 characters";

    /// <summary>The 006 message for a repo given without a diff.</summary>
    public const string RepoMessage = "a repo goes with a diff only (it names the repo whose commits the range is read in)";

    /// <summary>Whether a value, percent-encoded, fits <see cref="MaxEncodedText"/>.</summary>
    /// <param name="value">The question text or node name.</param>
    /// <returns>True when a GET query string can carry it.</returns>
    public static bool FitsQueryUrl(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Uri.EscapeDataString(value).Length <= MaxEncodedText;
    }

    /// <summary>Checks <paramref name="query"/>.</summary>
    /// <param name="query">The question.</param>
    /// <param name="settings">The checked settings.</param>
    /// <returns>The terms (none for the node shape: they come from its anchor); SND-CONTEXT-006 for a malformed question; SND-CONTEXT-003 for the item and symbol shapes.</returns>
    public static Result<ValidQuery> Check(ContextQuery query, ContextSettings settings)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(settings);
        var shapes = new object?[] { query.Text, query.Node, query.Item, query.Symbol, query.Diff }.Count(s => s is not null);
        if (shapes != 1)
        {
            return ContextErrors.QueryInvalid.With(ShapeMessage);
        }

        if (query.Repo is not null && query.Diff is null)
        {
            return ContextErrors.QueryInvalid.With(RepoMessage);
        }

        if (CheckDepth(query.Depth) is { } depthError)
        {
            return depthError;
        }

        if (ParseHops(query.Hops) is { IsError: true } hopsError)
        {
            return hopsError.Error;
        }

        return Shaped(query);
    }

    /// <summary>A depth as a transport sent it: absent, or a whole number of 0 or more (a depth past <see cref="MaxDepth"/> is read as <see cref="MaxDepth"/>).</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The depth (null when absent); SND-CONTEXT-006 when it is not a whole number or is negative. The message names no flag: a transport adds its own hint.</returns>
    public static Result<int?> ParseDepth(string? text)
    {
        var parsed = ParseWhole(text, DepthMessage, 0, int.MaxValue);
        return parsed.IsError || parsed.Value is not { } depth ? parsed : Math.Min(depth, MaxDepth);
    }

    /// <summary>The cascade hops as a transport sent them: absent, or a comma list of state, flow, dependency and component (repeated keys joined with commas).</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The hops (null when absent); SND-CONTEXT-140 when a name is unknown or the list is empty.</returns>
    public static Result<CascadeHops?> ParseHops(string? text)
    {
        if (text is null)
        {
            return (CascadeHops?)null;
        }

        var hops = CascadeHops.None;
        foreach (var name in text.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!name.All(char.IsAsciiLetter) || !Enum.TryParse<CascadeHops>(name, ignoreCase: true, out var hop) || hop is CascadeHops.None or CascadeHops.Default || !Enum.IsDefined(hop))
            {
                return ContextErrors.HopsInvalid.With("hops must be a comma list of state, flow, dependency and component");
            }

            hops |= hop;
        }

        return hops;
    }

    /// <summary>The cascade options for a call: the hops it named (the default three when none), and the profile's component fan-out cap.</summary>
    /// <param name="hops">The hops from <see cref="ParseHops"/>, or null.</param>
    /// <param name="settings">The profile's context settings.</param>
    /// <returns>The options.</returns>
    public static CascadeOptions CascadeFor(CascadeHops? hops, ContextSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new CascadeOptions(hops ?? CascadeHops.Default, settings.CascadeComponentFanOut);
    }

    /// <summary>The 006 for a depth already a number, worded as <see cref="ParseDepth"/> words it.</summary>
    /// <param name="depth">The depth, or null.</param>
    /// <returns>Null when absent or 0 or more (a deeper one is clamped to <see cref="MaxDepth"/> where it is read), else the error.</returns>
    public static Error? CheckDepth(int? depth) =>
        depth is { } value && value < 0 ? OutOfRange(DepthMessage, value.ToString(CultureInfo.InvariantCulture)) : null;

    /// <summary>The 006 for a page: both are optional (absent limit keeps the rest, absent offset is 0); a limit under 1 or an offset under 0 is refused.</summary>
    /// <param name="limit">How many items to keep, or null.</param>
    /// <param name="offset">How many items to skip, or null for the start.</param>
    /// <returns>Null when the page is absent or in range, else the error.</returns>
    public static Error? CheckPage(int? limit, int? offset) =>
        limit is < 1 || offset is < 0 ? ContextErrors.QueryInvalid.With(PageMessage) : null;

    /// <summary>The 006 for an anchor set that is empty; any number of anchors is taken.</summary>
    /// <param name="anchors">The distinct anchor values.</param>
    /// <param name="call">The call named in the message: lenses, code or history.</param>
    /// <returns>Null when there is at least one, else the error.</returns>
    public static Error? CheckAnchors(IReadOnlyList<string> anchors, string call)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        return anchors.Count >= 1
            ? null
            : ContextErrors.QueryInvalid.With($"a {call} call takes at least 1 anchor, not 0");
    }

    private static Result<int?> ParseWhole(string? text, string rule, int min, int max)
    {
        if (text is null)
        {
            return (int?)null;
        }

        var digits = text.Length > 0 && text[0] is '-' or '+' ? text[1..] : text;
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return ContextErrors.QueryInvalid.With(rule);
        }

        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }

        return OutOfRange(rule, text);
    }

    private static Error OutOfRange(string rule, string shown) =>
        ContextErrors.QueryInvalid.With($"{rule}; {(shown.Length <= 12 ? shown : "that value")} is out of range");

    // The symbol shape in its file-path form: a path in the repo. A symbol inside a file ('path#name') needs the code index (Phase 9).
    private static Result<ValidQuery> FilePath(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Length > MaxTextLength || symbol.Any(char.IsControl))
        {
            return ContextErrors.QueryInvalid.With(SymbolMessage);
        }

        return symbol.Contains('#', StringComparison.Ordinal)
            ? ContextErrors.UnknownAnchor.With("symbols inside a file need the code index (a later phase); ask by the file's path")
            : new ValidQuery([]);
    }

    /// <summary>The one shape's own checks: a diff's two shas; a node name's length; a file path; the text's words; an item not yet (003).</summary>
    private static Result<ValidQuery> Shaped(ContextQuery query)
    {
        if (query.Diff is { } diff)
        {
            return diff.IsValid() ? new ValidQuery([]) : ContextErrors.QueryInvalid.With(DiffRange.Message);
        }

        if (query.Node is { } node)
        {
            return string.IsNullOrWhiteSpace(node) || node.Length > MaxTextLength
                ? ContextErrors.QueryInvalid.With(NodeMessage)
                : new ValidQuery([]);
        }

        if (query.Symbol is { } symbol)
        {
            return FilePath(symbol);
        }

        return query.Item is { } item ? Item(item) : Question(query.Text ?? string.Empty);
    }

    /// <summary>SND-CONTEXT-003 for an item no stored record answers to, with the tracker keys of linked records to ask for instead.</summary>
    /// <param name="item">The item as asked.</param>
    /// <param name="suggested">Tracker keys of linked records.</param>
    /// <param name="syncConfigured">True when an intent sync runs: the fix then says the record isn't linked yet or the key is unknown, instead of asking for the service.</param>
    /// <returns>The error, with a suggestedAnchors member when there is something to suggest.</returns>
    public static Error UnknownItem(string item, IReadOnlyList<string> suggested, bool syncConfigured = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(suggested);
        var shown = item.Length <= 80 && !item.Any(char.IsControl) ? item.Trim() : "(a long or unusual item)";
        var error = ContextErrors.UnknownAnchor.With($"no synced record is linked to the item '{shown}' (fix: {Fix(suggested, syncConfigured)})");
        return suggested.Count == 0 ? error : error.WithExtension("suggestedAnchors", [.. suggested], SettingsJsonContext.Default.ListString);
    }

    private static string Fix(IReadOnlyList<string> suggested, bool syncConfigured)
    {
        var next = syncConfigured
            ? "the record isn't linked yet or the key is unknown: check the key, run sindri context sources sync, wait for the next context-sync slot, or watch sindri context sources status"
            : "name the context-sync service under services: in the profile";
        return suggested.Count == 0 ? next : syncConfigured ? $"try {string.Join(", ", suggested)}; {next}" : "try " + string.Join(", ", suggested);
    }

    private static Result<ValidQuery> Item(string item) =>
        string.IsNullOrWhiteSpace(item) || item.Length > MaxTextLength || item.Any(char.IsControl)
            ? ContextErrors.QueryInvalid.With(ItemMessage)
            : new ValidQuery([]);

    private static Result<ValidQuery> Question(string text)
    {
        var terms = Terms(text);
        if (terms.Count == 0)
        {
            return ContextErrors.QueryInvalid.With("the question needs at least one word");
        }

        return new ValidQuery(terms);
    }

    /// <summary>The distinct words of <paramref name="text"/>, as <see cref="Terms"/> reads them but with a cap of the caller's choosing.</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The most words to return.</param>
    /// <returns>Up to <paramref name="max"/> words, in order of first use.</returns>
    public static IReadOnlyList<string> Words(string text, int max)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text.Append(' '))
        {
            if (char.IsLetterOrDigit(c) && word.Length < 64)
            {
                _ = word.Append(char.ToLowerInvariant(c));
            }
            else if (!char.IsLetterOrDigit(c) && word.Length > 0)
            {
                var term = word.ToString();
                _ = word.Clear();
                if (!words.Contains(term, StringComparer.Ordinal) && words.Count < max)
                {
                    words.Add(term);
                }
            }
        }

        return words;
    }

    /// <summary>The distinct words of <paramref name="text"/>: runs of letters and digits, lower-cased, at most 64 characters each.</summary>
    /// <param name="text">The question.</param>
    /// <returns>Up to <see cref="MaxTerms"/> terms, in order of first use.</returns>
    public static IReadOnlyList<string> Terms(string text) => Words(text, MaxTerms);
}
