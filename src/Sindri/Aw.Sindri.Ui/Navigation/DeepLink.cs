namespace Aw.Sindri.Ui.Navigation;

/// <summary>
/// A place inside the app (UI spec §3.3): 'home/needs-you', 'needs-you/&lt;rowId&gt;', 'item/&lt;ItemId&gt;',
/// 'decision/&lt;DecisionId&gt;', 'service/&lt;name&gt;', 'job/&lt;JobId&gt;', and from screens spec §3 'workflow/&lt;address&gt;', 'recipe/&lt;id&gt;', 'node/&lt;key&gt;', 'session/&lt;provider&gt;/&lt;id&gt;' and 'system/&lt;dependency&gt;'. The tray, the palette and a
/// second launch use them; a deep link only navigates. Subjects are opaque to the app: one segment of letters, digits and
/// '-_.:', at most 128 characters.
/// </summary>
/// <param name="Kind">What it opens.</param>
/// <param name="Subject">The row, item, decision, service or job; null for <see cref="DeepLinkKind.HomeNeedsYou"/>.</param>
internal sealed record DeepLink(DeepLinkKind Kind, string? Subject)
{
    /// <summary>Home with Needs you expanded.</summary>
    internal static DeepLink HomeNeedsYou { get; } = new(DeepLinkKind.HomeNeedsYou, Subject: null);

    /// <summary>Settings at the setup checklist.</summary>
    internal static DeepLink Setup { get; } = new(DeepLinkKind.Setup, Subject: null);

    /// <summary>The Projects page.</summary>
    internal static DeepLink Projects { get; } = new(DeepLinkKind.Projects, Subject: null);

    /// <summary>Parses a deep link's text.</summary>
    /// <param name="text">The text (from a second launch, the tray or the palette).</param>
    /// <param name="link">The link, when the text is one.</param>
    /// <returns>True when <paramref name="text"/> is a deep link.</returns>
    internal static bool TryParse(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeepLink? link)
    {
        link = null;
        if (TryParsePage(text, out var page))
        {
            link = page;
            return true;
        }

        var slash = text?.IndexOf('/', StringComparison.Ordinal) ?? -1;
        if (slash < 0)
        {
            return false;
        }

        var kind = _kinds.FirstOrDefault(k => text.AsSpan(0, slash).SequenceEqual(k.Prefix)).Kind;
        var subject = text![(slash + 1)..]; // why: slash >= 0 only when text isn't null
        if (kind is null || !(kind == DeepLinkKind.Workflow ? IsAddress(subject) : kind == DeepLinkKind.Recipe ? IsRecipe(subject) : kind == DeepLinkKind.Session ? IsSessionRef(subject) : IsSubject(subject)))
        {
            return false;
        }

        link = new DeepLink(kind.Value, subject);
        return true;
    }

    // The links that name a page and have no subject: 'home/needs-you', 'setup' and 'projects'.
    private static bool TryParsePage(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeepLink? link)
    {
        link = string.Equals(text, HomeNeedsYouText, StringComparison.Ordinal) ? HomeNeedsYou : string.Equals(text, SetupText, StringComparison.Ordinal) ? Setup : string.Equals(text, ProjectsText, StringComparison.Ordinal) ? Projects : ContextItemOf(text);
        return link is not null;
    }

    // 'context?item=<ItemId>': the Context page for an item.
    private static DeepLink? ContextItemOf(string? text) =>
        text is not null && text.StartsWith(ContextItemPrefix, StringComparison.Ordinal) && IsSubject(text[ContextItemPrefix.Length..])
            ? new DeepLink(DeepLinkKind.ContextItem, text[ContextItemPrefix.Length..])
            : null;

    /// <summary>The link's text, the form <see cref="TryParse"/> reads.</summary>
    /// <returns>The text.</returns>
    public override string ToString() =>
        Kind == DeepLinkKind.HomeNeedsYou ? HomeNeedsYouText : Kind == DeepLinkKind.Setup ? SetupText : Kind == DeepLinkKind.Projects ? ProjectsText : Kind == DeepLinkKind.ContextItem ? ContextItemPrefix + Subject : _kinds.First(k => k.Kind == Kind).Prefix + "/" + Subject;

    private const string HomeNeedsYouText = "home/needs-you";

    private const string SetupText = "setup";

    private const string ProjectsText = "projects";

    private const string ContextItemPrefix = "context?item=";

    private const int MaxSubject = 128;

    private static readonly (string Prefix, DeepLinkKind? Kind)[] _kinds =
    [
        ("needs-you", DeepLinkKind.NeedsYou),
        ("item", DeepLinkKind.Item),
        ("decision", DeepLinkKind.Decision),
        ("service", DeepLinkKind.Service),
        ("job", DeepLinkKind.Job),
        ("workflow", DeepLinkKind.Workflow),
        ("recipe", DeepLinkKind.Recipe),
        ("node", DeepLinkKind.Node),
        ("session", DeepLinkKind.Session),
        ("system", DeepLinkKind.System),
    ];

    // A workflow address is the instance's, whose tail may hold segments ('ITM-01JC8/step'): each is a subject, none a dot step.
    private static bool IsAddress(string s) =>
        s.Length <= MaxSubject && s.Split('/').All(segment => segment is not ("." or "..") && IsSubject(segment));

    // A recipe link names the recipe, and may pin a version after '@': the hash, whole or by a prefix ('id@hash12').
    private static bool IsRecipe(string s) =>
        s.Length <= MaxSubject && s.Split('@') is var parts && parts.Length <= 2 && parts.All(IsSubject);

    // A session link names the session by its ref, provider/id: two segments, each a subject, neither a dot step.
    private static bool IsSessionRef(string s) =>
        s.Length <= MaxSubject && s.Split('/') is [var provider, var id] && provider is not ("." or "..") && id is not ("." or "..") && IsSubject(provider) && IsSubject(id);

    private static bool IsSubject(string s) =>
        s.Length is > 0 and <= MaxSubject && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
