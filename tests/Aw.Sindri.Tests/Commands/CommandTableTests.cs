using Aw.Kernel.Services;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The verb table as Program runs it, with the console swapped for writers: the exit codes the doc promises for the
/// local verbs. It swaps the process's console, so it never runs beside another test.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CommandTableTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public CommandTableTests()
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

    private string ProfilePath => Path.Combine(_home.Path, "profile", "profile.yaml");

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_verb_exits_2_with_SND_CLI_001()
    {
        var code = await Run("frobnicate");

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-001 'frobnicate' is not a sindri command", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Index_embed_reaches_the_embed_verbs_not_the_index_read_verbs()
    {
        var code = await Run("index", "embed", "bogus");

        Assert.Equal(2, code);
        Assert.Contains("'index embed bogus' isn't an index embed command", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approving_a_malformed_hash_exits_2_with_SND_CLI_003()
    {
        var code = await Run("profile", "approve", "abc123");

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-003 'abc123' is not a profile hash (64 hex digits)", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("none", 2)]
    [InlineData("invalid", 2)]
    [InlineData("valid", 1)]
    [InlineData("approved", 0)]
    public async Task Profile_validate_exits_0_approved_1_unapproved_2_invalid(string state, int expected)
    {
        await Put(state);

        Assert.Equal(expected, await Run("profile", "validate"));
    }

    private async Task Put(string state)
    {
        if (state == "none")
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);   // why: the profile path has a parent
        await File.WriteAllTextAsync(ProfilePath, state == "invalid" ? "schemaVersion: 1\nuser: thor\nunknownKey: 1\n" : "schemaVersion: 1\nuser: thor\n",
            TestContext.Current.CancellationToken);
        if (state == "approved")
        {
            await Approve();
        }
    }

    /// <summary>Approves the live profile as 'sindri profile approve' does: under the lease, recorded in the ledger and saved.</summary>
    private async Task Approve()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = Composition.Compose(new ServiceCollection(), Environment, TimeProvider.System);
        NoTemporal.Use(collection);   // why: approving a profile needs the lease, not a real dev server (which would outlive the test)
        var provider = collection.BuildServiceProvider();
        await using (provider)
        {
            await using var scope = provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            using var human = services.GetRequiredService<ICallerContextAccessor>().Enter(A.Caller().AtTerminal().Client(ClientLabel.Cli).Build());   // approve is protected
            _ = await services.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct);
            var approve = services.GetRequiredService<IService<ApproveProfileRequest, ApproveProfileResponse>>();
            var hash = (await approve.Handle(new ApproveProfileRequest(Hash: null, Answer: null), ct)).Value.Hash;
            var issued = (await services.GetRequiredService<IService<IssueChallengeRequest, IssueChallengeResponse>>()
                .Handle(new IssueChallengeRequest("approve profile " + hash.Abbreviated), ct)).Value;
            Assert.True((await approve.Handle(new ApproveProfileRequest(hash, new ChallengeAnswer(issued.Challenge, issued.Action)), ct)).Value.Approved);
            _ = await services.GetRequiredService<IService<StopProcessRequest, StopProcessResponse>>().Handle(new StopProcessRequest(StopReason.Stop), ct);
        }
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args) =>
        CommandTable.Run(args, Environment, TimeProvider.System);
}
