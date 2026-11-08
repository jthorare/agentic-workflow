using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.GetRepoStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri repo status [&lt;path&gt;] [--nudge] [--json]': where a repo stands against the approved profile. Local and read-only: it reads the approved snapshot and asks git, takes no lock, writes
/// nothing and never starts the control plane, so the SessionStart hook can call it with --nudge (hook contract). --nudge never fails: an error is silence, exit 0.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
internal sealed class RepoStatusVerb(IServiceProvider services, Output output)
{
    private const string Usage = "repo status [<path>] [--nudge] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var nudge = args.Contains("--nudge", StringComparer.Ordinal);
        var paths = args.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (args.Any(static a => a.StartsWith("--", StringComparison.Ordinal) && a is not ("--json" or "--nudge")) || paths.Count > 1)
        {
            return nudge ? 0 : await output.Error(SindriErrors.InvalidInput.With($"'repo status {string.Join(' ', args)}' is not a repo command: {Usage}")).ConfigureAwait(false);
        }

        var request = new GetRepoStatusRequest(Path.GetFullPath(paths.Count == 1 ? paths[0] : Environment.CurrentDirectory));
        return nudge ? await Nudge(request, ct).ConfigureAwait(false) : await Plain(request, args.Contains("--json", StringComparer.Ordinal), ct).ConfigureAwait(false);
    }

    /// <summary>The one line or nothing; never fails and never blocks a session, so any error is silence.</summary>
    private async Task<int> Nudge(GetRepoStatusRequest request, CancellationToken ct)
    {
        try
        {
            var answer = await services.GetRequiredService<IService<GetRepoStatusRequest, GetRepoStatusResponse>>().Handle(request, ct).ConfigureAwait(false);
            return answer.IsError || RepoStatusRules.NudgeLine(answer.Value.Status) is not { Length: > 0 } line ? 0 : await output.Text(line + "\n", 0).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return 0;
        }
    }

    private async Task<int> Plain(GetRepoStatusRequest request, bool json, CancellationToken ct)
    {
        var answer = await services.GetRequiredService<IService<GetRepoStatusRequest, GetRepoStatusResponse>>().Handle(request, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            _ = await output.Error(answer.Error).ConfigureAwait(false);
            return 2;
        }

        var status = answer.Value.Status;
        return json
            ? await output.Json(status, RepoStatusRules.ExitCode(status)).ConfigureAwait(false)
            : await output.Text(RepoStatusRules.Text(status) + "\n", RepoStatusRules.ExitCode(status)).ConfigureAwait(false);
    }
}
