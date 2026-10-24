using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>'sindri scrub' as Program dispatches it, over a recording service: the routes, what each one passes the service, and the exit codes (cutover spec §6.2 item 1). No control plane.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ScrubCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private readonly TextReader _consoleIn = Console.In;

    public ScrubCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        Console.SetIn(_consoleIn);
        _home.Dispose();
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(Recorder service, params string[] args) =>
        CommandTable.Run(args, Environment, TimeProvider.System, services => services.AddScoped<IService<ScrubRequest, ScrubResponse>>(_ => service));

    [Fact, Trait("Lane", "integration")]
    public async Task Bare_scrub_passes_standard_input_and_prints_what_comes_back()
    {
        Console.SetIn(new StringReader("hello\n"));
        var service = new Recorder(new ScrubResponse("[scrubbed]\n"));

        var code = await Run(service, "scrub");

        Assert.Equal((0, "[scrubbed]\n"), (code, _out.ToString()));
        Assert.Equal(new ScrubRequest(ScrubMode.Stdin, Text: "hello\n"), service.Seen);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Staged_exits_1_with_the_SND_SCRUB_002_line_on_a_finding()
    {
        var service = new Recorder(SindriErrors.ScrubStagedSecrets.With("refused: 1 likely secret(s) in staged changes:\nf.txt:1 aws-access-key"));

        var code = await Run(service, "scrub", "--staged");

        Assert.Equal(1, code);
        Assert.StartsWith("SND-SCRUB-002 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(new ScrubRequest(ScrubMode.Staged), service.Seen);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Staged_with_nothing_found_exits_0()
    {
        var code = await Run(new Recorder(new ScrubResponse("No secrets in staged changes.\n")), "scrub", "--staged");

        Assert.Equal(0, code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Install_passes_repo_and_bin_and_prints_where_the_hook_went()
    {
        var service = new Recorder(new ScrubResponse("Installed the secret-scan pre-commit hook at /r/.git/hooks/pre-commit.\n", "/r/.git/hooks/pre-commit"));

        var code = await Run(service, "scrub", "--install-pre-commit", "--repo", "/r", "--bin", "/stable/sindri");

        Assert.Equal(0, code);
        Assert.Equal(new ScrubRequest(ScrubMode.InstallPreCommit, Repo: "/r", Bin: "/stable/sindri"), service.Seen);
        Assert.Contains("/r/.git/hooks/pre-commit", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Install_without_flags_leaves_repo_and_bin_null()
    {
        var service = new Recorder(new ScrubResponse("ok\n"));

        _ = await Run(service, "scrub", "--install-pre-commit");

        Assert.Equal(new ScrubRequest(ScrubMode.InstallPreCommit), service.Seen);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("SND-SCRUB-003")]
    [InlineData("SND-SCRUB-004")]
    [InlineData("SND-SCRUB-005")]
    public async Task The_other_refusals_print_their_code_and_exit_2(string code)
    {
        var error = SindriErrors.All.Single(e => e.Code == code).With("details");

        var exit = await Run(new Recorder(error), "scrub", "--install-pre-commit");

        Assert.Equal(2, exit);
        Assert.StartsWith(code + " ", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--frobnicate")]
    [InlineData("--staged", "--install-pre-commit")]
    public async Task An_unknown_flag_is_SND_CLI_001_and_never_reaches_the_service(params string[] args)
    {
        var service = new Recorder(new ScrubResponse("x"));

        var code = await Run(service, ["scrub", .. args]);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-CLI-001 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(service.Seen);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_four_scrub_refusals_are_in_the_error_catalogue_and_the_guard_proof_code_is_unchanged()
    {
        var codes = SindriErrors.All.Select(static e => e.Code).ToList();

        Assert.Subset(codes.ToHashSet(StringComparer.Ordinal), new HashSet<string>(["SND-SCRUB-002", "SND-SCRUB-003", "SND-SCRUB-004", "SND-SCRUB-005"], StringComparer.Ordinal));
        Assert.Equal("SND-SCRUB-002", SindriErrors.ScrubStagedSecrets.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Help_lists_the_scrub_verb_and_its_exit_code()
    {
        var line = Assert.Single(Usage.Text.Split('\n'), static l => l.TrimStart().StartsWith("sindri scrub ", StringComparison.Ordinal));

        Assert.Contains("--install-pre-commit", line, StringComparison.Ordinal);
        Assert.Contains("SND-SCRUB-002", line, StringComparison.Ordinal);
    }

    private sealed class Recorder : IService<ScrubRequest, ScrubResponse>
    {
        private readonly Result<ScrubResponse> _answer;

        public Recorder(ScrubResponse answer)
        {
            _answer = answer;
        }

        public Recorder(Error error)
        {
            _answer = error;
        }

        public ScrubRequest? Seen { get; private set; }

        public ValueTask<Result<ScrubResponse>> Handle(ScrubRequest request, CancellationToken cancellationToken)
        {
            Seen = request;
            return ValueTask.FromResult(_answer);
        }
    }
}
