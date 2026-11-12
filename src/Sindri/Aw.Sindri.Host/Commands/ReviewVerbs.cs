using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri reviews', 'sindri review show' and 'sindri review fix' (PR review page spec §6, §8.2): clients of the review routes.
/// The two reads never start the control plane (API spec §7); 'review fix' does unless --no-start. A session's forge text comes back already
/// inside the untrusted fence, and the CLI prints what it got. 'review fix' reads the PR's head commit first when --head is left out, as the
/// project writes read the version.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ReviewVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = ReviewVerbArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = ensureRunning is null ? null : await ensureRunning(command.Verb == ReviewVerbArguments.Verb.Fix && !command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            ReviewVerbArguments.Verb.List => await List(command, ct).ConfigureAwait(false),
            ReviewVerbArguments.Verb.Show => await Show(command, ct).ConfigureAwait(false),
            ReviewVerbArguments.Verb.Fix => await Fix(command, ct).ConfigureAwait(false),
            ReviewVerbArguments.Verb.None => throw new ArgumentOutOfRangeException(nameof(args), command.Verb, "not a review verb"),
            _ => throw new ArgumentOutOfRangeException(nameof(args), command.Verb, "not a review verb"),
        };
    }

    private static string Route(ReviewPullRef pull) =>
        string.Create(CultureInfo.InvariantCulture, $"/sindri/reviews/{Uri.EscapeDataString(pull.Owner)}/{Uri.EscapeDataString(pull.Repo)}/{pull.Number}");

    private static bool Stale(AsOf asOf) => asOf.Rebuilding || asOf.Stalled;

    private async Task<int> List(ReviewVerbArguments.Parsed command, CancellationToken ct)
    {
        var answer = await client.GetView<ListReviewsResponse>("/sindri/reviews", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var page = answer.Value.Value;
        var code = Stale(page.AsOf) ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RenderList(page), code).ConfigureAwait(false);
    }

    private static string RenderList(ListReviewsResponse page)
    {
        var text = new StringBuilder();
        Line(text, page.Notice);
        Line(text, page.LoginPrompt is { } prompt ? $"{prompt.Code}: {prompt.Text}" + (prompt.Suggested is { } login ? $" ({login}?)" : string.Empty) : null);
        Line(text, page.Connect is { } connect ? $"{connect.Code}: {connect.Text}" : null);
        Line(text, page.Stale?.Text);
        foreach (var group in page.Groups.OrderBy(static g => g.Kind))
        {
            var rows = group.Sections.SelectMany(static s => s.Rows).ToList();
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{group.Title} ({rows.Count})"));
            foreach (var row in rows)
            {
                _ = text.Append("  ").AppendLine(RowLine(row));
            }
        }

        Line(text, page.Empty?.Text);
        Line(text, page.Library.Text);
        return text.ToString();
    }

    private static void Line(StringBuilder text, string? line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            _ = text.AppendLine(line);
        }
    }

    private static string RowLine(ReviewRow row) =>
        string.Create(CultureInfo.InvariantCulture, $"{row.Repo}#{row.Number}  {row.WaitingText ?? "-"}  ci {Ci(row.Ci)}  {(row.Draft ? "[draft] " : string.Empty)}{row.Title}  {row.Author}  {row.LastActivity.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");

    private static string Ci(ReviewCi ci) => ci switch
    {
        ReviewCi.None => "-",
        ReviewCi.Pass => "pass",
        ReviewCi.Fail => "fail",
        ReviewCi.Running => "running",
        _ => throw new ArgumentOutOfRangeException(nameof(ci), ci, "not a CI state"),
    };

    private async Task<int> Show(ReviewVerbArguments.Parsed command, CancellationToken ct)
    {
        var answer = await client.GetView<ReviewPageModel>(Route(command.Pull), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = Stale(answer.Value.Value.AsOf) ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RenderPage(answer.Value.Value), code).ConfigureAwait(false);
    }

    private static string RenderPage(ReviewPageModel page)
    {
        var pr = page.Pr;
        var text = new StringBuilder();
        _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{pr.Repo}#{pr.Number}  {pr.State}{(pr.Draft ? " (draft)" : string.Empty)}  {pr.Title}"));
        Field(text, "author", pr.Author);
        Field(text, "head", $"{pr.Head.Sha7} {pr.Head.Branch} -> {pr.Base}");
        Field(text, "link", pr.Url);
        Field(text, "issues", pr.Issues.Count == 0 ? null : string.Join(", ", pr.Issues.Select(static i => i.Key)));
        Field(text, "note", page.Moving?.Text);
        Field(text, "note", page.Offline?.Text);
        foreach (var part in page.Strip)
        {
            Field(text, part.Panel, part.Text);
        }

        Field(text, "diff", string.Create(CultureInfo.InvariantCulture, $"{page.Diff.FileCount} files{(page.Diff.Capped ? " (capped)" : string.Empty)}"));
        return text.ToString();
    }

    private static void Field(StringBuilder text, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = text.Append("  ").Append(name).Append(": ").AppendLine(value);
        }
    }

    private async Task<int> Fix(ReviewVerbArguments.Parsed command, CancellationToken ct)
    {
        var head = command.Head;
        if (head is null)
        {
            var page = await client.GetView<ReviewPageModel>(Route(command.Pull), ct).ConfigureAwait(false);
            if (page.IsError)
            {
                return await output.Error(page.Error).ConfigureAwait(false);
            }

            head = page.Value.Value.Pr.Head.Sha;
        }

        // why: a typed finding has no file or check behind it, so it goes as a line finding with no place; the thread names the PR, not a line
        var sent = await client.Post<ReviewFixBody, SendReviewFixResponse>(Route(command.Pull) + "/fix", new ReviewFixBody("line", command.Text, head), ct).ConfigureAwait(false);
        if (sent.IsError)
        {
            return await output.Error(sent.Error).ConfigureAwait(false);
        }

        return command.Json
            ? await output.Raw(sent.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(Sent(command.Pull, sent.Value.Value), 0).ConfigureAwait(false);
    }

    private static string Sent(ReviewPullRef pull, SendReviewFixResponse sent)
    {
        var text = new StringBuilder();
        _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"{pull.Owner}/{pull.Repo}#{pull.Number} fix sent as {sent.ItemId.Value} ({sent.Went}{(sent.Session is { } session ? ": " + session : string.Empty)})")).AppendLine();
        Line(text, sent.Posted ? null : sent.PostCode is { } code ? $"thread not posted: {code} {sent.PostError}" : "thread not posted: no thread on this PR, the item is in Work");
        Line(text, sent.FileError);
        return text.ToString();
    }
}
