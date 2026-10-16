using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Notifications;

/// <summary>The notifications file in a temp home, with a fake OS notification centre.</summary>
[Lane(TestLane.Integration)]
public sealed class FileDeskNoticesContractTests : DeskNoticesContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IDeskNotices Create() => Create(new FakeOsNotifier());

    /// <summary>A park question can quote a secret-shaped value: it is scrubbed before the file, the digest and the OS banner, never after.</summary>
    [Fact]
    public async Task A_secret_shaped_notice_text_never_reaches_the_file_the_digest_or_the_banner()
    {
        var secret = "ghp_" + new string('a', 36);
        var os = new FakeOsNotifier();
        var notices = Create(os);

        _ = await notices.Send(Notice("WFI-s:1:n1") with { Text = $"fix the token {secret} then resume" }, TestContext.Current.CancellationToken);
        _ = await notices.Send(Notice("WFI-s:2:n2", NoticeClass.Info) with { Text = $"also {secret}" }, TestContext.Current.CancellationToken);

        var everything = string.Concat(Directory.EnumerateFiles(_home.Path, "*.jsonl", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        Assert.Contains("fix the token", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, string.Concat(os.Shown.Select(static n => n.Text)), StringComparison.Ordinal);
        Assert.Contains("fix the token", Assert.Single(os.Shown).Text, StringComparison.Ordinal);
    }

    /// <summary>The digest line is written first and the notifications line commits the send: a failed digest leaves no notice recorded, so the retry sends it whole.</summary>
    [Fact]
    public async Task A_failed_digest_write_leaves_the_notice_unsent_so_the_retry_delivers_both_lines()
    {
        var notices = Create(new FakeOsNotifier());
        var digest = Path.Combine(_home.Path, "digest");
        await File.WriteAllTextAsync(digest, "in the way", TestContext.Current.CancellationToken);   // a file where the directory belongs: the digest append throws

        _ = await Assert.ThrowsAnyAsync<Exception>(async () => await notices.Send(Notice("WFI-d:2:n1", NoticeClass.Info), TestContext.Current.CancellationToken));
        File.Delete(digest);
        var retried = await notices.Send(Notice("WFI-d:2:n1", NoticeClass.Info), TestContext.Current.CancellationToken);

        Assert.True(retried.IsSuccess && retried.Value);
        _ = Assert.Single(await File.ReadAllLinesAsync(Path.Combine(_home.Path, "notifications.jsonl"), TestContext.Current.CancellationToken));
        var day = Assert.Single(Directory.EnumerateFiles(digest));
        _ = Assert.Single(await File.ReadAllLinesAsync(day, TestContext.Current.CancellationToken));
    }

    private FileDeskNotices Create(FakeOsNotifier os) =>
        new(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), os, SliceHarness.Json);
}
