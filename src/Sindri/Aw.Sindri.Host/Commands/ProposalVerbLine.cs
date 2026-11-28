namespace Aw.Sindri.Host.Commands;

/// <summary>One parsed 'sindri proposals' or 'sindri proposal show|reject' line.</summary>
/// <param name="Verb">'list', 'show' or 'reject'.</param>
/// <param name="Id">The proposal for show and reject.</param>
/// <param name="State">The --state filter of list.</param>
/// <param name="Json">--json: print the route's JSON.</param>
/// <param name="Fence">--fence or --no-fence; null leaves it to whether stdout is a terminal.</param>
internal sealed record ProposalVerbLine(string Verb, string? Id, string? State, bool Json, bool? Fence)
{
    /// <summary>Parses the words after 'sindri'.</summary>
    /// <param name="words">Such as ["proposals", "--state", "held"] or ["proposal", "show", "prp_…"].</param>
    /// <returns>The line, or null when it isn't a proposal line.</returns>
    internal static ProposalVerbLine? Parse(string[] words)
    {
        ArgumentNullException.ThrowIfNull(words);
        return words switch
        {
            ["proposals", .. var rest] => Flags("list", id: null, rest),
            ["proposal", "list", .. var rest] => Flags("list", id: null, rest),
            ["proposal", "show" or "reject", var id, .. var rest] when !id.StartsWith("--", StringComparison.Ordinal) => Flags(words[1], id, rest),
            _ => null,
        };
    }

    private static ProposalVerbLine? Flags(string verb, string? id, string[] rest)
    {
        var line = new ProposalVerbLine(verb, id, State: null, Json: false, Fence: null);
        for (var i = 0; i < rest.Length; i++)
        {
            line = rest[i] switch
            {
                "--json" => line with { Json = true },
                "--fence" when verb != "reject" => line with { Fence = true },
                "--no-fence" when verb != "reject" => line with { Fence = false },
                "--state" when verb == "list" && i + 1 < rest.Length => line with { State = rest[++i] },
                _ => null,
            };
            if (line is null)
            {
                return null;
            }
        }

        return line;
    }
}
