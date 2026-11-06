using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Engine;

public sealed class LibrarySettingsTests
{
    private const string Home = "/home/harbor";

    private static Result<ContextSettings> Read(string library) =>
        SettingsReader.Read(JsonDocument.Parse("{\"sources\":{\"library\":" + library + "}}").RootElement.Clone());

    private static LibraryHost Host(string? sindriHome = "/home/harbor/.agentic-workflow/sindri-app", Func<string, string>? real = null) =>
        new() { Home = Home, SindriHome = sindriHome, StateDefault = Home + "/.agentic-workflow", RealPath = real ?? (static p => p) };

    private static LibrarySource Source(string json) => Read(json).Value.Library!;

    [Fact, Trait("Lane", "unit")]
    public void A_profile_with_no_library_key_has_no_library_and_no_model_roots()
    {
        var settings = SettingsReader.Read(JsonDocument.Parse("""{"sources":{}}""").RootElement.Clone());

        Assert.Null(settings.Value.Library);
        Assert.Empty(settings.Value.LibraryModelRoots);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_keys_are_read_with_their_defaults()
    {
        var library = Source("""{"vaults":[{"name":"work","path":"~/Documents/WorkVault","deny":["Private/**"]},{"name":"notes","path":"/srv/notes","models":true}]}""");

        Assert.Null(library.StateDir);
        Assert.False(library.Models);
        Assert.Equal("work,notes", string.Join(',', library.Vaults.Select(v => v.Name)));
        Assert.Equal(["Private/**"], library.Vaults[0].Deny);
        Assert.Equal([false, true], library.Vaults.Select(v => v.Models));
    }

    [Fact, Trait("Lane", "unit")]
    public void Model_roots_are_the_aw_root_and_the_vaults_that_set_models()
    {
        var settings = Read("""{"models":true,"vaults":[{"name":"work","path":"/srv/work","models":true},{"name":"quiet","path":"/srv/quiet"}]}""");

        Assert.Equal(["aw", "work"], settings.Value.LibraryModelRoots);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_turned_off_aw_root_is_never_a_model_root()
        => Assert.Empty(Read("""{"stateDir":"off","models":true}""").Value.LibraryModelRoots);

    [Theory, Trait("Lane", "unit")]
    [InlineData("Work")]
    [InlineData("aw")]
    [InlineData("repo")]
    [InlineData("repo-docs")]
    [InlineData("a_b")]
    [InlineData("")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789")]
    public void A_vault_name_outside_the_rule_is_refused_naming_the_key_path(string name)
    {
        var settings = Read("{\"vaults\":[{\"name\":\"" + name + "\",\"path\":\"/srv/work\"}]}");

        Assert.Equal("SND-LIB-001", settings.Error.Code);
        Assert.StartsWith("context.sources.library.vaults[0].name:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_used_twice_is_refused_at_the_second()
    {
        var settings = Read("""{"vaults":[{"name":"work","path":"/srv/a"},{"name":"work","path":"/srv/b"}]}""");

        Assert.Equal("SND-LIB-001", settings.Error.Code);
        Assert.StartsWith("context.sources.library.vaults[1].name:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Eleven_vaults_are_refused_and_ten_are_not()
    {
        static string Vaults(int n) => "{\"vaults\":[" + string.Join(',', Enumerable.Range(0, n).Select(i => string.Create(CultureInfo.InvariantCulture, $"{{\"name\":\"v{i}\",\"path\":\"/srv/v{i}\"}}"))) + "]}";

        Assert.True(Read(Vaults(10)).IsSuccess);
        Assert.StartsWith("context.sources.library.vaults:", Read(Vaults(11)).Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("relative/path")]
    [InlineData("")]
    [InlineData("/srv/../etc")]
    [InlineData("~/../elsewhere")]
    [InlineData("~other/notes")]
    public void A_vault_path_that_is_not_absolute_or_has_dots_is_refused_naming_the_key_path(string path)
    {
        var settings = Read("{\"vaults\":[{\"name\":\"work\",\"path\":\"" + path + "\"}]}");

        Assert.Equal("SND-LIB-001", settings.Error.Code);
        Assert.StartsWith("context.sources.library.vaults[0].path:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"colour":"red"}""", "context.sources.library.colour:")]
    [InlineData("""{"vaults":[{"name":"work","path":"/srv/w","colour":"red"}]}""", "context.sources.library.vaults[0].colour:")]
    [InlineData("""{"vaults":["work"]}""", "context.sources.library.vaults[0]:")]
    [InlineData("""{"vaults":"work"}""", "context.sources.library.vaults:")]
    [InlineData("""[]""", "context.sources.library:")]
    [InlineData("""{"vaults":[{"name":"work"}]}""", "context.sources.library.vaults[0].path:")]
    public void A_wrong_shape_is_refused_naming_the_key_path(string library, string expected)
    {
        var settings = Read(library);

        Assert.Equal("SND-LIB-001", settings.Error.Code);
        Assert.StartsWith(expected, settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_leading_tilde_is_expanded_to_the_home_before_the_path_is_checked()
    {
        var roots = LibraryPaths.Resolve(Source("""{"vaults":[{"name":"work","path":"~/Documents/WorkVault"}]}"""), Host()).Value;

        Assert.Equal("aw,work", string.Join(',', roots.Select(r => r.Name)));
        Assert.Equal(Home + "/Documents/WorkVault", roots[1].Path);
        Assert.Equal(Home + "/.agentic-workflow", roots[0].Path);
        Assert.True(roots[0].IsAw);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_dir_is_used_as_written_and_off_drops_the_aw_root()
    {
        var written = LibraryPaths.Resolve(Source("""{"stateDir":"/srv/aw"}"""), Host()).Value;
        var off = LibraryPaths.Resolve(Source("""{"stateDir":"off"}"""), Host()).Value;

        Assert.Equal("/srv/aw", written.Single().Path);
        Assert.Empty(off);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/")]
    [InlineData("~/")]
    [InlineData("/home/harbor")]
    [InlineData("/home/harbor/")]
    [InlineData("/home")]
    [InlineData("/home/harbor/.agentic-workflow")]
    [InlineData("/home/harbor/.agentic-workflow/sindri-app")]
    public void A_vault_at_the_root_the_home_or_holding_the_sindri_home_is_refused(string path)
    {
        var resolved = LibraryPaths.Resolve(Source("{\"vaults\":[{\"name\":\"work\",\"path\":\"" + path + "\"}]}"), Host());

        Assert.Equal("SND-LIB-001", resolved.Error.Code);
        Assert.StartsWith("context.sources.library.vaults[0].path:", resolved.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_aw_root_may_hold_the_sindri_home_but_may_not_be_the_home()
    {
        Assert.True(LibraryPaths.Resolve(Source("{}"), Host()).IsSuccess);
        var home = LibraryPaths.Resolve(Source("""{"stateDir":"~/"}"""), Host());

        Assert.StartsWith("context.sources.library.stateDir:", home.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/srv/a", "/srv/a/inner")]
    [InlineData("/srv/a/inner", "/srv/a")]
    [InlineData("/srv/a", "/srv/a/")]
    [InlineData("/srv/a", "/home/harbor/.agentic-workflow/notes")]
    public void A_root_inside_another_is_refused_at_the_later_vault(string first, string second)
    {
        var json = "{\"vaults\":[{\"name\":\"one\",\"path\":\"" + first + "\"},{\"name\":\"two\",\"path\":\"" + second + "\"}]}";

        var resolved = LibraryPaths.Resolve(Source(json), Host());

        Assert.Equal("SND-LIB-001", resolved.Error.Code);
        Assert.Contains("context.sources.library.vaults[", resolved.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Roots_are_compared_by_their_real_path()
    {
        static string Real(string path) => path == "/srv/link" ? "/srv/a/inner" : path;
        var json = """{"vaults":[{"name":"one","path":"/srv/a"},{"name":"two","path":"/srv/link"}]}""";

        var resolved = LibraryPaths.Resolve(Source(json), Host(real: Real));

        Assert.StartsWith("context.sources.library.vaults[1].path:", resolved.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Siblings_with_a_shared_name_prefix_are_not_nested_and_a_missing_folder_is_not_an_error()
    {
        var json = """{"vaults":[{"name":"one","path":"/srv/vault"},{"name":"two","path":"/srv/vault-two"}]}""";

        Assert.True(LibraryPaths.Resolve(Source(json), Host()).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_roots_carry_their_deny_globs_and_models_flags()
    {
        var roots = LibraryPaths.Resolve(Source("""{"models":true,"vaults":[{"name":"work","path":"/srv/work","deny":["Private/**"],"models":true}]}"""), Host()).Value;

        Assert.Equal([true, true], roots.Select(r => r.Models));
        Assert.Equal(["Private/**"], roots[1].Deny);
    }
}
