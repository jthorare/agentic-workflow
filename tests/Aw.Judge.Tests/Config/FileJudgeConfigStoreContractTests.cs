using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Config;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Config;

[Lane(TestLane.Integration)]
public sealed class FileJudgeConfigStoreContractTests : JudgeConfigStoreContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<IJudgeConfigStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IJudgeConfigStore>(Store(Path.Combine(_state.Path, "judge")));

    protected override async ValueTask<IJudgeConfigStore> CreateUnavailable(CancellationToken cancellationToken)
    {
        var file = Path.Combine(_state.Path, "not-a-directory");
        await File.WriteAllTextAsync(file, "x", cancellationToken);
        return Store(Path.Combine(file, "judge"));
    }

    [Fact]
    public async Task A_file_that_isnt_a_valid_config_is_AW_JUDGE_007_naming_the_file()
    {
        var directory = Path.Combine(_state.Path, "judge");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, FileJudgeConfigStore.FileName);
        await File.WriteAllTextAsync(file, """{"questions":{"x":{"enabled":true,"threshold":-1}}}""", TestContext.Current.CancellationToken);

        var loaded = await Store(directory).Load(TestContext.Current.CancellationToken);

        Assert.Equal(JudgeErrors.ConfigMalformed.Code, loaded.Error.Code);
        Assert.Equal($"invalid config {file}: question 'x': threshold must be between 0 and 1, not -1", loaded.Error.Message);
    }

    [Fact]
    public async Task A_directory_in_the_file_s_place_is_AW_JUDGE_007()
    {
        var directory = Path.Combine(_state.Path, "judge");
        Directory.CreateDirectory(Path.Combine(directory, FileJudgeConfigStore.FileName));

        var loaded = await Store(directory).Load(TestContext.Current.CancellationToken);

        Assert.Equal(JudgeErrors.ConfigMalformed.Code, loaded.Error.Code);
    }

    [Fact]
    public async Task A_save_replaces_the_file_whole_so_a_reader_never_sees_it_half_written()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(_state.Path, "judge");
        var store = Store(directory);
        Assert.True((await store.Save(JudgeConfig.Default, ct)).IsSuccess);
        var file = Path.Combine(directory, FileJudgeConfigStore.FileName);
        var before = await File.ReadAllTextAsync(file, ct);
        var changed = new JudgeConfig(new Dictionary<string, QuestionConfig>(StringComparer.Ordinal) { ["wake-gate"] = new(Enabled: false) });

        // A reader that opened the file before the save keeps the old config whole: the save renames a new file into
        // place instead of truncating and rewriting this one.
        await using var reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Assert.True((await store.Save(changed, ct)).IsSuccess);

        using var text = new StreamReader(reader);
        Assert.Equal(before, await text.ReadToEndAsync(ct));
        Assert.Contains("\"wake-gate\"", await File.ReadAllTextAsync(file, ct), StringComparison.Ordinal);
        Assert.Equal(FileJudgeConfigStore.FileName, Path.GetFileName(Assert.Single(Directory.GetFiles(directory))));
    }

    public void Dispose() => _state.Dispose();

    private static FileJudgeConfigStore Store(string stateDirectory) =>
        new(stateDirectory, AwJson.CreateOptions(JudgeJsonContext.Default));
}
