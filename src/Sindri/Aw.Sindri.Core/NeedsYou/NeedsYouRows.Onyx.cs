using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>The row for the managed Onyx: it isn't set up, or serve could not start it. Carries no action id: the human runs the command.</summary>
    /// <param name="notice">What needs the human.</param>
    /// <param name="since">When the list was read (the notice has no history of its own).</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForOnyx(OnyxNotice notice, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(scrubber);
        var cli = CliForm.Parse(notice.Command);
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowId.From("failure:service:onyx"),
            NeedsYouKind.Failure,
            "onyx · " + notice.Title.ToLowerInvariant(),
            new NeedsYouSubject(SubjectKind.Service, SubjectId.From("onyx")),
            Item: null,
            SafeText.Clean(scrubber, notice.Title, TitleMax),
            SafeText.Clean(scrubber, notice.Reason, ReasonMax),
            Repo: null,
            Urgency.Colour,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            notice.Action is null ? null : ActionId.From(notice.Action),
            notice.Action is null ? [] : [ActionId.From(notice.Action)],
            cli.Value.Text);
    }
}
