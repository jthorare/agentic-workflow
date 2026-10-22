using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Features.CheckPack;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.CheckPack;

/// <summary>'sindri pack check' (line spec §5.3): the schema, the files, the lens count, the hash and the pin line.</summary>
[Lane(TestLane.Unit)]
public sealed class CheckPackServiceTests
{
    private const string Dir = "/packs/mine";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Manifest(params string[] prompts)
    {
        var lenses = string.Join(',', prompts.Select(static (p, i) => $$"""{"id":"lens-{{i.ToString(CultureInfo.InvariantCulture)}}","title":"Lens {{i.ToString(CultureInfo.InvariantCulture)}}","prompt":"{{p}}","when":"always"}"""));
        return $$"""{"kind":"lenses","id":"mine","version":1,"applies":"design","lenses":[{{lenses}}]}""";
    }

    private static Dictionary<string, string> Valid() => new(StringComparer.Ordinal)
    {
        ["pack.json"] = Manifest("lenses/a.md", "lenses/b.md"),
        ["lenses/a.md"] = "Look for A.",
        ["lenses/b.md"] = "Look for B.",
    };

    private static ValueTask<Result<CheckPackResponse>> Check(Dictionary<string, string> files, params string[] outside) =>
        new CheckPackService(new InMemoryPackDirectories().With(Dir, files, outside)).Handle(new CheckPackRequest(Dir), Token);

    [Fact]
    public async Task A_valid_pack_passes_with_its_lens_count_hash_and_pin_line()
    {
        var files = Valid();

        var result = (await Check(files)).Value;

        Assert.True(result.Passed);
        Assert.Empty(result.Findings);
        Assert.Equal("mine", result.Id?.Value);
        Assert.Equal("design", result.Applies);
        Assert.Equal(2, result.Lenses);
        Assert.Equal(LensPackReader.Hash(files), result.Hash);
        Assert.Equal($"workflow.packs.pins: lenses/mine: {result.Hash}", result.Pin);
    }

    [Fact]
    public async Task The_hash_follows_the_files_content()
    {
        var changed = Valid();
        changed["lenses/b.md"] = "Look for something else.";

        Assert.NotEqual((await Check(Valid())).Value.Hash, (await Check(changed)).Value.Hash, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_referenced_file_that_is_missing_fails_and_names_it()
    {
        var files = Valid();
        _ = files.Remove("lenses/b.md");

        var result = (await Check(files)).Value;

        Assert.False(result.Passed);
        Assert.Null(result.Hash);
        Assert.Null(result.Pin);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("SND-PACK-003", finding.Code);
        Assert.Contains("lenses/b.md", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task More_than_ten_lenses_fail()
    {
        var prompts = Enumerable.Range(0, 11).Select(static i => $"lenses/{i.ToString(CultureInfo.InvariantCulture)}.md").ToArray();
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["pack.json"] = Manifest(prompts) };
        foreach (var prompt in prompts)
        {
            files[prompt] = "x";
        }

        var finding = Assert.Single((await Check(files)).Value.Findings);

        Assert.Equal("SND-PACK-003", finding.Code);
        Assert.Contains("11", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pack_without_pack_json_fails()
    {
        var result = (await Check(new Dictionary<string, string>(StringComparer.Ordinal) { ["lenses/a.md"] = "x" })).Value;

        Assert.False(result.Passed);
        Assert.Contains("pack.json", Assert.Single(result.Findings).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pack_whose_id_is_not_its_directory_name_fails()
    {
        var files = Valid();
        files["pack.json"] = files["pack.json"].Replace("\"id\":\"mine\"", "\"id\":\"other\"", StringComparison.Ordinal);

        Assert.False((await Check(files)).Value.Passed);
    }

    [Fact]
    public async Task An_entry_pointing_outside_the_pack_is_a_finding_each_and_no_hash_is_given()
    {
        var result = (await Check(Valid(), "lenses/out.md", "extra.md")).Value;

        Assert.False(result.Passed);
        Assert.Null(result.Hash);
        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, static f => Assert.Equal("SND-PACK-003", f.Code));
        Assert.Contains("lenses/out.md", result.Findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_directory_that_cannot_be_read_is_an_error_not_a_finding()
    {
        var result = await new CheckPackService(new InMemoryPackDirectories()).Handle(new CheckPackRequest("/packs/none"), Token);

        Assert.True(result.IsError);
    }

    [Fact]
    public void A_directory_given_with_a_trailing_slash_names_its_last_segment_as_the_id()
    {
        Assert.Equal("mine", CheckPackService.IdOf("/packs/mine/"));
        Assert.Equal("mine", CheckPackService.IdOf("mine"));
    }
}
