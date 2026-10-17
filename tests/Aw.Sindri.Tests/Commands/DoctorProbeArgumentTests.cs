using System.Text.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The argument shapes 'sindri doctor --probe' accepts, through CommandTable: which ones are a probe and which are a usage error.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class DoctorProbeArgumentTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public DoctorProbeArgumentTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private Task<int> Run(params string[] args)
        => CommandTable.Run(
            args,
            static _ => null,
            TimeProvider.System,
            services => services
                .AddSingleton<IHarnessVersions>(new FakeHarnessVersions())
                .AddSingleton<ISindriHome>(new FakeSindriHome(_home.Path)));

    [Theory, Trait("Lane", "integration")]
    [InlineData("doctor", "--probe")]
    [InlineData("doctor", "--probe", "--json")]
    [InlineData("doctor", "--probe", "--harness", "claude-code")]
    [InlineData("doctor", "--probe", "--harness", "claude-code", "--json")]
    public async Task Doctor_probe_prints_the_report_as_json_and_exits_0(params string[] args)
    {
        var code = await Run(args);

        Assert.Equal(0, code);
        using var report = JsonDocument.Parse(_out.ToString());
        Assert.True(report.RootElement.TryGetProperty("harness", out var harness));
        if (args.Contains("--harness"))
        {
            Assert.Equal("claude-code", harness.GetString());
        }
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("doctor", "--probe", "--bogus")]
    [InlineData("doctor", "--probe", "--harness")]
    [InlineData("doctor", "--probe", "--harness", "claude-code", "--bogus")]
    [InlineData("doctor", "--probe", "--json", "--harness", "claude-code")]
    public async Task Doctor_probe_with_a_malformed_argument_is_a_usage_error_exit_2(params string[] args)
    {
        var code = await Run(args);

        Assert.Equal(2, code);
        Assert.Contains(SindriErrors.UnknownVerb.Code, _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
    }
}
