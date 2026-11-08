using Aw.Kernel;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>
/// The Linear connection save approves in one click only when the live file is exactly the approved profile plus the keys that click wrote (by hash, as the login save does): a change the profile diff doesn't list,
/// a hand-edited url or teams, or an empty diff while something else waits never ride along. The real approval runs over a store that writes the profile file the way the product's store does.
/// </summary>
public sealed class LinearConnectionApprovalTests
{
    private const string Connect = "/sindri/linear/connection";
    private const string DisconnectWrite = "/sindri/linear/connection/write";
    private const string Read = """{"kind":"read","key":"lin_api_read","teams":["ENG"]}""";
    private const string Write = """{"kind":"write","key":"lin_api_write"}""";
    private const string Approved = "\"approved\":true";
    private const string NotApproved = "\"approved\":false";

    private const string BaseYaml = GoldenHost.ProfileYaml
        + "context:\n  sources:\n    linear:\n      token: \"env:LINEAR_API_KEY_OLD\"\n      teams: [OPS]\n";

    [Fact, Trait("Lane", "integration")]
    public async Task A_change_the_diff_misses_is_not_approved_with_the_read_key()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml, ct);
        // an empty settings entry: a real change to the profile hash that ProfileDiff lists no key for
        await Edit(host, text => text + "review:\n  settings:\n    extra: {}\n", ct);

        var answer = await host.Http.Ask(Post(Connect, Read, ItemScenarios.Human(host)));

        Assert.Contains(NotApproved, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pending_url_edit_refuses_the_read_key_save_before_the_key_is_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml, ct);
        await Edit(host, text => text.Replace("    linear:\n", "    linear:\n      url: http://127.0.0.1:9/graphql\n", StringComparison.Ordinal), ct);

        var answer = await host.Http.Ask(Post(Connect, Read, ItemScenarios.Human(host)));

        Assert.Equal(409, (int)answer.Status);
        Assert.Contains("SND-LINEAR-016", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("LINEAR_API_KEY\"", await File.ReadAllTextAsync(Path.Combine(host.Home, "profile", "profile.yaml"), ct), StringComparison.Ordinal);   // nothing was stored: the old pointer is still the only one
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Pending_teams_are_not_approved_with_the_write_key()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml, ct);
        await Edit(host, text => text.Replace("teams: [OPS]", "teams: [OPS, ENG]", StringComparison.Ordinal), ct);

        var answer = await host.Http.Ask(Post(Connect, Write, ItemScenarios.Human(host)));

        Assert.Contains(NotApproved, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Saving_the_same_read_key_again_does_not_approve_another_change_that_waits()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml, ct);
        var first = await host.Http.Ask(Post(Connect, Read, ItemScenarios.Human(host)));
        Assert.Contains(Approved, first.Body, StringComparison.Ordinal);
        await Edit(host, text => text + "review:\n  settings:\n    extra: {}\n", ct);

        // the file's linear keys already hold this token and these teams, so the diff against the approval lists nothing for this click
        var again = await host.Http.Ask(Post(Connect, Read, ItemScenarios.Human(host)));

        Assert.Contains(NotApproved, again.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Removing_only_the_write_key_is_approved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml.Replace("      teams: [OPS]\n", "      teams: [OPS]\n      writeToken: \"env:LINEAR_WRITE_KEY\"\n", StringComparison.Ordinal), ct);

        var answer = await host.Http.Ask(Delete(DisconnectWrite, ItemScenarios.Human(host)));

        Assert.Contains(Approved, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_read_key_save_that_is_the_only_change_is_approved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml, ct);

        var answer = await host.Http.Ask(Post(Connect, Read, ItemScenarios.Human(host)));

        Assert.Contains(Approved, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_write_key_save_that_is_the_only_change_is_approved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(BaseYaml, ct);

        var answer = await host.Http.Ask(Post(Connect, Write, ItemScenarios.Human(host)));

        Assert.Contains(Approved, answer.Body, StringComparison.Ordinal);
    }

    // The real approval over fake Linear checks and a store that writes the profile file through the real writers (the keychain is the only part left out): the profile is approved first, then the test makes its pending edit.
    private static async Task<GoldenHost> Start(string yaml, CancellationToken ct)
    {
        var host = await GoldenHost.Start(temporary: true, ct, yaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearKeyChecks>(new FakeLinearKeyChecks().Reading(new LinearConnectionTeamView("e1", "ENG", "Engineering"))));
            _ = services.Replace(ServiceDescriptor.Scoped<ILinearConnectionStore>(static sp => new ProfileWritingStore(sp.GetRequiredService<ISourceTokenProfileWriter>(), sp.GetRequiredService<ILinearWriteTokenWriter>())));
        });
        try
        {
            await ViewScenarios.Approve(host, ct);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    private static async Task Edit(GoldenHost host, Func<string, string> change, CancellationToken ct)
    {
        var file = Path.Combine(host.Home, "profile", "profile.yaml");
        await File.WriteAllTextAsync(file, change(await File.ReadAllTextAsync(file, ct)), ct);
    }

    private sealed class ProfileWritingStore(ISourceTokenProfileWriter tokens, ILinearWriteTokenWriter writeTokens) : ILinearConnectionStore
    {
        private const string Source = "linear";

        public ValueTask<bool> IsReadKey(string key, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public ValueTask<Result<string>> SaveRead(string key, IReadOnlyList<string> teams, CancellationToken cancellationToken)
        {
            const string pointer = "env:LINEAR_API_KEY";
            var written = tokens.SetToken(Source, pointer);
            var listed = written.IsError ? written : tokens.SetTeams(Source, teams);
            return ValueTask.FromResult<Result<string>>(listed.IsError ? listed.Error : pointer);
        }

        public ValueTask<Result<string>> SaveWrite(string key, CancellationToken cancellationToken)
        {
            const string pointer = "env:LINEAR_WRITE_KEY";
            var written = writeTokens.SetWriteToken(Source, pointer);
            return ValueTask.FromResult<Result<string>>(written.IsError ? written.Error : pointer);
        }

        public ValueTask<Result<bool>> RemoveWrite(CancellationToken cancellationToken) => ValueTask.FromResult(writeTokens.RemoveWriteToken(Source));
    }
}
