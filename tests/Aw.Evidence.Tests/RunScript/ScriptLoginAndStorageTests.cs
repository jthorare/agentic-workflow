using Aw.Evidence.Core;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.RunScript;

/// <summary>A script's own login and localStorage (cutover gap G-a, spec §3.3) in the run slice.</summary>
public sealed class ScriptLoginAndStorageTests
{
    private static UiScript With(ScriptLogin? login, IReadOnlyDictionary<string, string>? localStorage = null) =>
        A.Script().Build() with { Login = login, LocalStorage = localStorage };

    [Fact, Trait("Lane", "unit")]
    public async Task A_script_login_signs_in_each_viewport_with_the_credentials_its_variables_name()
    {
        var harness = new RunScriptHarness();
        harness.SecretValues["ADMIN_EMAIL"] = "admin@example.com";
        harness.SecretValues["ADMIN_PASSWORD"] = "hunter2";
        _ = harness.WithScript(With(new ScriptLogin("ADMIN_EMAIL", "ADMIN_PASSWORD")) with { Viewports = [ViewportKind.Desktop, ViewportKind.Phone] });

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(2, harness.Browser.Sessions.Count);
        Assert.All(harness.Browser.Sessions, s => Assert.Equal("sign-in", s.Calls[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_script_login_whose_variables_are_unset_is_refused_before_a_browser_opens()
    {
        var harness = new RunScriptHarness();
        _ = harness.WithScript(With(new ScriptLogin("ADMIN_EMAIL", "ADMIN_PASSWORD")));

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.Equal(EvidenceErrors.CredentialsMissing.Code, result.Error.Code);
        Assert.Empty(harness.Browser.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_sign_in_never_echoes_the_credentials()
    {
        var harness = new RunScriptHarness();
        harness.Page.SignInFails = true;
        harness.SecretValues["ADMIN_EMAIL"] = "admin@example.com";
        harness.SecretValues["ADMIN_PASSWORD"] = "hunter2";
        _ = harness.WithScript(With(new ScriptLogin("ADMIN_EMAIL", "ADMIN_PASSWORD")));

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.True(result.IsError);
        Assert.DoesNotContain("admin@example.com", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Script_local_storage_reaches_every_session_unchanged()
    {
        var harness = new RunScriptHarness();
        var entries = new Dictionary<string, string>(StringComparer.Ordinal) { ["theme"] = "dark" };
        _ = harness.WithScript(With(login: null, entries));

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.All(harness.Browser.Sessions, s => Assert.Equal(entries, s.Options.LocalStorage));
    }
}
