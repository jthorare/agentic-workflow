using System.Text.Json;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'open' command: opens a pull request.</summary>
internal static class OpenCommand
{
    internal const string Name = "open";

    internal static readonly CommandSpec Spec = new(
        "open --repo <host/owner/name> --head <branch> --base <branch> --head-sha <sha> --title <text> [--body <text>] [--draft]",
        ["repo", "head", "base", "head-sha", "title"], ["body"], ["draft"]);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var result = await Binding.Repo(forge, line.Need("repo")).BindAsync(repo => forge.OpenPull(
            new OpenPullRequest(repo, line.Need("head"), line.Need("base"), line.Need("head-sha"), line.Need("title"), line["body"] ?? string.Empty, line.Flags.Contains("draft")),
            CancellationToken.None)).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
