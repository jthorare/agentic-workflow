using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.EndLearnMode;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox learn off [--repo &lt;repo&gt;] [--json]' (sandbox spec §3.6): ends network learn mode early for a repo, or every repo, through the running control plane (POST /sindri/sandbox/learn-off).
/// It only tightens, so there is no approval. It never starts the control plane (API spec §7). Exit 0 done; 1 when Sindri isn't running (SND-API-001); 2 on usage or a refusal (a launched session gets SND-AUTH-002).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class SandboxLearnVerbs(ControlPlaneClient client, Output output)
{
    internal const string Usage = "sandbox learn off [--repo <repo>] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (repo, json) = parsed.Value;
        var answer = await client.Post<EndLearnModeRequest, CommandAccepted>("/sindri/sandbox/learn-off", new EndLearnModeRequest(repo), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"learn mode ended for {(repo is { } r ? r.Value : "every repo")} (sequence {answer.Value.Value.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private static Kernel.Result<(RepoName? Repo, bool Json)> Parse(string[] args)
    {
        RepoName? repo = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--repo" when i + 1 < args.Length:
                    if (!RepoName.TryFrom(args[++i], out var name))
                    {
                        return SindriErrors.InvalidInput.With($"'{args[i]}' isn't a repo name ({Usage})");
                    }

                    repo = name;
                    break;
                default:
                    return SindriErrors.InvalidInput.With($"'{args[i]}' isn't an option of sandbox learn off ({Usage})");
            }
        }

        return (repo, json);
    }
}
