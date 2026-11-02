using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

public sealed class SecretPointerEnvironmentTests
{
    private const string Fixture = "fixture-wrapped-token";

    private static readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal)
    {
        ["EXAMPLE_TOKEN"] = Fixture,
        ["AW_STATE_DIR"] = "/tmp/aw-state",
        ["LINEAR_API_KEY"] = "fixture-linear-key",
        ["keychain:sindri/example-api-key"] = "fixture-same-named-variable",
        ["on_rawsecret"] = "fixture-raw",
    };

    [Fact, Trait("Lane", "unit")]
    public void A_pointer_resolves_to_its_secret()
        => Assert.Equal(Fixture, Wrapped(new FakeToolRunner(ToolRun.Missing))("env:EXAMPLE_TOKEN"));

    [Fact, Trait("Lane", "unit")]
    public void A_keychain_pointer_resolves_through_the_tool()
        => Assert.Equal(Fixture, Wrapped(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, Fixture + "\n")))("keychain:sindri/example-api-key"));

    [Fact, Trait("Lane", "unit")]
    public void A_plain_name_reads_the_real_environment()
        => Assert.Equal("/tmp/aw-state", Wrapped(new FakeToolRunner(ToolRun.Missing))("AW_STATE_DIR"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("keychain:sindri/example-api-key")]
    [InlineData("env:MISSING_TOKEN")]
    [InlineData("vault:example")]
    [InlineData("env:bad-name")]
    public void A_failing_pointer_is_null_and_never_falls_through(string name)
        => Assert.Null(Wrapped(new FakeToolRunner(ToolRun.Missing))(name));

    [Fact, Trait("Lane", "unit")]
    public void The_resolver_sees_only_the_parsed_pointer()
    {
        var seen = new List<SecretPointer>();
        var wrapped = SecretPointerEnvironment.Over(Read, new RecordingSecrets(seen));

        _ = wrapped("op:Work/Example/credential");

        Assert.Equal(SecretScheme.OnePassword, Assert.Single(seen).Scheme);
    }

    [Fact, Trait("Lane", "unit")]
    public void Composition_wraps_the_environment_it_is_given()
        => Assert.Equal(Fixture, Composition.ContextEnvironment(Read)("env:EXAMPLE_TOKEN"));

    private static string? Read(string name) => _environment.GetValueOrDefault(name);

    private static Func<string, string?> Wrapped(FakeToolRunner runner)
        => SecretPointerEnvironment.Over(Read, new PointerSecrets(Read, runner, KeychainPlatform.MacOS));

    private sealed class RecordingSecrets(List<SecretPointer> seen) : ISecretPointers
    {
        public Result<string> Resolve(SecretPointer secretPointer)
        {
            seen.Add(secretPointer);
            return SindriErrors.SecretUnresolved.With("fixture");
        }
    }
}
