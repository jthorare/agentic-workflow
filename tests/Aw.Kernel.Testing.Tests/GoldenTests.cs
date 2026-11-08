namespace Aw.Kernel.Testing.Tests;

/// <summary>Golden checks read and write real files, so they run in the integration lane.</summary>
[Lane(TestLane.Integration)]
public sealed class GoldenTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-golden-").FullName;

    private static Normalization Declared => new(Normalization.IsoTimestamps, Normalization.Guids);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Output_matching_after_normalization_passes() =>
        Golden.Match(
            "run 0e5f38f5-1c2d-4e3f-8a9b-0123456789ab at 2026-10-10T06:00:00.123Z\nok\n",
            typeof(GoldenTests).Assembly,
            "Golden/run.txt",
            Declared);

    [Fact]
    public void A_difference_names_the_first_differing_line()
    {
        Write("a.txt", "one\ntwo\nthree\n");

        var e = Assert.Throws<GoldenMismatchException>(() => Compare("one\nTWO\nthree\n", "a.txt", Normalization.None));

        Assert.Contains("line 2", e.Message, StringComparison.Ordinal);
        Assert.Contains("expected: two", e.Message, StringComparison.Ordinal);
        Assert.Contains("actual:   TWO", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Extra_trailing_output_is_a_difference()
    {
        Write("b.txt", "one\n");

        _ = Assert.Throws<GoldenMismatchException>(() => Compare("one\ntwo\n", "b.txt", Normalization.None));
    }

    [Fact]
    public void A_golden_file_with_crlf_line_endings_still_matches()
    {
        Write("crlf.txt", "one\r\ntwo\r\n");

        Compare("one\ntwo\n", "crlf.txt", Normalization.None);
    }

    [Fact]
    public void A_golden_file_that_is_not_normalized_is_a_difference()
    {
        Write("raw.txt", "at 2026-10-10T06:00:00Z\n");

        var e = Assert.Throws<GoldenMismatchException>(() => Compare("at 2026-10-10T06:00:00Z\n", "raw.txt", Declared));

        Assert.Contains("expected: at 2026-10-10T06:00:00Z", e.Message, StringComparison.Ordinal);
        Assert.Contains("actual:   at <timestamp>", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_golden_file_says_how_to_create_it()
    {
        var e = Assert.Throws<GoldenMismatchException>(() => Compare("x", "none.txt", Normalization.None));

        Assert.Contains(Golden.UpdateVariable + "=1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_mode_writes_the_normalized_output_and_fails_the_run_for_review()
    {
        var e = Assert.Throws<GoldenMismatchException>(
            () => Golden.Match("at 2026-10-10T06:00:00Z\r\n", _dir, "new/n.txt", Declared, update: true, inCi: false));

        Assert.Contains("updated", e.Message, StringComparison.Ordinal);
        Assert.Equal("at <timestamp>\n", File.ReadAllText(Path.Combine(_dir, "new", "n.txt")));
        Compare("at 2026-12-31T23:59:59Z\n", "new/n.txt", Declared);
    }

    [Fact]
    public void Update_mode_passes_and_rewrites_nothing_when_the_file_already_matches()
    {
        Write("same.txt", "at <timestamp>\n");
        var path = Path.Combine(_dir, "same.txt");
        var before = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, before.AddHours(-1));
        var stamped = File.GetLastWriteTimeUtc(path);

        Golden.Match("at 2026-10-10T06:00:00Z\n", _dir, "same.txt", Declared, update: true, inCi: false);

        Assert.Equal(stamped, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Update_mode_still_fails_the_run_when_the_file_differs()
    {
        Write("old.txt", "old\n");

        var e = Assert.Throws<GoldenMismatchException>(
            () => Golden.Match("new\n", _dir, "old.txt", Normalization.None, update: true, inCi: false));

        Assert.Contains("updated", e.Message, StringComparison.Ordinal);
        Assert.Equal("new\n", File.ReadAllText(Path.Combine(_dir, "old.txt")));
    }

    [Fact]
    public void Update_mode_is_refused_in_ci_and_writes_nothing()
    {
        var e = Assert.Throws<GoldenMismatchException>(
            () => Golden.Match("x\n", _dir, "ci.txt", Normalization.None, update: true, inCi: true));

        Assert.Contains("CI", e.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_dir, "ci.txt")));
    }

    [Theory]
    [InlineData("CI", "true", true)]
    [InlineData("GITHUB_ACTIONS", "true", true)]
    [InlineData("CI", "false", false)]
    [InlineData("OTHER", "true", false)]
    public void Ci_is_detected_from_the_environment(string name, string value, bool expected) =>
        Assert.Equal(expected, Golden.IsCi(n => string.Equals(n, name, StringComparison.Ordinal) ? value : null));

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("Golden/../../escape.txt")]
    [InlineData("/etc/hosts")]
    public void Paths_outside_the_source_root_are_refused(string relativePath)
    {
        _ = Assert.Throws<ArgumentException>(
            () => Golden.Match("x\n", _dir, relativePath, Normalization.None, update: true, inCi: false));
        _ = Assert.Throws<ArgumentException>(() => Golden.SourcePath(typeof(GoldenTests).Assembly, relativePath));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_dir)!, "escape.txt")));
    }

    [Fact]
    public void SourcePath_points_into_the_test_project_not_the_build_output()
    {
        var path = Golden.SourcePath(typeof(GoldenTests).Assembly, "Golden/run.txt");

        Assert.True(File.Exists(path), path);
        Assert.DoesNotContain(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
    }

    [Fact]
    public void SourcePath_says_how_to_declare_the_source_root()
    {
        var e = Assert.Throws<InvalidOperationException>(() => Golden.SourcePath(typeof(Golden).Assembly, "x.txt"));

        Assert.Contains(Golden.SourceRootKey, e.Message, StringComparison.Ordinal);
    }

    private void Compare(string actual, string relativePath, Normalization normalization) =>
        Golden.Match(actual, _dir, relativePath, normalization, update: false, inCi: false);

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);
}
