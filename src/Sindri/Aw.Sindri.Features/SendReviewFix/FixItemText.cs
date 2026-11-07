using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>The words of a fix task's item (PR review page spec §8.2 step 1): the title, the body and the thread's marker.</summary>
internal static class FixItemText
{
    private const int MaxTitle = 200;
    private const int MaxBody = 4000;
    private const string TitlePrefix = "Fix: ";

    /// <summary>The marker the thread carries, which the babysitter reads the item id from.</summary>
    /// <param name="item">The item's id.</param>
    /// <returns>"sindri.fix:&lt;item id&gt;".</returns>
    internal static string Marker(string item) => "sindri.fix:" + item;

    /// <summary>"Fix: " and the first line of the text, within the item title's 200 characters and without control characters.</summary>
    /// <param name="text">The finding.</param>
    /// <returns>The title.</returns>
    internal static string Title(string text)
    {
        var first = new string([.. text.Trim().Split('\n')[0].Where(static c => !char.IsControl(c))]).Trim();
        var room = MaxTitle - TitlePrefix.Length;
        return TitlePrefix + (first.Length > room ? first[..room].TrimEnd() : first);
    }

    /// <summary>The body: the finding as quoted text, then file and line, head, PR and branch lines.</summary>
    /// <param name="request">The request.</param>
    /// <param name="slug">The PR's repository as "owner/name".</param>
    /// <param name="link">The PR's link.</param>
    /// <param name="head">The head string, "owner/repo:branch".</param>
    /// <returns>The body.</returns>
    internal static string Body(SendReviewFixRequest request, string slug, string link, string head)
    {
        var tail = new StringBuilder();
        if (!string.IsNullOrEmpty(request.Path))
        {
            _ = tail.Append("file: ").Append(Where(request)).Append('\n');
        }

        _ = tail.Append("head: ").Append(request.Head).Append('\n')
            .Append("pr: ").Append(slug).Append('#').Append(request.Number.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("link: ").Append(link).Append('\n')
            .Append("branch: ").Append(head);
        var quoted = string.Join('\n', request.Text.Trim().ReplaceLineEndings("\n").Split('\n').Select(static line => "> " + line));
        var room = MaxBody - tail.Length - 2;
        return (quoted.Length > room ? quoted[..room] : quoted) + "\n\n" + tail;
    }

    /// <summary>"path:line", with " (removed line)" for the old side.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The place.</returns>
    internal static string Where(SendReviewFixRequest request) =>
        request.Path + (request.Line is { } line ? ":" + line.ToString(CultureInfo.InvariantCulture) : string.Empty) + (request.Side == "old" ? " (removed line)" : string.Empty);

    /// <summary>The review to post: a line comment on the new side; a body comment naming the place for the old side (a removed line has no line on the head, until Ph8) and for a finding with no file.</summary>
    /// <param name="request">The request.</param>
    /// <param name="link">The PR's link.</param>
    /// <param name="item">The item's id.</param>
    /// <returns>The review.</returns>
    internal static PullReview Review(SendReviewFixRequest request, string link, string item)
    {
        var marker = Marker(item);
        var text = request.Text.Trim();
        if (request.Path is { Length: > 0 } path && request.Line is { } line && request.Side != "old")
        {
            return new PullReview(link, request.Head, PullReviewVerdict.Comment, string.Empty, [new PullReviewComment(path, line, text)], marker);
        }

        var body = request.Path is { Length: > 0 } ? Where(request) + "\n\n" + text : text;
        return new PullReview(link, request.Head, PullReviewVerdict.Comment, body, [], marker);
    }
}
