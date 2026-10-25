using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri recipe approve' and 'recipe revoke' against the real host (engine spec §4.2, §6.3, Phase 2's signal): the
/// preview, no typed action, 'approved …' and 'revoked …; N instances parked'; a session may approve too; a file whose eval is missing is SND-RECIPE-008.
/// </summary>
public sealed class RecipeApproveCliTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _home.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Human_approves_with_the_preview_and_revokes_with_the_parked_count()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var step = await Register(app, ct);
        var name = $"workflow.step@{step.Hash.Display}";

        var approved = await Commands(app, human: true, "approve workflow.step 4").Approve(name, ct);
        var approveOut = _out.ToString();
        var revoked = await Commands(app, human: true, $"revoke {name}").Revoke(name, ct);

        Assert.True(approved == 0, _error.ToString());
        Assert.Contains("diff against the last approved version: nothing\n", approveOut, StringComparison.Ordinal);
        Assert.Contains("pinned blocks:\n  step.checks sha256:", approveOut, StringComparison.Ordinal);
        Assert.Contains("evals    3 declared: evals/workflow.step/happy.jsonl", approveOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Type '", approveOut, StringComparison.Ordinal);
        Assert.EndsWith($"approved {name}\n", approveOut, StringComparison.Ordinal);
        Assert.True(revoked == 0, _error.ToString());
        Assert.EndsWith($"revoked {name}; 0 instances parked\n", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Approving_the_same_pins_again_exits_0_and_says_it_woke_the_instances_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var step = await Register(app, ct);
        var name = $"workflow.step@{step.Hash.Display}";
        Assert.Equal(0, await Commands(app, human: true, "approve workflow.step 4").Approve(name, ct));
        _out.GetStringBuilder().Clear();

        var again = await Commands(app, human: true, "approve workflow.step 4").Approve(name, ct);

        Assert.True(again == 0, _error.ToString());
        Assert.EndsWith($"already approved {name}; woke 0 open instances\n", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_session_approves_with_no_challenge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var step = await Register(app, ct);
        var name = $"workflow.step@{step.Hash.Display}";

        var session = await Commands(app, human: false, "approve workflow.step 4").Approve(name, ct);

        Assert.True(session == 0, _error.ToString());
        Assert.NotNull((await app.Services.GetRequiredService<IRecipeApprovals>().Latest(step.Id, 4, step.Hash, ct)).Value);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task File_whose_eval_is_missing_is_SND_RECIPE_008_before_any_call()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var file = Path.Combine(_home.Path, "workflow.step.yaml");
        await File.WriteAllTextAsync(file, ShippedRecipes.Read("sindri/workflow.step.yaml"), ct);

        var code = await Commands(app, human: true, "approve workflow.step 4").Approve(file, ct);

        Assert.Equal(2, code);
        Assert.StartsWith($"SND-RECIPE-008 {file} declares eval evals/workflow.step/happy.jsonl, which is missing", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty((await app.Services.GetRequiredService<IRecipeStore>().All(ct)).Value);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task File_edited_without_raising_its_version_is_SND_RECIPE_007_before_the_prompt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var step = await Register(app, ct);
        var file = await EditedCopy(ct);

        var code = await Commands(app, human: true, "approve workflow.step 4").Approve(file, ct);

        AssertRefusedAsEdited(step, code);
        Assert.Null((await app.Services.GetRequiredService<IRecipeApprovals>().Latest(step.Id, 3, step.Hash, ct)).Value);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task File_edited_without_raising_an_approved_version_is_SND_RECIPE_007_not_already_approved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var step = await Register(app, ct);
        Assert.Equal(0, await Commands(app, human: true, "approve workflow.step 4").Approve($"workflow.step@{step.Hash.Display}", ct));
        _out.GetStringBuilder().Clear();
        _error.GetStringBuilder().Clear();
        var file = await EditedCopy(ct);

        var code = await Commands(app, human: true, "approve workflow.step 4").Approve(file, ct);

        AssertRefusedAsEdited(step, code);
    }

    private void AssertRefusedAsEdited(RegisteredRecipe step, int code)
    {
        Assert.Equal(2, code);
        Assert.DoesNotContain("Type '", _out.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("SND-RECIPE-007 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains($"RCP-VERSION-001: workflow.step version 4 is registered with hash {step.Hash.Display}", _error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>workflow.step's text with one 'next:' line edited and its version left at 2, with its eval files beside it.</summary>
    private async Task<string> EditedCopy(CancellationToken ct)
    {
        var file = Path.Combine(_home.Path, "workflow.step.yaml");
        await File.WriteAllTextAsync(file, ShippedRecipes.Read("sindri/workflow.step.yaml")
            .Replace("Run the Step's deterministic checks.", "Run the edited Step's checks.", StringComparison.Ordinal), ct);
        foreach (var eval in ShippedRecipes.Evals.Where(e => e.StartsWith("sindri/evals/workflow.step/", StringComparison.Ordinal)))
        {
            var to = Path.Combine(_home.Path, eval["sindri/".Length..]);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(to)!);   // why: a file path under the temp home always has a directory
            await File.WriteAllTextAsync(to, ShippedRecipes.Read(eval), ct);
        }

        return file;
    }

    private RecipeDeskCommands Commands(WebApplication app, bool human, string typed)
    {
        var session = new AgentSession(Terminal: true, Prover: false);
        var client = new ControlPlaneClient(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json,
            CallerHeaders.For(session, human ? app.Services.GetRequiredService<IHumanTokenStore>() : new InMemoryHumanTokenStore(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));
        var desk = new RecipeDesk(client, new StringReader(typed + "\n"), static _ => Task.FromResult<Error?>(null));
        return new RecipeDeskCommands(desk, app.Services.CreateScope().ServiceProvider, new Output(_out, _error, SliceHarness.Json));
    }

    private static async Task<RegisteredRecipe> Register(WebApplication app, CancellationToken ct)
    {
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Document!;   // why: the shipped recipes bind (ShippedRecipesTests)
        var text = Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null }, SliceHarness.Json));
        return (await app.Services.GetRequiredService<IRecipeStore>().Add(
            new RegisteredRecipe(document.Recipe.Id, document.Recipe.Version, document.Hash, text, Base: null, DateTimeOffset.UnixEpoch), ct)).Value.Recipe;
    }

    /// <summary>A control plane in the temp home, as CliEndToEndTests starts it: the lease held, this start's token minted and held, its endpoint recorded.</summary>
    private async Task<WebApplication> Started(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        Core.Lease.LeaseGrant grant;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        return app;
    }
}
