using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IEvalFiles contract: the same tests run against the in-memory fake and the file adapter. A derived class supplies
/// a fresh store and a path for a scratch file by name.
/// </summary>
public abstract class EvalFilesContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh store.</summary>
    /// <returns>The store.</returns>
    protected abstract IEvalFiles Create();

    /// <summary>A path for a scratch file that doesn't exist yet.</summary>
    /// <param name="name">A file name.</param>
    /// <returns>The path.</returns>
    protected abstract string ScratchPath(string name);

    /// <summary>Appended lines read back in order, whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Appended_lines_read_back_in_order()
    {
        var files = Create();
        var path = ScratchPath("record.jsonl");

        Expect.That((await files.AppendLine(path, """{"a":1}""", _none)).IsSuccess, "an append succeeds");
        Expect.That((await files.AppendLine(path, """{"a":2}""", _none)).IsSuccess, "a second append succeeds");

        var read = await files.ReadLines(path, _none);
        Expect.That(read.IsSuccess && read.Value.SequenceEqual(["""{"a":1}""", """{"a":2}"""], StringComparer.Ordinal), "the lines come back in order");
    }

    /// <summary>A file that isn't there can't be read.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_is_an_error()
    {
        var read = await Create().ReadLines(ScratchPath("missing.jsonl"), _none);

        Expect.That(read.IsError, "reading a missing file fails");
    }

    /// <summary>A report is written whole under its name, and a second report of that name replaces it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_report_is_written_under_its_name_and_replaced_by_the_next()
    {
        var files = Create();

        var first = await files.WriteReport("ask-check-jev-as-is-1.md", "first", _none);
        var second = await files.WriteReport("ask-check-jev-as-is-1.md", "second", _none);

        Expect.That(first.IsSuccess && second.IsSuccess && first.Value == second.Value, "both writes name the same file");
        Expect.That(first.Value.EndsWith("ask-check-jev-as-is-1.md", StringComparison.Ordinal), "the path ends with the name");
        var read = await files.ReadLines(second.Value, _none);
        Expect.That(read.IsSuccess && read.Value.SequenceEqual(["second"], StringComparer.Ordinal), "the second text replaced the first");
    }
}
