using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>A setup step (<see cref="SetupStepViewModel"/>, continued): a web address in the shown words becomes a link of its own, so the row never shows a raw URL as text.</summary>
internal sealed partial class SetupStepViewModel
{
    /// <summary>The address the step's words pointed at, or null.</summary>
    public Uri? LinkUri { get; private set; }

    /// <summary>The words of the link button; empty without a link.</summary>
    public string LinkText { get; private set; } = string.Empty;

    /// <summary>True when the step has a link.</summary>
    public bool HasLink => LinkUri is not null;

    // Takes the first http(s) address out of a shown sentence (and the ": " before it) and keeps it as the link; the sentence is returned without it.
    private string TakeLink(string text, bool reset)
    {
        if (reset)
        {
            (LinkUri, LinkText) = (null, string.Empty);
        }

        if (LinkUri is not null || UrlInText.Match(text) is not { Success: true } found
            || !Uri.TryCreate(found.Groups["url"].Value, UriKind.Absolute, out var uri))
        {
            return text;
        }

        (LinkUri, LinkText) = (uri, Labels.SetupLinkText(uri.Host));
        return text.Remove(found.Index, found.Length).TrimEnd();
    }

    [GeneratedRegex(@"\s*:?\s*(?<url>https?://[^\s]*[^\s.,;:!?)])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex UrlInText { get; }
}
