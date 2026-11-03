using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.Traits;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>How trait-check treats an edited image file: its size is checked before it is read, and a text file named .png is never judged as text.</summary>
[Lane(TestLane.Unit)]
public sealed class TraitCheckImageLimitsTests
{
    [Fact]
    public async Task TraitCheck_edited_png_over_the_cap_is_skipped_without_being_read()
    {
        using var repo = new TraitRepo();
        using var state = new TempStateDirectory();
        var big = ImageBytes.Png(600, 400, "v1").Concat(new byte[5 * 1024 * 1024]).ToArray();
        await File.WriteAllBytesAsync(Path.Combine(repo.Root, "src", "big.png"), big, TestContext.Current.CancellationToken);
        Commit(repo);
        await File.WriteAllBytesAsync(Path.Combine(repo.Root, "src", "big.png"), [.. big, 1], TestContext.Current.CancellationToken);
        var files = new RecordingFiles(new FileTraitFiles(repo.Root));
        var jev = CheckTraitsHarness.Violating();

        var result = await Service(state, files, jev).Handle(A.CheckTraitsRequest().Edit("src/big.png").Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("image-not-sent", Assert.Single(result.Value.Skipped).Value);
        Assert.Empty(files.ImageReads);
        Assert.Empty(jev.Asked);
    }

    [Fact]
    public async Task TraitCheck_a_text_file_named_png_is_image_not_sent_only_and_not_judged_as_text()
    {
        using var repo = new TraitRepo();
        using var state = new TempStateDirectory();
        repo.Write("src/notes.png", "// not a picture\n");
        Commit(repo);
        repo.Write("src/notes.png", "// TODO: still not a picture\n");
        var jev = CheckTraitsHarness.Violating();

        var result = await Service(state, new FileTraitFiles(repo.Root), jev).Handle(A.CheckTraitsRequest().Edit("src/notes.png").Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Findings);
        Assert.Equal("image-not-sent", Assert.Single(result.Value.Skipped).Value);
        Assert.Empty(jev.Asked);
    }

    private static CheckTraitsService Service(TempStateDirectory state, ITraitFiles files, FakeBatchClassifier jev) => new(
        new SystemProcessRunner(Environment.GetEnvironmentVariable),
        new FileTraitStore(Path.Combine(state.Path, "traits")),
        files,
        new JevTraitJudge(jev, new FakeInputRedactor(), new InMemoryJudgeConfigStore()),
        new RecordingTraitLog(),
        new FakeTimeProvider());

    private static void Commit(TraitRepo repo)
    {
        _ = repo.Git("add", "-A");
        _ = repo.Git("-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "files");
    }

    // The real trait files, remembering which images were read.
    private sealed class RecordingFiles(ITraitFiles inner) : ITraitFiles
    {
        private readonly List<string> _reads = [];

        public IReadOnlyList<string> ImageReads => _reads;

        public string WorkingDirectory => inner.WorkingDirectory;

        public string? ReadText(string path) => inner.ReadText(path);

        public long? FileSize(string path) => inner.FileSize(path);

        public ValueTask<ImageRead> ReadImage(string path, string root, CancellationToken cancellationToken)
        {
            _reads.Add(path);
            return inner.ReadImage(path, root, cancellationToken);
        }
    }
}
