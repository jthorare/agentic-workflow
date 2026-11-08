namespace Aw.Sindri.Ui.Navigation;

/// <summary>
/// A place inside the app (UI spec §3.3): 'home/needs-you', 'needs-you/&lt;rowId&gt;', 'item/&lt;ItemId&gt;',
/// 'decision/&lt;DecisionId&gt;', 'service/&lt;name&gt;', 'job/&lt;JobId&gt;', and from screens spec §3 'node/&lt;key&gt;', 'session/&lt;provider&gt;/&lt;id&gt;' 'system/&lt;dependency&gt;' and 'project/&lt;escaped id&gt;'. The tray, the palette and a
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

    /// <summary>The Reviews page.</summary>
    internal static DeepLink Reviews { get; } = new(DeepLinkKind.Reviews, Subject: null);

    /// <summary>The Add project sheet.</summary>
    internal static DeepLink ProjectAdd { get; } = new(DeepLinkKind.ProjectAdd, Subject: null);

    /// <summary>The page sheet.</summary>
    internal static DeepLink PageSheet { get; } = new(DeepLinkKind.PageSheet, Subject: null);

    /// <summary>The Add several pages sheet.</summary>
    internal static DeepLink AddSeveralPages { get; } = new(DeepLinkKind.AddSeveralPages, Subject: null);
    /// <summary>The library errors sheet.</summary>
    internal static DeepLink LibrarySheet { get; } = new(DeepLinkKind.LibrarySheet, Subject: null);

    /// <summary>The fix-task sheet.</summary>
    internal static DeepLink FixTaskSheet { get; } = new(DeepLinkKind.FixTaskSheet, Subject: null);

    /// <summary>The Pin sheet.</summary>
    internal static DeepLink PinSheet { get; } = new(DeepLinkKind.PinSheet, Subject: null);

    /// <summary>One PR's review page.</summary>
    /// <param name="repo">owner/name.</param>
    /// <param name="number">The PR number.</param>
    /// <returns>The link ('reviews/acme/api/2071').</returns>
    internal static DeepLink ForReview(string repo, int number) => new(DeepLinkKind.Review, repo + "/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
        if (kind is null || !IsSubjectOf(kind.Value, subject))
        {
            return false;
        }

        link = new DeepLink(kind.Value, subject);
        return true;
    }

    // The links that have no subject: 'home/needs-you', 'setup', 'projects' and 'projects/add'.
    private static bool TryParsePage(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeepLink? link)
    {
        link = Array.Find(_pageTexts, p => string.Equals(p.Text, text, StringComparison.Ordinal)) is { Text: not null } page ? new DeepLink(page.Kind, Subject: null) : ContextItemOf(text);
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
        Kind == DeepLinkKind.ContextItem ? ContextItemPrefix + Subject : Array.Find(_pageTexts, p => p.Kind == Kind).Text ?? (_kinds.First(k => k.Kind == Kind).Prefix + "/" + Subject);

    // The kinds with no subject, and their text.
    private static readonly (DeepLinkKind Kind, string Text)[] _pageTexts =
    [
        (DeepLinkKind.HomeNeedsYou, HomeNeedsYouText), (DeepLinkKind.Setup, SetupText), (DeepLinkKind.Projects, ProjectsText), (DeepLinkKind.Reviews, ReviewsText),
        (DeepLinkKind.LibrarySheet, LibrarySheetText), (DeepLinkKind.FixTaskSheet, FixTaskSheetText), (DeepLinkKind.ProjectAdd, ProjectAddText), (DeepLinkKind.PinSheet, PinSheetText), (DeepLinkKind.PageSheet, PageSheetText),
        (DeepLinkKind.AddSeveralPages, AddSeveralPagesText),
    ];

    private const string HomeNeedsYouText = "home/needs-you";

    private const string SetupText = "setup";

    private const string ProjectsText = "projects";

    private const string ReviewsText = "reviews";

    private const string LibrarySheetText = "reviews/library";

    private const string FixTaskSheetText = "reviews/fix";

    private const string ProjectAddText = "projects/add";

    private const string PageSheetText = "context/page";

    private const string AddSeveralPagesText = "context/pages/add";
    private const string PinSheetText = "context/pin";

    private const string ContextItemPrefix = "context?item=";

    private const int MaxSubject = 128;

    private static readonly (string Prefix, DeepLinkKind? Kind)[] _kinds =
    [
        ("needs-you", DeepLinkKind.NeedsYou),
        ("item", DeepLinkKind.Item),
        ("decision", DeepLinkKind.Decision),
        ("service", DeepLinkKind.Service),
        ("job", DeepLinkKind.Job),
        ("node", DeepLinkKind.Node),
        ("session", DeepLinkKind.Session),
        ("system", DeepLinkKind.System),
        ("project", DeepLinkKind.Project),
        ("reviews", DeepLinkKind.Review),
        ("linear", DeepLinkKind.Issue),
    ];

    // Each kind's own rule for its subject.
    private static bool IsSubjectOf(DeepLinkKind kind, string subject) => kind switch
    {
        DeepLinkKind.Session => IsSessionRef(subject),
        DeepLinkKind.Project => IsEscapedId(subject),
        DeepLinkKind.Review => IsPrRef(subject),
        DeepLinkKind.Issue => IsIssueKey(subject),
        DeepLinkKind.HomeNeedsYou or DeepLinkKind.NeedsYou or DeepLinkKind.Item or DeepLinkKind.Decision or DeepLinkKind.Service or DeepLinkKind.Job or DeepLinkKind.Node or DeepLinkKind.ContextItem or DeepLinkKind.System or DeepLinkKind.Setup or DeepLinkKind.Projects or DeepLinkKind.Reviews or DeepLinkKind.LibrarySheet or DeepLinkKind.FixTaskSheet or DeepLinkKind.ProjectAdd or DeepLinkKind.PageSheet or DeepLinkKind.AddSeveralPages or DeepLinkKind.PinSheet => IsSubject(subject),
        _ => false,
    };

    // A review link names the PR as owner/repo/number: three segments, the first two subjects and no dot step, the number a positive integer.
    private static bool IsPrRef(string s) =>
        s.Length <= MaxSubject && s.Split('/') is [var owner, var repo, var number] && owner is not ("." or "..") && repo is not ("." or "..") && IsSubject(owner) && IsSubject(repo)
        && number.All(char.IsAsciiDigit) && int.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0;

    // A Linear link names the issue by its key: the team key, a dash and the number ('ENG-12').
    private static bool IsIssueKey(string s) =>
        s.Length <= MaxSubject && s.Split('-') is [{ Length: > 0 } team, { Length: > 0 } number] && team.All(char.IsAsciiLetterOrDigit) && number.All(char.IsAsciiDigit);

    // A session link names the session by its ref, provider/id: two segments, each a subject, neither a dot step.
    private static bool IsSessionRef(string s) =>
        s.Length <= MaxSubject && s.Split('/') is [var provider, var id] && provider is not ("." or "..") && id is not ("." or "..") && IsSubject(provider) && IsSubject(id);

    // A project link names the project by its id escaped as one segment ('linear%3Aproject%2F<uuid>'): the escape's own characters and the subject's, and nothing that unescapes to a path step.
    private static bool IsEscapedId(string s) =>
        s.Length is > 0 and <= MaxSubject && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':' or '%') && Unescapes(s);

    private static bool Unescapes(string s)
    {
        var id = Uri.UnescapeDataString(s);
        return id.Length > 0 && id is not ("." or "..") && !id.Any(char.IsControl);
    }

    private static bool IsSubject(string s) =>
        s.Length is > 0 and <= MaxSubject && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
