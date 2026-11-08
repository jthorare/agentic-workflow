using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri connection add|list|show|test|remove [--client-id ID] [--key-env NAME] [--kind K] [--preset P]' (connections PR 10a): a client of the /sindri/connections routes.
/// A session may run every verb (owner decision D2). 'add' for an oauth connection prints the consent URL as soon as it has it, then waits for the callback.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ConnectionVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    private const string Root = "/sindri/connections";
    private static readonly TimeSpan _signInWait = TimeSpan.FromMinutes(5.5);   // the sign-in's own limit is 5 minutes
    private static readonly TimeSpan _pollEvery = TimeSpan.FromSeconds(1);

    private readonly ControlPlaneClient _client = client;
    private readonly Output _output = output;
    private readonly Func<bool, CancellationToken, Task<Error?>>? _ensureRunning = ensureRunning;

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            return await _output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = _ensureRunning is null ? null : await _ensureRunning(!command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await _output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            "list" => await List(command, ct).ConfigureAwait(false),
            "show" => await Show(command, ct).ConfigureAwait(false),
            "test" => await Test(command, ct).ConfigureAwait(false),
            "remove" => await Remove(command, ct).ConfigureAwait(false),
            _ => await Add(command, ct).ConfigureAwait(false),
        };
    }

    private async Task<int> List(Parsed command, CancellationToken ct)
    {
        var answer = await _client.Get<ListConnectionsResponse>(Root, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await _output.Error(answer.Error).ConfigureAwait(false);
        }

        if (command.Json)
        {
            return await _output.Raw(answer.Value.Body, 0).ConfigureAwait(false);
        }

        var text = new StringBuilder();
        foreach (var view in answer.Value.Value.Connections)
        {
            _ = text.AppendLine(Line(view));
        }

        return await _output.Text(answer.Value.Value.Connections.Count == 0 ? "No connections. Add one with: sindri connection add <service:label> --preset <preset> --client-id <id>\n" : text.ToString(), 0).ConfigureAwait(false);
    }

    private async Task<int> Show(Parsed command, CancellationToken ct)
    {
        var answer = await _client.Get<GetConnectionResponse>(Route(command.Name), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await _output.Error(answer.Error).ConfigureAwait(false);
        }

        return command.Json
            ? await _output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await _output.Text(Detail(answer.Value.Value.Connection), 0).ConfigureAwait(false);
    }

    private async Task<int> Test(Parsed command, CancellationToken ct)
    {
        var answer = await _client.Post<TestConnectionRequest, TestConnectionResponse>(Route(command.Name) + "/test", body: null, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await _output.Error(answer.Error).ConfigureAwait(false);
        }

        var tested = answer.Value.Value;
        var code = tested.Ok ? 0 : 1;
        return command.Json
            ? await _output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await _output.Text(tested.Ok ? $"{tested.Connection.Name}: the provider accepted the credential.\n" : $"{tested.Connection.Name}: {tested.Reason}\n", code).ConfigureAwait(false);
    }

    private async Task<int> Remove(Parsed command, CancellationToken ct)
    {
        var answer = await _client.Delete<RemoveConnectionResponse>(Route(command.Name), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await _output.Error(answer.Error).ConfigureAwait(false);
        }

        var removed = answer.Value.Value;
        return command.Json
            ? await _output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await _output.Text($"Removed {removed.Name}{(removed.SecretDeleted ? " and its stored secret" : string.Empty)}.\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Add(Parsed command, CancellationToken ct)
    {
        // why: the CLI asks for the consent URL first (Wait false), prints it, then polls for the row, so the owner sees the URL even when no browser opened
        var request = new AddConnectionRequest(command.Name, command.Preset, command.Kind, command.Client, command.KeyEnv, command.Scopes, Wait: false);
        var added = await _client.Post<AddConnectionRequest, AddConnectionResponse>(Root, request, ct).ConfigureAwait(false);
        if (added.IsError)
        {
            return await _output.Error(added.Error).ConfigureAwait(false);
        }

        var response = added.Value.Value;
        if (response.ConsentUrl is null)
        {
            return command.Json
                ? await _output.Raw(added.Value.Body, 0).ConfigureAwait(false)
                : await _output.Text($"Added {response.Connection.Name} ({response.Connection.Kind}), {response.Connection.Status}.\n", 0).ConfigureAwait(false);
        }

        await _output.Notice("Sign in at this address if your browser did not open:").ConfigureAwait(false);
        await _output.Notice(response.ConsentUrl).ConfigureAwait(false);
        var finished = await WaitForRow(command.Name, ct).ConfigureAwait(false);
        if (finished.IsError)
        {
            return await _output.Error(finished.Error).ConfigureAwait(false);
        }

        return command.Json
            ? await _output.Raw(finished.Value.Body, 0).ConfigureAwait(false)
            : await _output.Text($"Added {finished.Value.Value.Connection.Name} ({finished.Value.Value.Connection.Kind}), {finished.Value.Value.Connection.Status}.\n", 0).ConfigureAwait(false);
    }

    // The row is written only after the callback landed and the tokens are stored, so "found" means signed in.
    private async Task<Result<ApiAnswer<GetConnectionResponse>>> WaitForRow(string name, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(_signInWait);
        try
        {
            while (true)
            {
                var read = await _client.Get<GetConnectionResponse>(Route(name), limit.Token).ConfigureAwait(false);
                if (read.IsSuccess || read.Error.Code != SindriErrors.ConnectionNotFound.Code)
                {
                    return read;
                }

                await Task.Delay(_pollEvery, limit.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return SindriErrors.ConnectionSignInTimeout.With("no sign-in finished in 5 minutes (consent denied or the page was closed); run the command again");
        }
    }

    private static string Route(string name) => Root + "/" + Uri.EscapeDataString(name);

    private static string Line(ConnectionView view) =>
        $"{view.Name}  {view.Kind}  {view.Status}{(view.StatusReason is null ? string.Empty : "  " + view.StatusReason)}";

    private static string Detail(ConnectionView view)
    {
        var text = new StringBuilder();
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"name: {view.Name}")
            .AppendLine(CultureInfo.InvariantCulture, $"kind: {view.Kind}")
            .AppendLine(CultureInfo.InvariantCulture, $"preset: {view.Preset ?? "none"}")
            .AppendLine(CultureInfo.InvariantCulture, $"status: {view.Status}{(view.StatusReason is null ? string.Empty : " (" + view.StatusReason + ")")}")
            .AppendLine(CultureInfo.InvariantCulture, $"secret stored: {(view.HasSecret ? "yes" : "no")}");
        if (view.LastRefreshedAt is { } refreshed)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"last refreshed: {refreshed:O}");
        }

        foreach (var pair in view.Config.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"{pair.Key}: {pair.Value}");
        }

        return text.ToString();
    }

    private static Result<Parsed> Parse(string[] args)
    {
        var verb = args.Length > 1 ? args[1] : "list";
        if (args[0] == "connections" && args.Length == 1)
        {
            verb = "list";
        }

        if (verb is not ("add" or "list" or "show" or "test" or "remove"))
        {
            return SindriErrors.InvalidInput.With("usage: sindri connection add|list|show|test|remove (fix: sindri help)");
        }

        string? name = null;
        string? preset = null;
        string? kind = null;
        string? client = null;
        string? keyEnv = null;
        var scopes = new List<string>();
        var json = false;
        var noStart = false;
        var rest = args.Skip(args[0] == "connections" && args.Length == 1 ? 1 : 2).ToArray();
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--no-start":
                    noStart = true;
                    break;
                case "--preset" or "--kind" or "--client-id" or "--key-env" or "--scope":
                    var flag = rest[i];
                    var value = i + 1 < rest.Length ? rest[++i] : null;
                    if (value is null)
                    {
                        return SindriErrors.InvalidInput.With($"{flag} needs a value");
                    }

                    switch (flag)
                    {
                        case "--preset": preset = value; break;
                        case "--kind": kind = value; break;
                        case "--client-id": client = value; break;
                        case "--key-env": keyEnv = value; break;
                        default: scopes.Add(value); break;
                    }

                    break;
                case var flagLike when flagLike.StartsWith('-'):
                    return SindriErrors.InvalidInput.With($"unknown flag {flagLike} (fix: sindri help)");
                case var positional when name is null:
                    name = positional;
                    break;
                default:
                    return SindriErrors.InvalidInput.With("one connection name at most (service:label)");
            }
        }

        if (verb != "list" && name is null)
        {
            return SindriErrors.InvalidInput.With($"sindri connection {verb} needs a name (service:label)");
        }

        return new Parsed(verb, name ?? string.Empty, preset, kind, client, keyEnv, scopes, json, noStart);
    }

    private sealed record Parsed(string Verb, string Name, string? Preset, string? Kind, string? Client, string? KeyEnv, IReadOnlyList<string> Scopes, bool Json, bool NoStart);
}
