using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The <c lang="text">sindri launch</c> verbs over a scripted setup: flags, text, JSON and exit codes.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchVerbsTests : IDisposable
{
    private static readonly LaunchShowReport _shown = new("kitty (exec-template)", "detected", "/home/u/launcher.json",
        [
            new(AgentId.From("claude-code"), CatalogSource.Builtin, "claude", "/usr/bin/claude", "sha256:aa", StepOk: true, ParkLine: null),
            new(AgentId.From("aider"), CatalogSource.Added, "aider", ResolvedBin: null, "sha256:bb", StepOk: false, "Steps on `aider` will park: approve `agents.aider.stepArgvHash: sha256:bb`"),
        ],
        []);

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
    }

    private LaunchVerbs Verbs(ScriptedLaunchSetup setup) => new(setup, new Output(_out, _err, AwJson.CreateOptions(SindriJsonContext.Default)));

    private static ScriptedLaunchSetup Setup(Result<LaunchInitReport>? init = null) =>
        new(init ?? new LaunchInitReport("kitty", "from $TERM_PROGRAM", "/home/u/launcher.json", "{}"), _shown);

    [Fact]
    public async Task Show_json_lists_source_bin_argvHash_and_step_ok()
    {
        var code = await Verbs(Setup()).Run(["show", "--json"], CancellationToken.None);

        using var json = JsonDocument.Parse(_out.ToString());
        var aider = json.RootElement.GetProperty("entries")[1];
        Expect.Equal(0, code);
        Expect.Equal("added", aider.GetProperty("source").GetString());
        Expect.Equal("aider", aider.GetProperty("bin").GetString());
        Expect.Equal("sha256:bb", aider.GetProperty("argvHash").GetString());
        Expect.True(!aider.GetProperty("stepOk").GetBoolean());
    }

    [Fact]
    public async Task Show_text_prints_the_will_park_line_for_an_unapproved_entry_and_not_for_an_approved_one()
    {
        var code = await Verbs(Setup()).Run(["show"], CancellationToken.None);

        var text = _out.ToString();
        Expect.Equal(0, code);
        Expect.True(text.Contains("Steps on `aider` will park: approve `agents.aider.stepArgvHash: sha256:bb`", StringComparison.Ordinal));
        Expect.True(!text.Contains("Steps on `claude-code`", StringComparison.Ordinal));
        Expect.True(text.Contains("Terminal: kitty (exec-template)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Init_says_what_it_chose_and_where()
    {
        var code = await Verbs(Setup()).Run(["init"], CancellationToken.None);

        Expect.Equal(0, code);
        Expect.True(_out.ToString().Contains("Wrote /home/u/launcher.json: kitty (from $TERM_PROGRAM). The next open uses it.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Init_stdout_prints_only_the_file_text()
    {
        var setup = Setup(new LaunchInitReport("kitty", "from $TERM_PROGRAM", Path: null, """{"schema":1}"""));

        var code = await Verbs(setup).Run(["init", "--stdout", "--viewer", "kitty", "--force"], CancellationToken.None);

        Expect.Equal(0, code);
        Expect.Equal("""{"schema":1}""" + Environment.NewLine, _out.ToString());
        Expect.Equal(new LaunchInitRequest("kitty", Force: true, ToStdout: true), setup.Asked);
    }

    [Fact]
    public async Task Init_refused_because_the_file_exists_exits_1()
    {
        var code = await Verbs(Setup(KernelErrors.Conflict.With("launcher.json exists (fix: --force)"))).Run(["init"], CancellationToken.None);

        Expect.Equal(1, code);
        Expect.True(_err.ToString().Contains("--force", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unknown_flag_or_verb_exits_2()
    {
        Expect.Equal(2, await Verbs(Setup()).Run(["init", "--bogus"], CancellationToken.None));
        Expect.Equal(2, await Verbs(Setup()).Run(["wat"], CancellationToken.None));
        Expect.Equal(2, await Verbs(Setup()).Run([], CancellationToken.None));
    }
}
