using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    private static readonly ActionId _addRepo = ActionId.From(SetupActions.AddRepo);

    /// <summary>The row for the repo step after a failed AddRepo job (setup-once spec §7.3): 'setup:repo', coloured, with AddRepo to try another folder.</summary>
    /// <param name="sentence">Why it failed, in the owner's words.</param>
    /// <param name="since">When the job ended.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForSetupRepo(string sentence, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        ArgumentNullException.ThrowIfNull(scrubber);
        var cli = CliForm.Parse("sindri repo add <path>");
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowId.From("setup:repo"),
            NeedsYouKind.Setup,
            "setup · add your repo",
            new NeedsYouSubject(SubjectKind.Setup, SubjectId.From("repo")),
            Item: null,
            "Add your repo",
            SafeText.Clean(scrubber, sentence, ReasonMax),
            Repo: null,
            Urgency.Colour,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            _addRepo,
            [_addRepo],
            cli.Value.Text);
    }

    private static readonly ActionId _registerMcp = ActionId.From(SetupActions.RegisterMcp);

    /// <summary>The row for a profile approved before the automatic registration existed (setup-once spec §5.4): asked once, 'setup:mcp', and pressing it registers now and records the consent.</summary>
    /// <param name="since">When the row was made.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForTurnOnMcp(DateTimeOffset since, ITextScrubber scrubber) =>
        ForSetupMcp("Sindri can connect your agents to itself in each of your repos, and keep that up to date. Turn it on once and it does it for you.", since, scrubber, "Turn on for your repos");

    /// <summary>The row for the mcp step after a failed RegisterMcp job (setup-once spec §5.4, §7.3): 'setup:mcp', coloured, with RegisterMcp to try again after the owner has fixed what the sentence names.</summary>
    /// <param name="sentence">Why it failed, in the owner's words (the provider's sentence and its fix).</param>
    /// <param name="since">When the job ended.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <param name="headline">The row's headline.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForSetupMcp(string sentence, DateTimeOffset since, ITextScrubber scrubber, string headline = "Connect your agents")
    {
        ArgumentNullException.ThrowIfNull(sentence);
        ArgumentNullException.ThrowIfNull(scrubber);
        var cli = CliForm.Parse("sindri mcp register --apply");
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowId.From("setup:mcp"),
            NeedsYouKind.Setup,
            "setup · connect your agents",
            new NeedsYouSubject(SubjectKind.Setup, SubjectId.From("mcp")),
            Item: null,
            headline,
            SafeText.Clean(scrubber, sentence, ReasonMax),
            Repo: null,
            Urgency.Colour,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            _registerMcp,
            [_registerMcp],
            cli.Value.Text);
    }
}
