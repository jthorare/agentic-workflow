using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListDenials;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox denials [--run &lt;launchId&gt;] [--since &lt;time&gt;] [--json]' (sandbox spec §3.6): what the sandbox blocked, grouped by target and rule with the fix each kind allows.
/// A query through the running control plane, which never starts it: exit 1 SND-API-001 when it is down. '--since' is a time (2026-10-27T09:00:00Z) or an age (30m, 6h, 2d).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="clock">The clock, for an age.</param>
internal sealed class SandboxDenialsVerb(ControlPlaneClient client, Output output, TimeProvider clock)
{
    internal const string Usage = "usage: sindri sandbox denials [--run <launchId>] [--since <time|30m|6h|2d>] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var (query, json, problem) = Parse(args);
        if (problem is not null)
        {
            return await Invalid(problem).ConfigureAwait(false);
        }

        var answer = await client.Get<ListDenialsResponse>("/sindri/sandbox/denials" + query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(DenialsRenderer.Render(answer.Value.Value.Launches), 0).ConfigureAwait(false);
    }

    /// <summary>The query string ('' or '?launch=..&amp;since=..'), whether --json was given, and the first problem with the arguments.</summary>
    private (string Query, bool Json, string? Problem) Parse(string[] args)
    {
        var query = new List<string>();
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            var value = args.ElementAtOrDefault(i + 1) ?? string.Empty;
            if (args[i] == "--json")
            {
                json = true;
                continue;
            }

            var part = args[i] switch
            {
                "--run" => LaunchId.TryParse(value, provider: null, out _) ? "launch=" + Uri.EscapeDataString(value) : null,
                "--since" => SinceQuery(value),
                _ => null,
            };
            if (part is null)
            {
                return (string.Empty, false, $"'{args[i]}' with '{value}' isn't a launch id (LCH-…), a time such as 2026-10-27T09:00:00Z or an age such as 6h, or an argument of this command");
            }

            query.Add(part);
            i++;
        }

        return (query.Count == 0 ? string.Empty : "?" + string.Join('&', query), json, null);
    }

    private string? SinceQuery(string text) =>
        TryTime(text, out var at) ? "since=" + Uri.EscapeDataString(at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)) : null;

    private bool TryTime(string text, out DateTimeOffset at)
    {
        if (text.Length >= 2 && text[..^1] is var digits && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            TimeSpan? age = text[^1] switch
            {
                'm' => TimeSpan.FromMinutes(n),
                'h' => TimeSpan.FromHours(n),
                'd' => TimeSpan.FromDays(n),
                _ => null,
            };
            if (age is { } span)
            {
                at = clock.GetUtcNow() - span;
                return true;
            }
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out at);
    }

    private async Task<int> Invalid(string message) =>
        await output.Error(SindriErrors.InvalidInput.With($"{message} ({Usage})")).ConfigureAwait(false);
}
