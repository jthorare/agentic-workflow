using System.Text.Json;
using Aw.Kernel.Telemetry;
using Aw.Meta.Host;
using Aw.Meta.Host.Commands;
using Aw.Meta.Tests.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>Runs aw-meta commands in process the way Program.cs does, over a temp state directory and a fake clock; each call is a fresh container, like a fresh process.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class CliHarness : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly Dictionary<string, string> _env = new(StringComparer.Ordinal);

    internal CliHarness()
    {
        _env["AW_STATE_DIR"] = _root.Path;
        _env["AW_CLAUDE_SESSIONS_DIR"] = SessionsDirectory;
        _ = Directory.CreateDirectory(SessionsDirectory);
        _env["AW_TMUX"] = FailClosedTmux.Write(_root.Path);   // the real tmux (the live team's server) is never reachable from a test
    }

    /// <summary>The tmux program every run uses: a stub under the harness's temp directory.</summary>
    internal string Tmux => _env["AW_TMUX"];

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero));

    internal string SessionsDirectory => Path.Combine(_root.Path, "sessions");

    internal string File(string name) => Path.Combine(_root.Path, name);

    internal void SetEnv(string name, string value)
    {
        if (name == "AW_TMUX" && !Path.GetFullPath(value).StartsWith(_root.Path + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"AW_TMUX must be a stub under the test's temp directory, not '{value}': a test never reaches the real tmux.");
        }

        _env[name] = value;
    }

    internal string Env(string name) => _env[name];

    /// <summary>Changes the composed services of every later run before the container is built (a test's store decorator); null keeps them as Program.cs composes them.</summary>
    internal Action<IServiceCollection>? Services { get; set; }

    internal Task<CliResult> Run(params string[] args) => RunWithInput(string.Empty, args);

    internal async Task<CliResult> RunWithInput(string stdin, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var input = new StringReader(stdin);
        var (arguments, environment, problem) = TeamOption.Apply(args, name => _env.GetValueOrDefault(name));
        var io = new ConsoleIo(output, error, input, environment);
        if (problem.Length > 0)
        {
            return new CliResult(CommandOutput.Usage(io, problem, "[--team <name>] <command> [arguments]"), output.ToString(), error.ToString());
        }

        var services = Composition.Compose(new ServiceCollection(), io.Env, Clock);
        Services?.Invoke(services);
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            _ = provider.GetRequiredService<TelemetryRuntime>();
            var exit = await CommandTable.Run(arguments, provider, provider.GetRequiredService<JsonSerializerOptions>(), io, TestContext.Current.CancellationToken).ConfigureAwait(false);
            return new CliResult(exit, output.ToString(), error.ToString());
        }
    }

    public void Dispose() => _root.Dispose();
}
