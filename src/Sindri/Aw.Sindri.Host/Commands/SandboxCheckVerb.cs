using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Features.CheckSandbox;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox check [--json]' (sandbox spec §8): the same checks as the Check again card, from the terminal: every blocker with its fix, or the Steps a Check again would resume.
/// A query through the running control plane, which never starts it: exit 1 SND-API-001 when it is down. Read-only: nothing is resumed. Exit 0 ready, 1 something blocks a Step.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class SandboxCheckVerb(ControlPlaneClient client, Output output)
{
    internal const string Usage = "usage: sindri sandbox check [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (args.Any(static a => a != "--json"))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'{args.First(static a => a != "--json")}' isn't an argument of this command ({Usage})")).ConfigureAwait(false);
        }

        var answer = await client.Get<CheckSandboxResponse>("/sindri/sandbox/check", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Value.Ready ? 0 : 1;
        return args.Contains("--json", StringComparer.Ordinal)
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(Render(answer.Value.Value), code).ConfigureAwait(false);
    }

    /// <summary>The check as text: a blocker is its sentence and, on the next line, the command to copy; a ready box lists what a Check again would resume.</summary>
    /// <param name="response">The checks' answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(CheckSandboxResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = new StringBuilder();
        foreach (var blocker in response.Blockers)
        {
            _ = text.Append(blocker.Quiet ? "note     " : "blocked  ").AppendLine(blocker.Text);
            _ = blocker.Detail is { } detail ? text.Append("         saw: ").AppendLine(detail) : text;
            _ = blocker.Command is { } command ? text.Append("         ").AppendLine(command) : text;
        }

        if (response.Ready)
        {
            _ = text.AppendLine(response.ReadyToResume.Count == 0
                ? "Ready. Nothing is waiting on the sandbox."
                : $"Ready. Check again would resume {response.ReadyToResume.Count} Step{(response.ReadyToResume.Count == 1 ? string.Empty : "s")}: {string.Join(", ", response.ReadyToResume.Select(static i => i.Value))}");
        }

        return text.ToString();
    }
}
