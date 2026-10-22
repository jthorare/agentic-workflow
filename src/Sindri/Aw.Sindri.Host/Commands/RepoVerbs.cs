using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri repo add &lt;path&gt; [--name &lt;name&gt;] [--detach] [--json]' (setup-once spec §7.4; 'sindri-app repo add …' passes it through): the terminal form of the app's Add repo, which calls the same route. A relative path is made
/// absolute from the working directory (the route takes only absolute paths). It starts the AddRepo job and follows it as 'context map build' does (Ctrl-C cancels the job, a second Ctrl-C leaves);
/// --detach prints the job id and returns. Exit codes are the follower's: 0 added, 1 failed, cancelled, left or Sindri not running, 2 refused or invalid (SND-SETUP-001 and 002 are refusals).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="follower">Follows the job the start answers.</param>
internal sealed class RepoVerbs(ControlPlaneClient client, Output output, JobFollower follower)
{
    internal const string Usage = "repo add <path> [--name <name>] [--detach] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(args);
        var line = Parse(args);
        if (line is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'repo {string.Join(' ', args)}' isn't this command: {Usage}")).ConfigureAwait(false);
        }

        var started = await client.Post<AddRepoRequest, JobStartedResponse>("/sindri/setup/repos", new AddRepoRequest(line.Path, line.Name), interrupt).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        var answer = started.Value;
        if (!line.Detach)
        {
            return await follower.Follow(answer.Value, line.Json, interrupt, again, Describe).ConfigureAwait(false);
        }

        return line.Json
            ? await output.Raw(answer.Body, 0).ConfigureAwait(false)
            : await output.Text($"job {answer.Value.JobId.Value} started\n", 0).ConfigureAwait(false);
    }

    /// <summary>The final job's one line for the owner: the repo is added and the profile waits for the approval; nothing when the result isn't there or doesn't read.</summary>
    private string? Describe(JobView job)
    {
        if (job.Result is not { } result)
        {
            return null;
        }

        var read = output.Read<AddedRepo>(System.Text.Encoding.UTF8.GetBytes(result.GetRawText()));
        return read.IsSuccess ? $"added {read.Value.Repo.Value}; your profile waits for your approval: sindri profile approve\n" : null;
    }

    // add <path> and the flags, in any order; null for anything else.
    private static RepoLine? Parse(string[] args)
    {
        if (args is not ["add", .. var rest])
        {
            return null;
        }

        var (words, flags, name) = Split(rest);
        return words.Count == 1 && flags.TrueForAll(Known)
            ? new RepoLine(Absolute(words[0]), name, flags.Contains("--detach", StringComparer.Ordinal), flags.Contains("--json", StringComparer.Ordinal))
            : null;
    }

    private static (List<string> Words, List<string> Flags, string? Name) Split(string[] rest)
    {
        List<string> words = [];
        List<string> flags = [];
        string? name = null;
        for (var i = 0; i < rest.Length; i++)
        {
            if (string.Equals(rest[i], "--name", StringComparison.Ordinal) && i + 1 < rest.Length)
            {
                name = rest[++i];
            }
            else if (rest[i].StartsWith("--", StringComparison.Ordinal))
            {
                flags.Add(rest[i]);
            }
            else
            {
                words.Add(rest[i]);
            }
        }

        return (words, flags, name);
    }

    private static bool Known(string flag) => string.Equals(flag, "--detach", StringComparison.Ordinal) || string.Equals(flag, "--json", StringComparison.Ordinal);

    // The route takes absolute paths only; '~' is the user's home, a relative path is from the working directory.
    private static string Absolute(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..])
            : Path.GetFullPath(path);

    private sealed record RepoLine(string Path, string? Name, bool Detach, bool Json);
}
