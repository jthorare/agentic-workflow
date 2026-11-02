using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Eval;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

[Lane(TestLane.Integration)]
public sealed class FileEvalFilesTests : EvalFilesContract, IDisposable
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly TempStateDirectory _state = new();

    protected override IEvalFiles Create() => new FileEvalFiles(Path.Combine(_state.Path, "judge", "evals"));

    protected override string ScratchPath(string name) => Path.Combine(_state.Path, "scratch", name);

    [Fact]
    public async Task A_report_lands_in_the_evals_directory()
    {
        var written = await Create().WriteReport("q-jev-as-is-1.json", "{}", _ct);

        Assert.Equal(Path.Combine(_state.Path, "judge", "evals", "q-jev-as-is-1.json"), written.Value);
    }

    [Fact]
    public async Task Record_and_report_files_are_owner_only()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var files = Create();
        var record = ScratchPath("record.jsonl");

        _ = await files.AppendLine(record, "{}", _ct);
        _ = await files.AppendLine(record, "{}", _ct);
        var report = await files.WriteReport("r.md", "text", _ct);

        var ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(ownerOnly, File.GetUnixFileMode(record));
        Assert.Equal(ownerOnly, File.GetUnixFileMode(report.Value));
    }

    [Fact]
    public async Task A_file_that_already_exists_with_wider_rights_is_tightened_by_the_next_write()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var record = ScratchPath("old.jsonl");
        _ = Directory.CreateDirectory(Path.Combine(_state.Path, "scratch"));
        await File.WriteAllTextAsync(record, "{}\n", _ct);
        File.SetUnixFileMode(record, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        _ = await Create().AppendLine(record, "{}", _ct);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(record));
    }

    [Fact]
    public async Task A_path_that_cant_be_created_is_an_error_not_a_throw()
    {
        var blocker = Path.Combine(_state.Path, "blocker");
        await File.WriteAllTextAsync(blocker, "x", _ct);

        var written = await Create().AppendLine(Path.Combine(blocker, "sub", "record.jsonl"), "{}", _ct);

        Assert.True(written.IsError);
    }

    public void Dispose() => _state.Dispose();
}
