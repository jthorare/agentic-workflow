using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>
    /// The row for a connection whose token could not be refreshed (connections PR 10b): 'connection:&lt;name&gt;', coloured, text through SafeText.Clean,
    /// and the terminal form 'sindri connection add &lt;name&gt; --oauth' to sign in again.
    /// </summary>
    /// <param name="name">The connection.</param>
    /// <param name="reasonCode">Why the refresh failed, as a code (never a token).</param>
    /// <param name="since">When it failed.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForConnection(ConnectionName name, string reasonCode, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(reasonCode);
        ArgumentNullException.ThrowIfNull(scrubber);
        var text = name.ToString();
        var template = CliForm.Parse("sindri connection add <connection> --oauth");
        var cli = template.IsError ? template : template.Value.Fill("connection", SubjectId.From(text));   // Fill checks the name is safe on a command line
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowId.From("connection:" + text),
            NeedsYouKind.Connection,
            "connection · sign in again",
            new NeedsYouSubject(SubjectKind.Connection, SubjectId.From(text)),
            Item: null,
            $"Sign in to {text} again",
            SafeText.Clean(scrubber, $"The provider refused to renew the sign-in ({reasonCode})", ReasonMax),
            Repo: null,
            Urgency.Colour,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            PrimaryAction: null,
            [],
            cli.Value.Text,
            new NeedsYouOpen(NeedsYouRoutes.Connections, "Open connections"));
    }
}
