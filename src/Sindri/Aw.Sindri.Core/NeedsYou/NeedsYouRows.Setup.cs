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
}
