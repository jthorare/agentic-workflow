using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Tests.OnyxPinsTests;

public sealed class OnyxPinsParseTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string _sha = new('a', 64);
    private static readonly string _digest = "sha256:" + new string('b', 64);

    private static string Fixture(string extra = "") => string.Join(
        '\n',
        "# a comment",
        "",
        $"version v4.9.0 {Commit}",
        $"file docker_compose/docker-compose.yml {_sha}",
        $"image docker.io/o/app v1 linux/arm64 {_digest}",
        $"image docker.io/o/app v1 linux/amd64 {_digest}",
        "need disk-bytes 100",
        "need docker-memory-bytes 200",
        extra);

    [Fact, Trait("Lane", "unit")]
    public void Parse_reads_every_row_kind()
    {
        var pins = OnyxPins.Parse(Fixture()).Value;

        Assert.Equal("v4.9.0", pins.Version);
        Assert.Equal(Commit, pins.Commit);
        Assert.Equal([new OnyxFilePin("docker_compose/docker-compose.yml", _sha)], pins.Files);
        Assert.Equal(
            [new OnyxImagePin("docker.io/o/app", "v1", "linux/arm64", _digest), new OnyxImagePin("docker.io/o/app", "v1", "linux/amd64", _digest)],
            pins.Images);
        Assert.Equal(100, pins.DiskBytes);
        Assert.Equal(200, pins.DockerMemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_missing_version_row()
    {
        var result = OnyxPins.Parse(Fixture().Replace($"version v4.9.0 {Commit}", "", StringComparison.Ordinal));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("no version row", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("frobnicate x")]
    [InlineData("file /abs/path aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("image docker.io/o/app v1 linux/arm64 sha256:abc")]
    [InlineData("need disk-bytes lots")]
    [InlineData("need cpu-cores 4")]
    public void Parse_refuses_a_row_the_bash_check_would_refuse(string row)
    {
        var result = OnyxPins.Parse(Fixture(row));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.StartsWith("the Onyx pins are malformed: ", result.Error.Message, StringComparison.Ordinal);
    }

    // The bad row REPLACES the valid version row (appended it would be a second version row, which hides the tag and commit checks).
    [Theory, Trait("Lane", "unit")]
    [InlineData("version 4.9 " + Commit, "version tag \"4.9\" is not vX.Y.Z")]
    [InlineData("version v4.9 " + Commit, "version tag \"v4.9\" is not vX.Y.Z")]
    [InlineData("version v4.9.0 notacommit", "version commit is not 40 lowercase hex digits")]
    [InlineData("version v4.9.0 0123456789ABCDEF0123456789abcdef01234567", "version commit is not 40 lowercase hex digits")]
    [InlineData("version v4.9.0 0123456789abcdef", "version commit is not 40 lowercase hex digits")]
    [InlineData("version v4.9.0", "version wants 2 fields (tag commit), got 1")]
    public void Parse_refuses_a_bad_version_row_for_its_own_reason(string versionRow, string reason)
    {
        var result = OnyxPins.Parse(Fixture().Replace($"version v4.9.0 {Commit}", versionRow, StringComparison.Ordinal));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("line 3: " + reason, result.Error.Message, StringComparison.Ordinal);
    }

    // The same malformed fixtures as scripts/tests/onyx-lock.test.sh (its "parity" cases): each is the valid file with one row
    // dropped (by prefix) and one or more rows added, and both parsers refuse it for that row.
    private const string Sha64 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory, Trait("Lane", "unit")]
    [InlineData("need disk-bytes", "need disk-bytes 1234567890123456789")]
    [InlineData("", "file docker_compose/docker-compose.yml " + Sha64)]
    [InlineData("", "image docker.io/o/app v1 linux/arm64 " + Digest)]
    [InlineData("", "image Bad_Repo v1 linux/arm64 " + Digest + "\nimage Bad_Repo v1 linux/amd64 " + Digest)]
    [InlineData("", "image -lead v1 linux/arm64 " + Digest + "\nimage -lead v1 linux/amd64 " + Digest)]
    [InlineData("", "image docker.io/o/other bad:tag linux/arm64 " + Digest + "\nimage docker.io/o/other bad:tag linux/amd64 " + Digest)]
    [InlineData("", "image docker.io/o/other -v1 linux/arm64 " + Digest + "\nimage docker.io/o/other -v1 linux/amd64 " + Digest)]
    [InlineData("image docker.io/o/app v1 linux/amd64", "image docker.io/o/app v2 linux/amd64 " + Digest)]
    [InlineData("", "file a\\b " + Sha64)]
    [InlineData("", "file a//b " + Sha64)]
    [InlineData("", "file a/ " + Sha64)]
    [InlineData("", "file ../x " + Sha64)]
    [InlineData("", "file a/../x " + Sha64)]
    [InlineData("", "file /x " + Sha64)]
    [InlineData("", "file a\0b " + Sha64)]
    [InlineData("need disk-bytes", "need disk-bytes\u00a0100")]
    [InlineData("", "file a[1] " + Sha64)]
    [InlineData("", "file a?b " + Sha64)]
    [InlineData("", "file a%2e " + Sha64)]
    [InlineData("", "file a#b " + Sha64)]
    [InlineData("", "file a:b " + Sha64)]
    [InlineData("", "file a* " + Sha64)]
    [InlineData("", "\r")]
    [InlineData("", "# note\r")]
    [InlineData("", "need disk-bytes 100\r # c")]
    [InlineData("", "# a\vb")]
    [InlineData("", "# a\fb")]
    public void Parse_refuses_the_malformed_fixtures_the_bash_check_refuses(string dropPrefix, string rows)
    {
        var kept = Fixture().Split('\n').Where(l => dropPrefix.Length == 0 || !l.StartsWith(dropPrefix, StringComparison.Ordinal));
        var text = string.Join('\n', kept.Append(rows));

        var result = OnyxPins.Parse(text);

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("line ", result.Error.Message, StringComparison.Ordinal);
    }

    // awk splits fields on space and tab only: a CRLF file leaves "\r" on every row's last field, and a no-break space joins two fields.
    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_file_with_crlf_line_endings()
    {
        var result = OnyxPins.Parse(Fixture().Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("image docker.io/library/postgres 15.2-alpine linux/arm64 " + Digest + "\nimage docker.io/library/postgres 15.2-alpine linux/amd64 " + Digest)]
    [InlineData("image ghcr.io/o/a_b-c.d/e RELEASE.2025-07-23T15-54-02Z-cpuv1 linux/arm64 " + Digest + "\nimage ghcr.io/o/a_b-c.d/e RELEASE.2025-07-23T15-54-02Z-cpuv1 linux/amd64 " + Digest)]
    [InlineData("file data/nginx/app.conf.template " + Sha64)]
    public void Parse_accepts_what_the_bash_check_accepts(string rows)
        => Assert.True(OnyxPins.Parse(Fixture(rows)).IsSuccess);

    // The same two fixtures as scripts/tests/onyx-lock.test.sh: the valid file without its image rows, and without its compose file row.
    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_lock_with_no_image_rows()
    {
        var text = string.Join('\n', Fixture().Split('\n').Where(static l => !l.StartsWith("image", StringComparison.Ordinal)));

        var result = OnyxPins.Parse(text);

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("no image rows", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_lock_with_no_compose_file_row()
    {
        var text = string.Join('\n', Fixture().Split('\n').Where(static l => !l.StartsWith("file", StringComparison.Ordinal)));

        var result = OnyxPins.Parse(text);

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("no file row for docker_compose/docker-compose.yml", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_an_image_missing_a_platform()
    {
        var result = OnyxPins.Parse(Fixture().Replace($"image docker.io/o/app v1 linux/amd64 {_digest}", "", StringComparison.Ordinal));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("docker.io/o/app has no linux/amd64 row", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_second_version_row()
        => Assert.True(OnyxPins.Parse(Fixture($"version v4.9.1 {Commit}")).IsError);

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_missing_need_row()
        => Assert.True(OnyxPins.Parse(Fixture().Replace("need docker-memory-bytes 200", "", StringComparison.Ordinal)).IsError);
}
