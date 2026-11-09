using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Handoff;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateHandoffItem;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri handoff send &lt;title&gt; [--source S] [--repo R] [--branch B] [--prompt T | --prompt-file F] [--offer ID] [--link URL | --link LABEL=URL]... [--label L]... [--json] [--no-start]'
/// (#2258 PR 12): a client of POST /sindri/handoff that makes an item with its handoff payload. Exit 0 made, 1 the control plane isn't running, 2 refused or invalid.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not.</param>
internal sealed class HandoffVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>> ensureRunning)
{
    internal const string Usage = "handoff send <title> [--source S] [--repo R] [--branch B] [--prompt T | --prompt-file F] [--offer ID] [--link URL | --link LABEL=URL]... [--label L]... [--json] [--no-start]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = await Parse(args, ct).ConfigureAwait(false);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (request, json, noStart) = parsed.Value;
        if (await ensureRunning(!noStart, ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var made = await client.Post<CreateHandoffItemRequest, HandoffItemCreatedResponse>("/sindri/handoff", request, ct).ConfigureAwait(false);
        if (made.IsError)
        {
            return await output.Error(made.Error).ConfigureAwait(false);
        }

        var response = made.Value.Value;
        return json
            ? await output.Raw(made.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{response.ItemId.Value} handed off (offer {response.OfferKey}, sequence {response.Accepted.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private static async Task<Result<(CreateHandoffItemRequest Request, bool Json, bool NoStart)>> Parse(string[] args, CancellationToken ct)
    {
        if (args is not ["send", .. var rest])
        {
            return SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a handoff command: {Usage}");
        }

        var read = Read(rest);
        if (read.IsError)
        {
            return read.Error;
        }

        var flags = read.Value;
        if (flags.Title.Count == 0)
        {
            return SindriErrors.InvalidInput.With($"handoff send needs a title: {Usage}");
        }

        var repo = flags.Values.TryGetValue("--repo", out var repoText) ? RepoOf(repoText) : (RepoName?)null;
        var prompt = await Prompt(flags, ct).ConfigureAwait(false);
        if (repo.IsError || prompt.IsError)
        {
            return repo.IsError ? repo.Error : prompt.Error;
        }

        var request = new CreateHandoffItemRequest(string.Join(' ', flags.Title), flags.Values.GetValueOrDefault("--source", "local"), flags.Values.GetValueOrDefault("--offer"), repo.Value,
            flags.Values.GetValueOrDefault("--branch"), prompt.Value, flags.Links.Count == 0 ? null : flags.Links.ConvertAll(LinkOf), flags.Labels.Count == 0 ? null : flags.Labels);
        return (request, flags.Json, flags.NoStart);
    }

    private static Result<Flags> Read(string[] rest)
    {
        var flags = new Flags();
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--json":
                    flags.Json = true;
                    break;
                case "--no-start":
                    flags.NoStart = true;
                    break;
                case "--source" or "--repo" or "--branch" or "--prompt" or "--prompt-file" or "--offer" or "--link" or "--label":
                    if (i + 1 >= rest.Length)
                    {
                        return SindriErrors.InvalidInput.With($"{rest[i]} needs a value: {Usage}");
                    }

                    flags.Take(rest[i], rest[++i]);
                    break;
                default:
                    flags.Title.Add(rest[i]);
                    break;
            }
        }

        return flags.Values.ContainsKey("--prompt") && flags.Values.ContainsKey("--prompt-file")
            ? SindriErrors.InvalidInput.With("give --prompt or --prompt-file, not both")
            : flags;
    }

    // why: a bare URL is its own label; "Ticket=https://..." splits at the first '=' before "://" so a URL's own query '=' never splits it
    private static HandoffLink LinkOf(string text)
    {
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        var equals = text.IndexOf('=', StringComparison.Ordinal);
        return equals > 0 && (scheme < 0 || equals < scheme) ? new HandoffLink(text[..equals], text[(equals + 1)..]) : new HandoffLink(text, text);
    }

    private static Result<RepoName?> RepoOf(string text) =>
        RepoName.TryFrom(text, out var name) ? name : SindriErrors.InvalidInput.With($"'{text}' isn't a repo name");

    private static async Task<Result<string?>> Prompt(Flags flags, CancellationToken ct)
    {
        if (!flags.Values.TryGetValue("--prompt-file", out var path))
        {
            return flags.Values.GetValueOrDefault("--prompt");
        }

        try
        {
            return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (IOException e)
        {
            return SindriErrors.InvalidInput.With($"can't read --prompt-file {path}: {e.Message}");
        }
    }

    private sealed class Flags
    {
        internal List<string> Title { get; } = [];

        internal Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        internal List<string> Links { get; } = [];

        internal List<string> Labels { get; } = [];

        internal bool Json { get; set; }

        internal bool NoStart { get; set; }

        internal void Take(string flag, string value)
        {
            switch (flag)
            {
                case "--link":
                    Links.Add(value);
                    break;
                case "--label":
                    Labels.Add(value);
                    break;
                default:
                    Values[flag] = value;
                    break;
            }
        }
    }
}
