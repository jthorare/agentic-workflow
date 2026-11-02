using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox explain &lt;path|host|command&gt; [--plan &lt;file&gt;] [--json]' (sandbox spec §3.6): why a path, a host or a command is allowed or blocked, and how the owner can change it.
/// It runs before any service is built and reads only the file '--plan' or AW_SANDBOX_PLAN names: never the state directory, the ledger, telemetry or the control plane,
/// so it answers offline and with the state directory denied. Exit 0 answered, 2 usage or no readable plan.
/// </summary>
internal static class SandboxExplainVerb
{
    internal const string Usage = "usage: sindri sandbox explain <path|host|command> [--plan <file>] [--json]";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    internal static async Task<int> Run(string[] args, Func<string, string?> environment, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        var (query, planArg, json, bad) = Parse(args);
        if (bad is not null)
        {
            return await Fail(stderr, bad).ConfigureAwait(false);
        }

        var file = planArg ?? environment(SandboxEnvironment.Plan);
        if (string.IsNullOrWhiteSpace(file))
        {
            return await Fail(stderr, $"there is no sandbox plan file to read: this is not a sandboxed session (fix: pass --plan <file>, or run it inside a Step or `sindri open` session) ({Usage})").ConfigureAwait(false);
        }

        var (plan, problem) = await ReadPlan(file, ct).ConfigureAwait(false);
        if (plan is null)
        {
            return await Fail(stderr, problem ?? "the sandbox plan file can't be read").ConfigureAwait(false);
        }

        var answer = SandboxPlanRenderer.Explain(plan, query);
        await stdout.WriteAsync(json ? AsJson(query, answer) : answer.EndsWith('\n') ? answer : answer + "\n").ConfigureAwait(false);
        return 0;
    }

    private static (string Query, string? Plan, bool Json, string? Problem) Parse(string[] args)
    {
        var words = new List<string>();
        string? plan = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--json")
            {
                json = true;
            }
            else if (args[i] == "--plan" && i + 1 < args.Length)
            {
                plan = args[++i];
            }
            else if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return (string.Empty, null, false, $"'{args[i]}' isn't an argument of this command, or it needs a value ({Usage})");
            }
            else
            {
                words.Add(args[i]);
            }
        }

        return words.Count == 0
            ? (string.Empty, null, false, $"explain needs a path, a host or a command ({Usage})")
            : (string.Join(' ', words), plan, json, null);
    }

    private static async Task<(SandboxPlan? Plan, string? Problem)> ReadPlan(string file, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, $"the sandbox plan file {file} can't be read: {e.Message} (fix: check AW_SANDBOX_PLAN, or pass --plan <file>)");
        }

        var parsed = AwJson.Deserialize<SandboxPlan>(bytes, _json);
        return parsed.IsSuccess ? (parsed.Value, null) : (null, $"the sandbox plan file {file} isn't a plan this build reads: {parsed.Error.Message}");
    }

    private static string AsJson(string query, string answer)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            writer.WriteString("answer", answer.TrimEnd('\n'));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static async Task<int> Fail(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync($"{SindriErrors.InvalidInput.Code} {message}").ConfigureAwait(false);
        return 2;
    }
}
