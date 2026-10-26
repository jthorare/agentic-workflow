using Aw.Judge.Core.Questions;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class ModelPickPackTests
{
    private static ModelPickInput Pack(IEnumerable<string> paths, string brief = "Draft.", IReadOnlyList<string>? deny = null, params string[] known)
    {
        var input = new ModelPickInput(
            "step.interview.questions",
            "questions",
            brief,
            [.. paths.Select(p => new ModelPickFile(p, "text of " + p))],
            [new ModelPickCandidate("m1", "haiku", "cheap")],
            deny);
        return ModelPickPack.Apply(input, new FakeInputRedactor(known));
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("config/.env.production")]
    [InlineData("certs/server.pem")]
    [InlineData("certs/server.key")]
    [InlineData("certs/store.p12")]
    [InlineData("home/id_rsa")]
    [InlineData("home/id_rsa.pub")]
    [InlineData("deploy/secrets.yaml")]
    [InlineData("deploy/credentials.json")]
    [InlineData("infra/prod.tfvars")]
    public void A_built_in_secret_file_name_drops_the_file_as_secret(string path)
    {
        var packed = Pack([path, "src/Ok.cs"]);

        Assert.Equal(["src/Ok.cs"], packed.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(new ModelPickWithheld(Secret: 1, Denied: 0), packed.Withheld);
        Assert.Equal([new ModelPickOmitted(path, ("text of " + path).Length, "secret")], packed.Dropped);
    }

    [Theory]
    [InlineData("src/App.cs")]
    [InlineData("src/environment.cs")]
    [InlineData("docs/secret-handling.md")]
    [InlineData("docs/keys.md")]
    public void An_ordinary_file_name_is_kept(string path)
    {
        var packed = Pack([path]);

        Assert.Equal([path], packed.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(new ModelPickWithheld(0, 0), packed.Withheld);
    }

    [Theory]
    [InlineData("internal/plan.md")]
    [InlineData("internal/deep/plan.md")]
    [InlineData("src/Types.gen.cs")]
    public void A_deny_path_glob_drops_the_file_as_denied(string path)
    {
        var packed = Pack([path, "src/Ok.cs"], deny: ["internal/**", "**/*.gen.cs"]);

        Assert.Equal(["src/Ok.cs"], packed.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(new ModelPickWithheld(Secret: 0, Denied: 1), packed.Withheld);
        Assert.Equal("denied", Assert.Single(packed.Dropped!).Why);
    }

    [Fact]
    public void A_secret_in_a_files_text_drops_the_whole_file_not_the_span()
    {
        var found = SampleSecrets.GitHubToken;
        var input = new ModelPickInput(
            "s", "questions", "Draft.",
            [new ModelPickFile("src/Ok.cs", "fine"), new ModelPickFile("src/Config.cs", $"var t = \"{found}\";")],
            [new ModelPickCandidate("m1", "haiku", "cheap")]);

        var packed = ModelPickPack.Apply(input, new FakeInputRedactor(found));

        Assert.Equal(["src/Ok.cs"], packed.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal("secret", Assert.Single(packed.Dropped!).Why);
    }

    [Fact]
    public void A_secret_in_the_brief_is_masked_at_the_span_and_the_rest_is_kept()
    {
        var found = SampleSecrets.AnthropicKey;

        var packed = Pack(["src/Ok.cs"], brief: $"Before. The value is {found} for staging. After.", known: found);

        Assert.Equal($"Before. The value is {FakeInputRedactor.Placeholder} for staging. After.", packed.Brief);
        Assert.Equal(["src/Ok.cs"], packed.Files.Select(f => f.Path), StringComparer.Ordinal);
    }

    [Fact]
    public void The_deny_paths_and_the_caller_supplied_output_members_are_not_passed_on()
    {
        var input = new ModelPickInput(
            "s", "questions", "Draft.", [new ModelPickFile("src/Ok.cs", "fine")], [new ModelPickCandidate("m1", "haiku", "cheap")],
            DenyPaths: ["internal/**"],
            Omitted: [new ModelPickOmitted("forged.cs", 1, "cap")],
            Cut: [new ModelPickCut("forged.cs", 1, 2)],
            Withheld: new ModelPickWithheld(9, 9));

        var packed = ModelPickPack.Apply(input, new FakeInputRedactor());

        Assert.Null(packed.DenyPaths);
        Assert.Empty(packed.Omitted ?? []);
        Assert.Empty(packed.Cut ?? []);
        Assert.Equal(new ModelPickWithheld(0, 0), packed.Withheld);
    }

    [Fact]
    public void The_brief_counts_against_the_cap_before_the_files()
    {
        var input = new ModelPickInput(
            "s", "questions", new string('b', 60_000),
            [new ModelPickFile("src/a.cs", ModelPickJson.Lines(30_000)), new ModelPickFile("src/b.cs", ModelPickJson.Lines(30_000))],
            [new ModelPickCandidate("m1", "haiku", "cheap")]);

        var packed = ModelPickPack.Apply(input, new FakeInputRedactor());

        Assert.Equal(["src/a.cs"], packed.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal([new ModelPickOmitted("src/b.cs", 30_000, "cap")], packed.Omitted);
    }
}
