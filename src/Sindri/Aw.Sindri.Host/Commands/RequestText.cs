using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListRequests;

namespace Aw.Sindri.Host.Commands;

/// <summary>The plain-text form of the request list and the needs-you list (no colour: state is always a word).</summary>
internal static class RequestText
{
    /// <summary>One block per request: its line, then its text indented; the list's sentence first when it has one.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The text.</returns>
    internal static string Requests(ListRequestsResponse list)
    {
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);   // on stale views the rows may be partial, and this says why
        }

        foreach (var request in list.Requests)
        {
            var repo = request.Repo is { } r ? $"  [{r.Value}]" : string.Empty;
            var item = request.Item is { } i ? $"  {i.Value}" : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{request.Id.Value}  {RequestVerbArguments.KindName(request.Kind)}  {RequestVerbArguments.StateName(request.State)}{repo}{item}  by {request.Actor}  since {request.FiledAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}  decision {request.DecisionId.Value}"));
            Indented(text, request.Text);
        }

        return text.ToString();
    }

    /// <summary>One block per decision: its line, the question indented, who chose what when it is decided, the notice, and the command that decides it while it waits; the list's sentence first when it has one.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The text.</returns>
    internal static string Decisions(ListDecisionsResponse list)
    {
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var decision in list.Decisions)
        {
            var repo = decision.Repo is { } r ? $"  [{r.Value}]" : string.Empty;
            var item = decision.Item is { } i ? $"  {i.Value}" : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{decision.Id.Value}  {decision.Label}  {StateName(decision.State)}{repo}{item}  by {decision.FiledBy}  since {decision.FiledAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}"));
            Indented(text, decision.Question);

            if (decision.Choice is { } choice)
            {
                var note = decision.Note is { } n ? $": {n.ReplaceLineEndings("\n").Replace("\n", "\n    ", StringComparison.Ordinal)}" : string.Empty;   // a note's later lines are indented under the line they belong to
                _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  decided {choice.Value} by {decision.DecidedBy} at {decision.DecidedAt:yyyy-MM-ddTHH:mm:ssZ}{note}"));
            }

            if (decision.Notice is { } notice)
            {
                _ = text.Append("  ").AppendLine(notice);
            }

            if (decision.State == DecisionState.Pending)
            {
                _ = text.Append("  ").AppendLine(decision.Cli);
            }
        }

        return text.ToString();
    }

    /// <summary>The text with every line indented, so a multi-line text can't pass for a line of the list.</summary>
    private static void Indented(StringBuilder text, string value)
    {
        foreach (var line in value.Split('\n'))
        {
            _ = text.Append("  ").AppendLine(line);
        }
    }

    private static string StateName(DecisionState state) => state switch
    {
        DecisionState.Pending => "pending",
        DecisionState.Decided => "decided",
        DecisionState.Voided => "voided",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a decision state"),
    };

    /// <summary>One block per row: label, title and since, then why, the notice when there is one, and the command that does it; the sentence when there is none.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The text.</returns>
    internal static string NeedsYou(GetNeedsYouResponse list)
    {
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var row in list.Rows)
        {
            var repo = row.Repo is { } r ? $"  [{r.Value}]" : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{row.Label}  {row.Title}{repo}  since {row.Since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}"))
                .Append("  ").AppendLine(row.Reason);
            if (row.Notice is { } notice)
            {
                _ = text.Append("  ").AppendLine(notice);
            }

            _ = text.Append("  ").AppendLine(row.Cli);
        }

        return text.ToString();
    }
}
