using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>
    /// The row of a lane trigger (Home spec §3.2): 'lane-recycle:&lt;lane&gt;', 'agent-limited:&lt;subject&gt;' or 'pr-ready-merge:&lt;pr&gt;'. Text goes through SafeText.Clean. It offers no action of its own;
    /// the owner acts in the lane's terminal or on the PR, and Home's later actions attach by kind.
    /// </summary>
    /// <param name="raised">The raised event's payload.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row, or SND-CLI-003 when the kind is not a lane kind or the key is not safe as a subject id.</returns>
    public static Result<NeedsYouRow> ForLane(HomeLaneRowRaised raised, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(raised);
        ArgumentNullException.ThrowIfNull(scrubber);
        var kind = LaneKind(raised.Kind);
        if (kind is null)
        {
            return SindriErrors.InvalidInput.With($"'{raised.Kind}' is not a lane row kind");
        }

        var cli = CliForm.Parse(kind == NeedsYouKind.PrReadyMerge ? "sindri status" : "sindri agents");   // why: the read that shows the thing the row is about
        if (cli.IsError)
        {
            return cli.Error;
        }

        var pr = kind == NeedsYouKind.PrReadyMerge;
        var id = NeedsYouRowId.From(raised.Kind + ":" + raised.Key);
        return new NeedsYouRow(
            id,
            kind.Value,
            raised.Kind.Replace('-', ' '),
            new NeedsYouSubject(pr ? SubjectKind.PullRequest : SubjectKind.Lane, SubjectId.From(raised.Key)),
            Item: null,
            SafeText.Clean(scrubber, raised.Title, TitleMax),
            SafeText.Clean(scrubber, raised.Reason, ReasonMax),
            Repo: null,
            pr ? Urgency.Pulse : Urgency.Colour,
            raised.At,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            PrimaryAction: null,
            [],
            cli.Value.Text,
            new NeedsYouOpen(NeedsYouRoutes.Row(id), "Open the row"));
    }

    private static NeedsYouKind? LaneKind(string wire) => wire switch
    {
        "lane-recycle" => NeedsYouKind.LaneRecycle,
        "agent-limited" => NeedsYouKind.AgentLimited,
        "pr-ready-merge" => NeedsYouKind.PrReadyMerge,
        _ => null,
    };
}
