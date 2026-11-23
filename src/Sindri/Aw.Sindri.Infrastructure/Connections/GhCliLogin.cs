using System.Text.RegularExpressions;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// <see cref="IGitHubCliLogin"/> through `gh auth status --hostname github.com` (first-run spec decision 13): exit 0 means signed in, and the login comes from its "Logged in to github.com account ada" line
/// ("as ada" on an older gh). The status call prints no token.
/// </summary>
/// <param name="runner">Runs gh.</param>
internal sealed partial class GhCliLogin(IToolRunner runner) : IGitHubCliLogin
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [GeneratedRegex(@"Logged in to github\.com (?:account|as) (?<login>[A-Za-z0-9-]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LoggedIn { get; }

    /// <inheritdoc />
    public ValueTask<string?> Login(CancellationToken cancellationToken)
    {
        var run = runner.Run("gh", ["auth", "status", "--hostname", "github.com"], _timeout, options: null, cancellationToken);
        if (run is not { Outcome: ToolOutcome.Exited, ExitCode: 0 })
        {
            return ValueTask.FromResult<string?>(null);
        }

        var match = LoggedIn.Match(run.Output + "\n" + run.Error);   // gh prints the status on standard error before 2.40 and on standard output after
        var login = match.Success ? match.Groups["login"].Value : null;
        return ValueTask.FromResult(login is not null && PrReviewRules.LoginProblem(login) is null ? login : null);
    }
}
